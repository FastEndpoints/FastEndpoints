namespace NativeAotChecker.Endpoints.SerializerCtxGen;

enum ChecklistControllerName
{
    Alpha,
    Beta
}

sealed class ChecklistForApprovalDto
{
    public string Name { get; set; }
}

sealed class DictionaryEnumKeyEndpoint : EndpointWithoutRequest<Dictionary<ChecklistControllerName, ChecklistForApprovalDto[]>>
{
    public override void Configure()
    {
        Get("ser-ctx-gen-dictionary-enum-key");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(
            new()
            {
                [ChecklistControllerName.Alpha] = [new() { Name = "alpha-item" }],
                [ChecklistControllerName.Beta] = [new() { Name = "beta-item" }]
            },
            ct);
}
