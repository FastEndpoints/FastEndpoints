namespace FastEndpoints;

/// <summary>
/// a record encapsulating the http response as well as the resulting dto of a test execution
/// </summary>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
/// <param name="Response">http response message object</param>
/// <param name="Result">the resulting dto object</param>
public sealed record TestResult<TResponse>(HttpResponseMessage Response, TResponse? Result);