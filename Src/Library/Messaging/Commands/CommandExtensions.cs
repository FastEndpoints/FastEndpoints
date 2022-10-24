namespace FastEndpoints.Extensions;

public static class CommandExtensions
{
    public static Task<TResult> ExecuteAsync<TResult>(this ICommand<TResult> commandModel, CancellationToken cancellation = default)
    {
        //TODO: need to optimize these as reflection use per execution is bad for performance
        //      specially Activator.CreateInstance() and MethodInfo.Invoke() are known to be slow

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
        Type[] typeArgs = { commandModel.GetType() };

        var requestGenericType = requestType.MakeGenericType(typeArgs);
        var request = Activator.CreateInstance(requestGenericType);
        if (request == null)
            throw new Exception($"Couldn't create an instance of the command '{requestGenericType.Name}'!.");

        var sendMethod = request.GetType().GetMethod("ExecuteAsync")!;
        return (Task)sendMethod.Invoke(request, new object[] { commandModel, cancellation })!;
    }
}