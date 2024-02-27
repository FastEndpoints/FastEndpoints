using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using static FastEndpoints.Config;

namespace FastEndpoints;

static class BinderExtensions
{
    internal static string BareFieldName(this IFormFile file)
    {
        var indexOfOpeningBracket = file.Name.IndexOf('[');

        return indexOfOpeningBracket != -1 ? file.Name[..indexOfOpeningBracket] : file.Name;
    }

    internal static IEnumerable<PropertyInfo> BindableProps(this Type t)
    {
        return t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Where(
                    p => p.GetSetMethod()?.IsPublic is true &&
                         p.GetGetMethod()?.IsPublic is true &&
                         !p.IsDefined(Types.JsonIgnoreAttribute));
    }

    internal static Func<object, object> GetterForProp(this Type source, string propertyName)
    {
        //(object parent, object returnVal) => ((object)((TParent)parent).property);

        var parent = Expression.Parameter(Types.Object);
        var property = Expression.Property(Expression.Convert(parent, source), propertyName);
        var convertProp = Expression.Convert(property, Types.Object);

        return Expression.Lambda<Func<object, object>>(convertProp, parent).Compile();
    }

    internal static Action<object, object?> SetterForProp(this Type source, string propertyName)
    {
        //(object parent, object value) => ((TParent)parent).property = (TProp)value;

        var parent = Expression.Parameter(Types.Object);
        var value = Expression.Parameter(Types.Object);
        var property = Expression.Property(Expression.Convert(parent, source), propertyName);
        var body = Expression.Assign(property, Expression.Convert(value, property.Type));

        return Expression.Lambda<Action<object, object?>>(body, parent, value).Compile();
    }

    //static readonly ConstructorInfo _stringSegmentCtor = Types.StringSegment.GetConstructor([Types.String])!;
    internal static readonly ConcurrentDictionary<Type, Func<object?, ParseResult>> ParserFuncCache = new();
    static readonly MethodInfo _toStringMethod = Types.Object.GetMethod("ToString")!;
    static readonly ConstructorInfo _parseResultCtor = Types.ParseResult.GetConstructor([Types.Bool, Types.Object])!;

    internal static Func<object?, ParseResult> ValueParser(this Type type)
    {
        //we're only ever compiling a value parser for a given type once.
        //if a parser is requested for a type a second time, it will be returned from the dictionary instead of paying the compiling cost again.
        //the parser we return from here is then cached in RequestBinder PropCache entries avoiding the need to do repeated dictionary lookups here.
        //it is also possible that the user has already registered a parser func for a given type via config at startup.
        return ParserFuncCache.GetOrAdd(type, GetCompiledValueParser);

        static Func<object?, ParseResult> GetCompiledValueParser(Type tProp)
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
                tryParseMethod = tProp.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static, [Types.String, typeof(IFormatProvider), tProp.MakeByRefType()]);
                isIParseable = tryParseMethod is not null;
            }

            // var isTypedHeader = false;
            //
            // if (tryParseMethod is null && tProp.Name.EndsWith("HeaderValue")) //only applies to types from Microsoft.Net.Http.Headers due to tryparse having stringsegment
            // {
            //     tryParseMethod = tProp.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static, [Types.StringSegment, tProp.MakeByRefType()]);
            //     isTypedHeader = tryParseMethod is not null;
            // }

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

            // Expression? stringSegmentCreation = null;
            //
            // if (isTypedHeader)
            // {
            //     // 'new StringSegment(input == null ? (string)null : input.ToString())'
            //     stringSegmentCreation = Expression.New(_stringSegmentCtor, toStringConversion);
            // }

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
                new[] { resultVar, isSuccessVar },
                Expression.Assign(isSuccessVar, tryParseCall),
                Expression.New(_parseResultCtor, isSuccessVar, Expression.Convert(resultVar, Types.Object)));

            return Expression
                   .Lambda<Func<object?, ParseResult>>(block, inputParameter)
                   .Compile();

            static object? DeserializeJsonObjectString(object? input, Type tProp)
                => input is not StringValues { Count: 1 } vals
                       ? null
                       : vals[0]!.StartsWith('{') && vals[0]!.EndsWith('}') // check if it's a json object
                           ? JsonSerializer.Deserialize(vals[0]!, tProp, SerOpts.Options)
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

                    return JsonSerializer.Deserialize(vals[0]!, tProp, SerOpts.Options);
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

                var isEnumArray = false;
                if (tProp.IsArray)
                    isEnumArray = tProp.GetElementType()!.IsEnum;

                var sb = new StringBuilder("[");

                for (var i = 0; i < vals.Count; i++)
                {
                    if (isEnumArray || (vals[i]!.StartsWith('{') && vals[i]!.EndsWith('}')))
                        sb.Append(vals[i]);
                    else
                    {
                        sb.Append('"')
                          .Append(
                              vals[i]!.Contains('"') //json strings with quotations must be escaped
                                  ? vals[i]!.Replace("\"", "\\\"")
                                  : vals[i])
                          .Append('"');
                    }

                    if (i < vals.Count - 1)
                        sb.Append(',');
                }
                sb.Append(']');

                return JsonSerializer.Deserialize(sb.ToString(), tProp, SerOpts.Options);
            }
        }
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

                    if (pi.AttributeProvider?.GetCustomAttributes(Types.FromHeaderAttribute, true).Length != 0 && pi.PropertyType.Name.EndsWith("HeaderValue"))
                        ti.Properties.Remove(pi);
                }
            });
    }
#endif
}