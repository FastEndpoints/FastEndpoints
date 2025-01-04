using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
#if NET8_0_OR_GREATER
using System.Text.Json.Serialization.Metadata;
using Microsoft.Net.Http.Headers;
#endif

namespace FastEndpoints;

static class BinderExtensions
{
    internal static string BareFieldName(this IFormFile file)
    {
        var indexOfOpeningBracket = file.Name.IndexOf('[');

        return indexOfOpeningBracket != -1 ? file.Name[..indexOfOpeningBracket] : file.Name;
    }

    internal static ICollection<PropertyInfo> BindableProps(this Type type)
    {
        return (Cfg.BndOpts.ReflectionCache.GetOrAdd(type, new ClassDefinition())
                   .Properties ??= new(GetProperties(type))).Keys;

        static IEnumerable<KeyValuePair<PropertyInfo, PropertyDefinition>> GetProperties(Type t)
        {
            return t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                    .Where(
                        p => p.GetSetMethod()?.IsPublic is true &&
                             p.GetGetMethod()?.IsPublic is true &&
                             p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always &&
                             !p.IsDefined(Types.DontInjectAttribute))
                    .Select(p => new KeyValuePair<PropertyInfo, PropertyDefinition>(p, new()));
        }
    }

    static readonly Func<object> _emptyRequestInitializer = () => new EmptyRequest();

    internal static Func<object> ObjectFactory(this Type type)
    {
        if (type == Types.EmptyRequest)
            return _emptyRequestInitializer;

        return Cfg.BndOpts.ReflectionCache.GetOrAdd(type, new ClassDefinition())
                  .ObjectFactory ??= CompileFactory(type);

        static Func<object> CompileFactory(Type t)
        {
            if (t.IsValueType)
                return Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(t), typeof(object))).Compile();

            var ctor = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                        .MinBy(c => c.GetParameters().Length) ??
                       throw new NotSupportedException($"Unable to instantiate type without a constructor! Offender: [{t.FullName}]");

            var args = ctor.GetParameters();
            var argExpressions = new List<Expression>(args.Length);

            for (var i = 0; i < args.Length; i++)
            {
                argExpressions.Add(
                    args[i].HasDefaultValue
                        ? Expression.Constant(args[i].DefaultValue, args[i].ParameterType)
                        : Expression.Default(args[i].ParameterType));
            }
            var ctorExpression = Expression.New(ctor, argExpressions);

            return Expression.Lambda<Func<object>>(ctorExpression).Compile();
        }
    }

    internal static Action<object, object?> SetterForProp(this Type tOwner, PropertyInfo prop)
    {
        if (!Cfg.BndOpts.ReflectionCache.TryGetValue(tOwner, out var classDef))
            throw new InvalidOperationException($"Reflection data not found for: [{tOwner.FullName}]");

        if (classDef.Properties is null)
            throw new InvalidOperationException($"Reflection data not found for properties of: [{tOwner.FullName}]");

        return classDef.Properties.GetOrAdd(prop, new PropertyDefinition())
                       .Setter ??= CompileSetter(tOwner, prop);

        static Action<object, object?> CompileSetter(Type tParent, PropertyInfo p)
        {
            //(object parent, object value) => ((TParent)parent).property = (TProp)value;
            var parent = Expression.Parameter(Types.Object);
            var value = Expression.Parameter(Types.Object);
            var property = Expression.Property(Expression.Convert(parent, tParent), p.Name);
            var body = Expression.Assign(property, Expression.Convert(value, property.Type));

            return Expression.Lambda<Action<object, object?>>(body, parent, value).Compile();
        }
    }

    static readonly ConstructorInfo _parseResultCtor = Types.ParseResult.GetConstructor([Types.Bool, Types.Object])!;

    internal static Func<StringValues, ParseResult> ValueParser(this Type type)
    {
        //user may have already registered a parser func for a given type via config at startup.
        //or reflection source generator may have already populated the cache.
        //if it's not there, compile the func at runtime.
        return Cfg.BndOpts.ReflectionCache.GetOrAdd(type, new ClassDefinition()).ValueParser
                   ??= CompileParser(type);

        static Func<StringValues, ParseResult> CompileParser(Type type)
        {
            type = type.GetUnderlyingType();

            if (type == Types.String)
                return input => new(true, input.ToString());

            if (type.IsEnum)
                return input => new(Enum.TryParse(type, input, true, out var res), res);

            if (type == Types.Uri)
                return input => new(Uri.TryCreate(input, UriKind.Absolute, out var res), res);

            var tryParseMethod = type.GetMethod(
                "TryParse",
                BindingFlags.Public | BindingFlags.Static,
                [Types.String, type.MakeByRefType()]);

            var isIParseable = false;

            if (tryParseMethod is null)
            {
                tryParseMethod = type.GetMethod(
                    "TryParse",
                    BindingFlags.Public | BindingFlags.Static,
                    [Types.String, Types.IFormatProvider, type.MakeByRefType()]);
                isIParseable = tryParseMethod is not null;
            }

            if (tryParseMethod == null || tryParseMethod.ReturnType != Types.Bool)
            {
                var interfaces = type.GetInterfaces();

                return interfaces.Contains(Types.IEnumerable) &&
                       !interfaces.Contains(Types.IDictionary) //dictionaries should be deserialized as json objects
                           ? (type.GetElementType() ?? type.GetGenericArguments().FirstOrDefault()) == Types.Byte
                                 ? input => new(true, DeserializeByteArray(input))
                                 : input => new(true, DeserializeJsonArrayString(input, type))
                           : input => new(true, DeserializeJsonObjectString(input, type));
            }

            // The 'StringValues' parameter passed into our delegate
            var inputParameter = Expression.Parameter(Types.StringValues, "input");

            // (string)input
            var castToString = Expression.Convert(inputParameter, Types.String);

            // 'res' variable used as the out parameter to the TryParse call
            var resultVar = Expression.Variable(type, "res");

            // 'isSuccess' variable to hold the result of calling TryParse
            var isSuccessVar = Expression.Variable(Types.Bool, "isSuccess");

            // To finish off, we need to following sequence of statements:
            //  - isSuccess = TryParse((string)input, res)
            //  - new ParseResult(isSuccess, (object)res)
            // A sequence of statements is done using a block, and the result of the final statement is the result of the block
            var tryParseCall = isIParseable
                                   ? Expression.Call(
                                       tryParseMethod,
                                       castToString,
                                       Expression.Constant(null, CultureInfo.InvariantCulture.GetType()),
                                       resultVar)
                                   : Expression.Call(tryParseMethod, castToString, resultVar);

            var block = Expression.Block(
                [resultVar, isSuccessVar],
                Expression.Assign(isSuccessVar, tryParseCall),
                Expression.New(_parseResultCtor, isSuccessVar, Expression.Convert(resultVar, Types.Object)));

            return Expression.Lambda<Func<StringValues, ParseResult>>(block, inputParameter).Compile();
        }
    }

    //public to make accessible to source generated code
    public static object? DeserializeJsonObjectString(StringValues input, Type tProp)
        => input.Count == 0
               ? null
               : input[0].IsJsonObjectString()
                   ? JsonSerializer.Deserialize(input[0]!, tProp, Cfg.SerOpts.Options)
                   : null;

    //public to make accessible to source generated code
    public static object? DeserializeByteArray(StringValues input)
        => input.Count == 0
               ? null
               : Convert.FromBase64String(input[0]!);

    //public to make accessible to source generated code
    public static object? DeserializeJsonArrayString(StringValues input, Type tProp)
    {
        if (input.Count == 0)
            return null;

        if (input.Count == 1 && input[0].IsJsonArrayString())
        {
            // querystring: ?ids=[1,2,3]
            // possible inputs:
            // - [1,2,3] (as StringValues[0])
            // - ["one","two","three"] (as StringValues[0])
            // - [{"name":"x"},{"name":"y"}] (as StringValues[0])

            return JsonSerializer.Deserialize(input[0]!, tProp, Cfg.SerOpts.Options);
        }

        // querystring: ?ids=one&ids=two
        // possible inputs:
        // - 1 (as StringValues)
        // - 1,2,3 (as StringValues)
        // - one (as StringValues)
        // - one,two,three (as StringValues)
        // - [1,2], 2, 3 (as StringValues)
        // - ["one","two"], three, four (as StringValues)
        // - {"name":"x"}, {"name":"y"} (as StringValues) - from swagger ui

        var isEnumCollection = false;

        if (Types.IEnumerable.IsAssignableFrom(tProp) &&
            int.TryParse(input[0], out _)) //skip if these are not digits due to JsonStringEnumConverter
        {
            if (tProp.IsArray)
                isEnumCollection = tProp.GetElementType()!.IsEnum;
            else if (tProp.IsGenericType)
                isEnumCollection = tProp.GetGenericArguments()[0].IsEnum;
        }

        var sb = new StringBuilder("[");

        for (var i = 0; i < input.Count; i++)
        {
            if (isEnumCollection || input[i].IsJsonObjectString())
                sb.Append(input[i]);
            else
            {
                sb.Append('"')
                  .AppendEscaped(input[i]!)
                  .Append('"');
            }

            if (i < input.Count - 1)
                sb.Append(',');
        }
        sb.Append(']');

        return JsonSerializer.Deserialize(sb.ToString(), tProp, Cfg.SerOpts.Options);
    }

    static bool IsJsonArrayString(this string? val)
        => val?.Length > 1 && val[0] == '[' && val[^1] == ']';

    static bool IsJsonObjectString(this string? val)
        => val?.Length > 1 && val[0] == '{' && val[^1] == '}';

    static StringBuilder AppendEscaped(this StringBuilder sb, string val)
    {
        foreach (var c in val)
        {
            switch (c)
            {
                case '\"':
                    sb.Append("\\\"");

                    break;
                case '\\':
                    sb.Append("\\\\");

                    break;
                case '\b':
                    sb.Append("\\b");

                    break;
                case '\f':
                    sb.Append("\\f");

                    break;
                case '\n':
                    sb.Append("\\n");

                    break;
                case '\r':
                    sb.Append("\\r");

                    break;
                case '\t':
                    sb.Append("\\t");

                    break;
                default:
                    if (char.IsControl(c) || c > 127)
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("x4"));
                    }
                    else
                        sb.Append(c);

                    break;
            }
        }

        return sb;
    }

#if NET8_0_OR_GREATER
    internal static void AddTypedHeaderValueParsers(this BindingOptions o, JsonSerializerOptions jso)
    {
        //header parsers
        o.ValueParserFor<CacheControlHeaderValue>(input => new(CacheControlHeaderValue.TryParse(new(input!), out var res), res));
        o.ValueParserFor<ContentDispositionHeaderValue>(input => new(ContentDispositionHeaderValue.TryParse(new(input!), out var res), res));
        o.ValueParserFor<ContentRangeHeaderValue>(input => new(ContentRangeHeaderValue.TryParse(new(input!), out var res), res));
        o.ValueParserFor<MediaTypeHeaderValue>(input => new(MediaTypeHeaderValue.TryParse(new(input!), out var res), res));
        o.ValueParserFor<RangeConditionHeaderValue>(input => new(RangeConditionHeaderValue.TryParse(new(input!), out var res), res));
        o.ValueParserFor<RangeHeaderValue>(input => new(RangeHeaderValue.TryParse(new(input!), out var res), res));
        o.ValueParserFor<EntityTagHeaderValue>(input => new(EntityTagHeaderValue.TryParse(new(input!), out var res), res));

        //list header parsers
        o.ValueParserFor<IList<MediaTypeHeaderValue>>(input => new(MediaTypeHeaderValue.TryParseList(input!, out var res), res));
        o.ValueParserFor<IList<EntityTagHeaderValue>>(input => new(EntityTagHeaderValue.TryParseList(input!, out var res), res));
        o.ValueParserFor<IList<SetCookieHeaderValue>>(input => new(SetCookieHeaderValue.TryParseList(input!, out var res), res));

        //need to prevent STJ from trying to deserialize these types
        jso.TypeInfoResolver = jso.TypeInfoResolver?.WithAddedModifier(
            ti =>
            {
                if (ti.Kind != JsonTypeInfoKind.Object)
                    return;

                for (var i = 0; i < ti.Properties.Count; i++)
                {
                    var pi = ti.Properties[i];

                    if (pi.AttributeProvider?.IsDefined(Types.FromHeaderAttribute, true) is true && pi.PropertyType.Name.EndsWith("HeaderValue"))
                        ti.Properties.Remove(pi);
                }
            });
    }
#endif
}