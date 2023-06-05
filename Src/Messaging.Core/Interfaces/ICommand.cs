namespace FastEndpoints;

/// <summary>
/// interface for a command that does not return anything
/// </summary>
public interface ICommand { }

/// <summary>
/// interface for a command that returns a result
/// </summary>
/// <typeparam name="TResult">the type of the result that will be returned from the handler [<see cref="ICommand{TResult}"/>] of this command</typeparam>
public interface ICommand<out TResult> { }