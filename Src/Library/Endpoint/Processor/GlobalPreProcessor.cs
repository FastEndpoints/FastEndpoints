namespace FastEndpoints;

/// <summary>
/// inherit this class to create a global pre-processor with access to the common processor state of the endpoint
/// </summary>
/// <typeparam name="TState">type of the common processor state</typeparam>
public abstract class GlobalPreProcessor<TState> : PreProcessor<object, TState>, IGlobalPreProcessor where TState : class, new()
{
}