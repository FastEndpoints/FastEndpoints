#pragma warning disable CA1822
using FluentValidation.Results;
using Microsoft.Extensions.Primitives;
using System.Text.Json;

namespace FastEndpoints;

/// <summary>
/// request binding options
/// </summary>
public sealed class BindingOptions
{
    /// <summary>
    /// a function used to construct the failure message when a supplied value cannot be succesfully bound to a dto property during model binding.
    /// <para>NOTE: this only applies to non-STJ operations. for customizing error messages of STJ binding failures, specify a <see cref="JsonExceptionTransformer"/> func.</para>
    /// the following arguments are supplied to the function.
    /// <para><see cref="Type"/>: the type of the property which failed to bind</para>
    /// <para><see cref="string"/>: the name of the property which failed to bind</para>
    /// <para><see cref="StringValues"/>: the value that was attempted which resulted in the failure</para>
    /// use these input parameters and construct your own error message string and return it from the function.
    /// </summary>
    public Func<Type, string, StringValues, string> FailureMessage { internal get; set; } = (tProp, propName, attemptedValue)
        => $"Value [{attemptedValue}] is not valid for a [{tProp.ActualTypeName()}] property!";

    /// <summary>
    /// by default, all STJ <see cref="JsonException"/>s thrown during deserialization are automatically caught and transformed using this function. 
    /// if you'd like to disable this behavior, simply set this property to <c>null</c> or specify a function to construct a
    /// <see cref="ValidationFailure"/> when STJ throws an exception due to invalid json input.
    /// <para>NOTE: this only applies to STJ based operations. for customizing error messages of non-STJ binding failures, specify a <see cref="FailureMessage"/> func.</para>
    /// </summary>
    public Func<JsonException, ValidationFailure>? JsonExceptionTransformer { internal get; set; } = (exception)
        => new ValidationFailure(
            propertyName: exception.Path != "$" ? exception.Path?[2..] : Config.SerOpts.SerializerErrorsField,
            errorMessage: exception.InnerException?.Message ?? exception.Message);

    /// <summary>
    /// an optional action to be run after the endpoint level request binding has occured.
    /// it is intended as a way to perform common model binding logic that applies to all endpoints/requests.
    /// the action is passed in the following arguments:
    /// <para><see cref="object"/>: the request dto instance</para>
    /// <para><see cref="Type"/>: the type of the request dto</para>
    /// <para><see cref="BinderContext"/>: the request binding context</para>
    /// <para><see cref="CancellationToken"/>: a cancellation token</para>
    /// <para>WARNING: be mindful of the performance cost of using reflection to modify the request dto object</para>
    /// </summary>
    public Action<object, Type, BinderContext, CancellationToken>? Modifier { internal get; set; }

    /// <summary>
    /// add a custom value parser function for any given type which the default model binder will use to parse values when model binding request dto properties from query/route/forms/headers/claims.
    /// this is an alternative approach to adding a `TryParse()` function to your types that need model binding support from the abovementioned binding sources.
    /// once you register a parser function here for a type, any `TryParse()` method on the type will not be used for parsing.
    /// also, these parser functions do not apply to JSON deserialization done by STJ and can be considered the equivalent to registering a custom converter in STJ when it comes to query/route/forms/headers/claims binding sources.
    /// </summary>
    /// <typeparam name="T">the type of the class which this parser function will target</typeparam>
    /// <param name="parser">a function that takes in a nullable object and returns a <see cref="ParseResult"/> as the output.
    /// <code>
    ///app.UseFastEndpoints(c =>
    ///{
    ///    c.Binding.ValueParserFor&lt;Guid&gt;(MyParsers.GuidParser);
    ///});
    ///
    ///public static class MyParsers
    ///{
    ///    public static ParseResult GuidParser(object? input)
    ///    {
    ///        Guid result;
    ///        bool success = Guid.TryParse(input?.ToString(), out result);
    ///        return new(success, result);
    ///    }
    ///}
    /// </code>
    /// </param>
    public bool ValueParserFor<T>(Func<object?, ParseResult> parser)
        => BinderExtensions.ParserFuncCache.TryAdd(typeof(T), parser);

    /// <summary>
    /// add a custom value parser function for any given type which the default model binder will use to parse values when model binding request dto properties from query/route/forms/headers/claims.
    /// this is an alternative approach to adding a `TryParse()` function to your types that need model binding support from the abovementioned binding sources.
    /// once you register a parser function here for a type, any `TryParse()` method on the type will not be used for parsing.
    /// also, these parser functions do not apply to JSON deserialization done by STJ and can be considered the equivalent to registering a custom converter in STJ when it comes to query/route/forms/headers/claims binding sources.
    /// </summary>
    /// <param name="type">the type of the class which this parser function will target</param>
    /// <param name="parser">a function that takes in a nullable object and returns a <see cref="ParseResult"/> as the output.
    /// <code>
    ///app.UseFastEndpoints(c =>
    ///{
    ///    c.Binding.ValueParserFor(typeof(Guid), MyParsers.GuidParser);
    ///});
    ///
    ///public static class MyParsers
    ///{
    ///    public static ParseResult GuidParser(object? input)
    ///    {
    ///        Guid result;
    ///        bool success = Guid.TryParse(input?.ToString(), out result);
    ///        return new(success, result);
    ///    }
    ///}
    /// </code>
    /// </param>
    public bool ValueParserFor(Type type, Func<object?, ParseResult> parser)
        => BinderExtensions.ParserFuncCache.TryAdd(type, parser);
}