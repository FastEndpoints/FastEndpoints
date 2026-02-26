namespace NativeAotChecker.Endpoints.Validation;

public class ErrorWithPropertyExpressionRequest
{
    public string[] Items { get; set; }
}

public class ErrorWithPropertyExpressionEndpoint : Endpoint<ErrorWithPropertyExpressionRequest>
{
    public override void Configure()
    {
        Post("error-with-prop-expression");
        AllowAnonymous();
    }

    public override Task HandleAsync(ErrorWithPropertyExpressionRequest req, CancellationToken ct)
    {
        //NOTE: we're checking PropertyChainExtensions.cs -> GetValueCompiled()

        // ReSharper disable once SuggestVarOrType_BuiltInTypes
        // ReSharper disable once ConvertToConstant.Local
        int idx = 0;
        ThrowError(r => r.Items[idx + 1], "this is a error message!");

        return Task.CompletedTask;
    }
}