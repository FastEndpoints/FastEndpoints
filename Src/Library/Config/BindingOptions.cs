#pragma warning disable CA1822
namespace FastEndpoints;

/// <summary>
/// request binding options
/// </summary>
public class BindingOptions
{
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
    public Action<object, Type, BinderContext, CancellationToken>? Modifier;

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
    public bool ValueParserFor<T>(Func<object?, ParseResult>? parser)
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
    public bool ValueParserFor(Type type, Func<object?, ParseResult>? parser)
        => BinderExtensions.ParserFuncCache.TryAdd(type, parser);
}