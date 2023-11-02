namespace FastEndpoints;

/// <summary>
/// inherit this class to create a global post-processor with access to the common processor state of the endpoint
/// </summary>
/// <typeparam name="TState">type of the common processor state</typeparam>
public abstract class GlobalPostProcessor<TState> : PostProcessor<object, TState, object>, IGlobalPostProcessor where TState : class, new()
{
}