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
        var c = Cfg.BndOpts.ReflectionCache.GetOrAdd(type, CreateClassDef);
        c.Properties ??= new(GetProperties(type, c));

        return c.Properties.Keys;

        static ClassDefinition CreateClassDef(Type t)
        {
            var c = new ClassDefinition();
            c.Properties = new(GetProperties(t, c));

            return c;
        }

        static IEnumerable<KeyValuePair<PropertyInfo, PropertyDefinition>> GetProperties(Type t, ClassDefinition c)
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

        return Cfg.BndOpts.ReflectionCache.GetOrAdd(type, CreateClassDef).ObjectFactory
                   ??= CompileFactory(type);

        static ClassDefinition CreateClassDef(Type t)
            => new() { ObjectFactory = CompileFactory(t) };

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
        if (!Cfg.BndOpts.ReflectionCache.TryGetValue(tOwner, out var c))
            throw new InvalidOperationException($"Reflection data not found for: [{tOwner.FullName}]");

        if (c.Properties is null)
            throw new InvalidOperationException($"Reflection data not found for properties of: [{tOwner.FullName}]");

        // ReSharper disable once HeapView.CanAvoidClosure
        return c.Properties.GetOrAdd(prop, p => CreatePropertyDef(p, tOwner)).Setter
                   ??= CompileSetter(tOwner, prop);

        static PropertyDefinition CreatePropertyDef(PropertyInfo p, Type tParent)
            => new() { Setter = CompileSetter(tParent, p) };

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

    //TODO: add support for value parsers to the reflection source generator

    internal static readonly ConcurrentDictionary<Type, Func<object?, ParseResult>> ParserFuncCache = [];
    static readonly MethodInfo _toStringMethod = Types.Object.GetMethod("ToString")!;
    static readonly ConstructorInfo _parseResultCtor = Types.ParseResult.GetConstructor([Types.Bool, Types.Object])!;

    internal static Func<object?, ParseResult> CachedValueParser(this Type type)
    {
        //we're only ever compiling a value parser for a given type once.
        //if a parser is requested for a type a second time, it will be returned from the dictionary instead of paying the compiling cost again.
        //the parser we return from here is then cached in RequestBinder PropCache entries avoiding the need to do repeated dictionary lookups here.
        //it is also possible that the user has already registered a parser func for a given type via config at startup.
        return ParserFuncCache.GetOrAdd(type, GetValueParser);

        static Func<object?, ParseResult> GetValueParser(Type tProp)
        {
            tProp = Nullable.GetUnderlyingType(tProp) ?? tProp;

            //note: the actual type of the `input` to the parser func can be
            //      either [object] or [StringValues]

            if (tProp == Types.String)
                return input => new(true, input?.ToString());

            if (tProp.IsEnum)
                return input => new(Enum.TryParse(tProp, input?.ToString(), true, out var res), res);

            if (tProp == Types.Uri)
                return input => new(Uri.TryCreate(input?.ToString(), UriKind.Absolute, out var res), res);

            var tryParseMethod = tProp.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static, [Types.String, tProp.MakeByRefType()]);

            var isIParseable = false;

            if (tryParseMethod is null)
            {
                tryParseMethod = tProp.GetMethod(
                    "TryParse",
                    BindingFlags.Public | BindingFlags.Static,
                    [Types.String, Types.IFormatProvider, tProp.MakeByRefType()]);
                isIParseable = tryParseMethod is not null;
            }

            if (tryParseMethod == null || tryParseMethod.ReturnType != Types.Bool)
            {
                var interfaces = tProp.GetInterfaces();

                return interfaces.Contains(Types.IEnumerable) &&
                       !interfaces.Contains(Types.IDictionary) //dictionaries should be deserialized as json objects
                           ? (tProp.GetElementType() ?? tProp.GetGenericArguments().FirstOrDefault()) == Types.Byte
                                 ? input => new(true, DeserializeByteArray(input))
                                 : input => new(true, DeserializeJsonArrayString(input, tProp))
                           : input => new(true, DeserializeJsonObjectString(input, tProp));
            }

            // The 'object' parameter passed into our delegate
            var inputParameter = Expression.Parameter(Types.Object, "input");

            // 'input == null ? (string)null : input.ToString()'
            var toStringConversion = Expression.Condition(
                Expression.ReferenceEqual(inputParameter, Expression.Constant(null, Types.Object)),
                Expression.Constant(null, Types.String),
                Expression.Call(inputParameter, _toStringMethod));

            // 'res' variable used as the out parameter to the TryParse call
            var resultVar = Expression.Variable(tProp, "res");

            // 'isSuccess' variable to hold the result of calling TryParse
            var isSuccessVar = Expression.Variable(Types.Bool, "isSuccess");

            // To finish off, we need to following sequence of statements:
            //  - isSuccess = TryParse(input.ToString(), res)
            //  - new ParseResult(isSuccess, (object)res)
            // A sequence of statements is done using a block, and the result of the final
            // statement is the result of the block
            var tryParseCall =
                isIParseable
                    ? Expression.Call(tryParseMethod, toStringConversion, Expression.Constant(null, CultureInfo.InvariantCulture.GetType()), resultVar)
                    : Expression.Call(tryParseMethod, toStringConversion, resultVar);

            var block = Expression.Block(
                [resultVar, isSuccessVar],
                Expression.Assign(isSuccessVar, tryParseCall),
                Expression.New(_parseResultCtor, isSuccessVar, Expression.Convert(resultVar, Types.Object)));

            return Expression.Lambda<Func<object?, ParseResult>>(block, inputParameter).Compile();

            static object? DeserializeJsonObjectString(object? input, Type tProp)
                => input is not StringValues { Count: 1 } vals
                       ? null
                       : vals[0]!.StartsWith('{') && vals[0]!.EndsWith('}') // check if it's a json object
                           ? JsonSerializer.Deserialize(vals[0]!, tProp, Cfg.SerOpts.Options)
                           : null;

            static object? DeserializeByteArray(object? input)
                => input is not StringValues { Count: 1 } vals
                       ? null
                       : Convert.FromBase64String(vals[0]!);

            static object? DeserializeJsonArrayString(object? input, Type tProp)
            {
                if (input is not StringValues vals || vals.Count == 0)
                    return null;

                if (vals.Count == 1 && vals[0]!.StartsWith('[') && vals[0]!.EndsWith(']'))
                {
                    // querystring: ?ids=[1,2,3]
                    // possible inputs:
                    // - [1,2,3] (as StringValues[0])
                    // - ["one","two","three"] (as StringValues[0])
                    // - [{"name":"x"},{"name":"y"}] (as StringValues[0])

                    return JsonSerializer.Deserialize(vals[0]!, tProp, Cfg.SerOpts.Options);
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
                    int.TryParse(vals[0], out _)) //skip if these are not digits due to JsonStringEnumConverter
                {
                    if (tProp.IsArray)
                        isEnumCollection = tProp.GetElementType()!.IsEnum;
                    else if (tProp.IsGenericType)
                        isEnumCollection = tProp.GetGenericArguments()[0].IsEnum;
                }

                var sb = new StringBuilder("[");

                for (var i = 0; i < vals.Count; i++)
                {
                    if (isEnumCollection || (vals[i]!.StartsWith('{') && vals[i]!.EndsWith('}')))
                        sb.Append(vals[i]);
                    else
                    {
                        sb.Append('"')
                          .AppendEscaped(vals[i]!)
                          .Append('"');
                    }

                    if (i < vals.Count - 1)
                        sb.Append(',');
                }
                sb.Append(']');

                return JsonSerializer.Deserialize(sb.ToString(), tProp, Cfg.SerOpts.Options);
            }
        }
    }

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
        o.ValueParserFor<CacheControlHeaderValue>(input => new(CacheControlHeaderValue.TryParse(new((StringValues)input!), out var res), res));
        o.ValueParserFor<ContentDispositionHeaderValue>(input => new(ContentDispositionHeaderValue.TryParse(new((StringValues)input!), out var res), res));
        o.ValueParserFor<ContentRangeHeaderValue>(input => new(ContentRangeHeaderValue.TryParse(new((StringValues)input!), out var res), res));
        o.ValueParserFor<MediaTypeHeaderValue>(input => new(MediaTypeHeaderValue.TryParse(new((StringValues)input!), out var res), res));
        o.ValueParserFor<RangeConditionHeaderValue>(input => new(RangeConditionHeaderValue.TryParse(new((StringValues)input!), out var res), res));
        o.ValueParserFor<RangeHeaderValue>(input => new(RangeHeaderValue.TryParse(new((StringValues)input!), out var res), res));
        o.ValueParserFor<EntityTagHeaderValue>(input => new(EntityTagHeaderValue.TryParse(new((StringValues)input!), out var res), res));

        //list header parsers
        o.ValueParserFor<IList<MediaTypeHeaderValue>>(input => new(MediaTypeHeaderValue.TryParseList((StringValues)input!, out var res), res));
        o.ValueParserFor<IList<EntityTagHeaderValue>>(input => new(EntityTagHeaderValue.TryParseList((StringValues)input!, out var res), res));
        o.ValueParserFor<IList<SetCookieHeaderValue>>(input => new(SetCookieHeaderValue.TryParseList((StringValues)input!, out var res), res));

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