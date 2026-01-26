using JetBrains.Annotations;

namespace FastEndpoints;

/// <summary>
/// a record encapsulating the http response as well as the resulting dto of a test execution
/// </summary>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
/// <param name="Response">http response message object</param>
/// <param name="Result">the resulting dto object. default when response is not successful.</param>
/// <param name="ErrorContent">the body content from error responses. null when response is successful.</param>
public sealed record TestResult<TResponse>(HttpResponseMessage Response,
                                           TResponse Result,
                                           [UsedImplicitly] string? ErrorContent = null)
{
    public void Deconstruct(out HttpResponseMessage response, out TResponse result)
    {
        response = Response;
        result = Result;
    }
}