using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using FluentValidation.Results;
using Microsoft.Extensions.Primitives;

// ReSharper disable MemberCanBeMadeStatic.Global

namespace FastEndpoints;

/// <summary>
/// request binding options
/// </summary>
[SuppressMessage("Performance", "CA1822:Mark members as static")]
public sealed class BindingOptions
{
    /// <summary>
    /// specify whether to use the json property naming policy when matching incoming field names to dto property names for non-json model binding.
    /// only applies when field names are not specified on properties with attributes such as [BindFrom(...)], [FromClaim(...)], [FromHeader(...)] etc.
    /// </summary>
    public bool UsePropertyNamingPolicy { get; set; } = false;

    /// <summary>
    /// by default, if a dto property is nullable and an incoming parameter value is omitted while only the parameter name exists, the default value for the property
    /// will be populated. setting <c>false</c> will prevent that from happening. only applies to non-STJ binding paths such as when binding from
    /// route/query/claims/headers/form fields etc.
    /// </summary>
    public bool UseDefaultValuesForNullableProps { get; set; } = true;

    /// <summary>
    /// the central cache of request dto related reflection data.
    /// populating this cache with source generated data will eliminate expression compilations during runtime as well as usage of
    /// reflection based property setters, etc. see the source generator documentation on how to populate this cache with generated data.
    /// </summary>
    public ReflectionCache ReflectionCache { get; } = new();

    /// <summary>
    /// a function used to construct the failure message when a supplied value cannot be successfully bound to a dto property during model binding.
    /// <para>
    /// NOTE: this only applies to non-STJ operations. for customizing error messages of STJ binding failures, specify a
    /// <see cref="JsonExceptionTransformer" /> func.
    /// </para>
    /// the following arguments are supplied to the function.
    /// <para><see cref="Type" />: the type of the property which failed to bind</para>
    /// <para><see cref="string" />: the name of the property which failed to bind</para>
    /// <para><see cref="StringValues" />: the value that was attempted which resulted in the failure</para>
    /// use these input parameters and construct your own error message string and return it from the function.
    /// </summary>
    public Func<Type, string, StringValues, string> FailureMessage { internal get; set; }
        = (tProp, _, attemptedValue)
              => $"Value [{attemptedValue}] is not valid for a [{tProp.ActualTypeName()}] property!";

    /// <summary>
    /// by default, all STJ <see cref="JsonException" />s thrown during deserialization are automatically caught and transformed using this function.
    /// if you'd like to disable this behavior, simply set this property to <c>null</c> or specify a function to construct a
    /// <see cref="ValidationFailure" /> when STJ throws an exception due to invalid json input.
    /// <para>
    /// NOTE: this only applies to STJ based operations. for customizing error messages of non-STJ binding failures, specify a
    /// <see cref="FailureMessage" /> func.
    /// </para>
    /// </summary>
    public Func<JsonException, ValidationFailure>? JsonExceptionTransformer { internal get; set; }
        = ex =>
          {
              var bindEx = ex as JsonBindException;

              return new(
                  propertyName: ex.Path is null or "$" || ex.Path.StartsWith("$[")
                                    ? bindEx?.FieldName ?? Cfg.SerOpts.SerializerErrorsField
                                    : ex.Path[2..],
                  errorMessage: bindEx?.FailureMessage ?? ex.InnerException?.Message ?? ex.Message);
          };

    /// <summary>
    /// this http status code will be used for all automatically sent <see cref="JsonException" /> responses  which are built using the
    /// <see cref="JsonExceptionTransformer" />
    /// func. defaults to 400.
    /// </summary>
    public int JsonExceptionStatusCode { internal get; set; } = 400;

    /// <summary>
    /// if this function is specified, any internal exceptions that are thrown by asp.net when accessing multipart form data will be caught and transformed to
    /// validation
    /// failures using this function. by default those exceptions are not caught and thrown out to the middleware pipeline. setting this func might come in handy
    /// if
    /// you need 413 responses (that arise from incoming request body size exceeding kestrel's <c>MaxRequestBodySize</c>) automatically transformed to 400 problem
    /// details
    /// responses.
    /// </summary>
    public Func<Exception, ValidationFailure>? FormExceptionTransformer { internal get; set; }

    /// <summary>
    /// an optional action to be run after the endpoint level request binding has occured.
    /// it is intended as a way to perform common model binding logic that applies to all endpoints/requests.
    /// the action is passed in the following arguments:
    /// <para><see cref="object" />: the request dto instance</para>
    /// <para><see cref="Type" />: the type of the request dto</para>
    /// <para><see cref="BinderContext" />: the request binding context</para>
    /// <para><see cref="CancellationToken" />: a cancellation token</para>
    /// <para>WARNING: be mindful of the performance cost of using reflection to modify the request dto object</para>
    /// </summary>
    public Action<object, Type, BinderContext, CancellationToken>? Modifier { internal get; set; }

    /// <summary>
    /// add a custom value parser function for any given type which the default model binder will use to parse values when model binding request dto
    /// properties from query/route/forms/headers/claims.
    /// this is an alternative approach to adding a `TryParse()` function to your types that need model binding support from the abovementioned binding
    /// sources.
    /// once you register a parser function here for a type, any `TryParse()` method on the type will not be used for parsing.
    /// also, these parser functions do not apply to JSON deserialization done by STJ and can be considered the equivalent to registering a custom converter
    /// in STJ when it comes to query/route/forms/headers/claims binding sources.
    /// </summary>
    /// <typeparam name="T">the type of the class which this parser function will target</typeparam>
    /// <param name="parser">
    /// a function that takes in a nullable object and returns a <see cref="ParseResult" /> as the output.
    /// <code>
    /// app.UseFastEndpoints(c =>
    /// {
    ///     c.Binding.ValueParserFor&lt;Guid&gt;(MyParsers.GuidParser);
    /// });
    ///
    /// public static class MyParsers
    /// {
    ///     public static ParseResult GuidParser(object? input)
    ///     {
    ///         Guid result;
    ///         bool success = Guid.TryParse(input?.ToString(), out result);
    ///         return new(success, result);
    ///     }
    /// }
    ///  </code>
    /// </param>
    public void ValueParserFor<T>(Func<StringValues, ParseResult> parser)
        => Cfg.BndOpts.ReflectionCache.GetOrAdd(typeof(T), new TypeDefinition()).ValueParser = parser;

    /// <summary>
    /// add a custom value parser function for any given type which the default model binder will use to parse values when model binding request dto
    /// properties from query/route/forms/headers/claims.
    /// this is an alternative approach to adding a `TryParse()` function to your types that need model binding support from the abovementioned binding
    /// sources.
    /// once you register a parser function here for a type, any `TryParse()` method on the type will not be used for parsing.
    /// also, these parser functions do not apply to JSON deserialization done by STJ and can be considered the equivalent to registering a custom converter
    /// in STJ when it comes to query/route/forms/headers/claims binding sources.
    /// </summary>
    /// <param name="type">the type of the class which this parser function will target</param>
    /// <param name="parser">
    /// a function that takes in a nullable object and returns a <see cref="ParseResult" /> as the output.
    /// <code>
    /// app.UseFastEndpoints(c =>
    /// {
    ///     c.Binding.ValueParserFor(typeof(Guid), MyParsers.GuidParser);
    /// });
    ///
    /// public static class MyParsers
    /// {
    ///     public static ParseResult GuidParser(object? input)
    ///     {
    ///         Guid result;
    ///         bool success = Guid.TryParse(input?.ToString(), out result);
    ///         return new(success, result);
    ///     }
    /// }
    ///  </code>
    /// </param>
    public void ValueParserFor(Type type, Func<StringValues, ParseResult> parser)
        => Cfg.BndOpts.ReflectionCache.GetOrAdd(type, new TypeDefinition()).ValueParser = parser;

    /// <summary>
    /// override value parsers for request dto properties that match a predicate.
    /// <para>
    /// WARNING: might lead to weird/untraceable behavior. use at own risk!
    /// </para>
    /// </summary>
    /// <param name="propertyMatcher">a predicate for qualifying a property</param>
    /// <param name="parser">the value parser for the matched property type</param>
    public void ValueParserWhen(Func<PropertyInfo, bool> propertyMatcher, Func<object?, Type, ParseResult> parser)
        => PropertyMatchers.Add(new(propertyMatcher, parser));

    internal static readonly List<(Func<PropertyInfo, bool> matcher, Func<object?, Type, ParseResult> parser)> PropertyMatchers = [];
}