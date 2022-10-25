namespace FastEndpoints;

/// <summary>
/// interface for a command that does not return anything
/// </summary>
public interface ICommand { }

/// <summary>
/// interface for a command that returns a result
/// </summary>
/// <typeparam name="TResult">the type of the returned result</typeparam>
public interface ICommand<out TResult> { }