namespace FastEndpoints.Extensions;

public static class RequestExtensions
{
    public static Task<TResult> ExecuteAsync<TResult>(this ICommand<TResult> commandModel, CancellationToken cancellation = default)
    {
        var requestType = typeof(Command<,>);
        Type[] typeArgs = { commandModel.GetType(), typeof(TResult) };

        var requestGenericType = requestType.MakeGenericType(typeArgs);
        var request = Activator.CreateInstance(requestGenericType);
        if (request == null)
            throw new Exception($"Couldn't create an instance of the command '{requestGenericType.Name}'!.");

        var sendMethod = request.GetType().GetMethod("ExecuteAsync")!;
        return (Task<TResult>)sendMethod.Invoke(request, new object[] { commandModel, cancellation })!;
    }


    public static Task ExecuteAsync(this ICommand commandModel, CancellationToken cancellation = default)
    {
        var requestType = typeof(Command<>);
        Type[] typeArgs = { commandModel.GetType()};

        var requestGenericType = requestType.MakeGenericType(typeArgs);
        var request = Activator.CreateInstance(requestGenericType);
        if (request == null)
            throw new Exception($"Couldn't create an instance of the command '{requestGenericType.Name}'!.");

        var sendMethod = request.GetType().GetMethod("ExecuteAsync")!;
        return (Task)sendMethod.Invoke(request, new object[] { commandModel, cancellation })!;
    }
}
