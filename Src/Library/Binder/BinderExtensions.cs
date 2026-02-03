using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace FastEndpoints;

[UnconditionalSuppressMessage("aot", "IL2070"), UnconditionalSuppressMessage("aot", "IL2111"), UnconditionalSuppressMessage("aot", "IL3050"),
 UnconditionalSuppressMessage("aot", "IL2080"), UnconditionalSuppressMessage("aot", "IL2067"), UnconditionalSuppressMessage("aot", "IL2026"),
 UnconditionalSuppressMessage("aot", "IL2075")]
static class BinderExtensions
{
    internal static string BareFieldName(this IFormFile file)
    {
        var indexOfOpeningBracket = file.Name.IndexOf('[');

        return indexOfOpeningBracket != -1 ? file.Name[..indexOfOpeningBracket] : file.Name;
    }

    static readonly Func<object> _emptyRequestInitializer = () => EmptyRequest.Instance;
    static readonly ConstructorInfo _parseResultCtor = Types.ParseResult.GetConstructor([Types.Bool, Types.Object])!;

    extension(Type type)
    {
        internal Func<object> ObjectFactory()
        {
            if (type == Types.EmptyRequest)
                return _emptyRequestInitializer;

            return Cfg.BndOpts.ReflectionCache.GetOrAdd(type, new TypeDefinition())
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
                        args[i].HasDefaultValue && args[i].DefaultValue is not null
                            ? Expression.Constant(args[i].DefaultValue, args[i].ParameterType)
                            : Expression.Default(args[i].ParameterType));
                }
                var ctorExpression = Expression.New(ctor, argExpressions);

                return Expression.Lambda<Func<object>>(ctorExpression).Compile();
            }
        }

        internal Action<object, object?> SetterForProp(PropertyInfo prop)
        {
            if (!Cfg.BndOpts.ReflectionCache.TryGetValue(type, out var classDef))
                throw new InvalidOperationException($"Reflection data not found for: [{type.FullName}]");

            if (classDef.Properties is null)
                throw new InvalidOperationException($"Reflection data not found for properties of: [{type.FullName}]");

            return classDef.Properties.GetOrAdd(prop, new PropertyDefinition())
                           .Setter ??= CompileSetter(type, prop);

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

        internal Func<StringValues, ParseResult> ValueParser()
        {
            //user may have already registered a parser func for a given type via config at startup.
            //or reflection source generator may have already populated the cache.
            //if it's not there, compile the func at runtime.
            return Cfg.BndOpts.ReflectionCache.GetOrAdd(type.GetUnderlyingType(), new TypeDefinition()).ValueParser
                       ??= GetOrCompileParser(type);

            static Func<StringValues, ParseResult> GetOrCompileParser(Type type)
            {
                type = type.GetUnderlyingType();

                if (type == Types.String || type == Types.StringSegment)
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

                    return interfaces.Contains(Types.IEnumerable) && !interfaces.Contains(Types.IDictionary) //dictionaries should be deserialized as json objects
                               ? (type.GetElementType() ?? type.GetGenericArguments().FirstOrDefault()) == Types.Byte
                                     ? input => new(TryParseByteArray(input, out var res), res)
                                     : input => new(TryParseCollection(input, type, out var res), res)
                               : input => new(TryParseObject(input, type, out var res), res);
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

        internal ICollection<PropertyInfo> BindableProps()
        {
            return (Cfg.BndOpts.ReflectionCache.GetOrAdd(type, new TypeDefinition())
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
    }

    static bool TryParseObject(StringValues input, Type tProp, out object? result)
    {
        if (input.Count == 0 || !input[0].IsJsonObjectString())
        {
            result = null;

            return false;
        }

        result = JsonSerializer.Deserialize(input[0]!, tProp, Cfg.SerOpts.Options);

        return result is not null;
    }

    static bool TryParseByteArray(StringValues input, out object? result)
    {
        if (input.Count == 0)
        {
            result = null;

            return false;
        }

        try
        {
            result = Convert.FromBase64String(input[0]!);

            return true;
        }
        catch (FormatException)
        {
            result = null;

            return false;
        }
    }

    static bool TryParseCollection(StringValues input, Type tProp, out object? result)
    {
        switch (input.Count)
        {
            case 0:

                result = null;

                return false;

            case 1 when input[0].IsJsonArrayString():

                // querystring: ?ids=[1,2,3]
                // possible inputs:
                // - [1,2,3] (as StringValues[0])
                // - ["one","two","three"] (as StringValues[0])
                // - [{"name":"x"},{"name":"y"}] (as StringValues[0])

                result = JsonSerializer.Deserialize(input[0]!, tProp, Cfg.SerOpts.Options);

                return result is not null;

            case 1 when input[0].IsMalformedJsonArrayString(out var json):

                //swagger ui likes to send {...},{...} without enclosing in [...]

                result = JsonSerializer.Deserialize(json, tProp, Cfg.SerOpts.Options);

                return result is not null;

            case 1 when input[0].IsCsvString():
                input = input[0]!.Split(','); //csv input support is undocumented

                break;
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

        result = JsonSerializer.Deserialize(sb.ToString(), tProp, Cfg.SerOpts.Options);

        return result is not null;
    }

    extension(string? input)
    {
        bool IsCsvString()
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var len = input.Length;

            if (input[0] == ',' || input[len - 1] == ',')
                return false;

            var sawComma = false;

            for (var i = 1; i < len; i++)
            {
                if (input[i] != ',')
                    continue;

                if (input[i - 1] == ',')
                    return false;

                sawComma = true;
            }

            return sawComma;
        }

        // ReSharper disable once OutParameterValueIsAlwaysDiscarded.Local
        bool IsMalformedJsonArrayString(out string json)
        {
            json = null!;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            var index = 0;

            while (index < input.Length && char.IsWhiteSpace(input[index]))
                index++;

            if (index >= input.Length || input[index] != '{')
                return false;

            var inString = false;
            var escapeNext = false;
            var braceCount = 1;

            for (var i = index + 1; i < input.Length; i++)
            {
                if (escapeNext)
                {
                    escapeNext = false;

                    continue;
                }

                var c = input[i];

                switch (c)
                {
                    case '\\':
                        escapeNext = true;

                        break;
                    case '"':
                        inString = !inString;

                        break;
                    default:
                    {
                        if (!inString)
                        {
                            switch (c)
                            {
                                case '{':
                                    braceCount++;

                                    break;
                                case '}':
                                {
                                    braceCount--;

                                    if (braceCount == 0)
                                    {
                                        json = $"[{input}]";

                                        return true;
                                    }

                                    break;
                                }
                            }
                        }

                        break;
                    }
                }
            }

            return false;
        }

        bool IsJsonArrayString()
            => input?.Length > 1 && input[0] == '[' && input[^1] == ']';

        bool IsJsonObjectString()
            => input?.Length > 1 && input[0] == '{' && input[^1] == '}';
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

    internal static void AddTypedHeaderValueParsers(this BindingOptions o, JsonSerializerOptions jso)
    {
        //header parsers
        o.ValueParserFor<CacheControlHeaderValue>(input => new(CacheControlHeaderValue.TryParse(new(input), out var res), res));
        o.ValueParserFor<ContentDispositionHeaderValue>(input => new(ContentDispositionHeaderValue.TryParse(new(input), out var res), res));
        o.ValueParserFor<ContentRangeHeaderValue>(input => new(ContentRangeHeaderValue.TryParse(new(input), out var res), res));
        o.ValueParserFor<MediaTypeHeaderValue>(input => new(MediaTypeHeaderValue.TryParse(new(input), out var res), res));
        o.ValueParserFor<RangeConditionHeaderValue>(input => new(RangeConditionHeaderValue.TryParse(new(input), out var res), res));
        o.ValueParserFor<RangeHeaderValue>(input => new(RangeHeaderValue.TryParse(new(input), out var res), res));
        o.ValueParserFor<EntityTagHeaderValue>(input => new(EntityTagHeaderValue.TryParse(new(input), out var res), res));

        //list header parsers
        o.ValueParserFor<IList<MediaTypeHeaderValue>>(input => new(MediaTypeHeaderValue.TryParseList(input, out var res), res));
        o.ValueParserFor<IList<EntityTagHeaderValue>>(input => new(EntityTagHeaderValue.TryParseList(input, out var res), res));
        o.ValueParserFor<IList<SetCookieHeaderValue>>(input => new(SetCookieHeaderValue.TryParseList(input, out var res), res));

        //need to prevent STJ from trying to deserialize these types
        jso.TypeInfoResolver = jso.TypeInfoResolver?.WithAddedModifier(
            ti =>
            {
                if (ti.Kind != JsonTypeInfoKind.Object)
                    return;

                for (var i = ti.Properties.Count - 1; i >= 0; i--)
                {
                    var pi = ti.Properties[i];

                    if (pi.AttributeProvider?.IsDefined(Types.FromHeaderAttribute, true) is true && pi.PropertyType.Name.EndsWith("HeaderValue"))
                        ti.Properties.RemoveAt(i);
                }
            });
    }
}