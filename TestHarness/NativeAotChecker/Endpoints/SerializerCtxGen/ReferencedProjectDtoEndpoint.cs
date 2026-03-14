using Contracts.Dtos;

namespace NativeAotChecker.Endpoints.SerializerCtxGen;

sealed class ReferencedProjectDtoEndpoint : Endpoint<MyDto, MyDto>
{
    public override void Configure()
    {
        Post("ser-ctx-gen-referenced-project-dto");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MyDto req, CancellationToken ct)
    {
        await Send.OkAsync(
            new()
            {
                Name = $"echo:{req.Name}",
                Age = req.Age + 1,
                Note = $"resolved:{req.Note}"
            },
            ct);
    }
}
