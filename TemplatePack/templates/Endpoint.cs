using FastEndpoints;

namespace FeatureName;

#if (useAttributes)
[HttpPost("api/route/here")]
#endif
#if (useMapper)
public class Endpoint : Endpoint<Request, Response, Mapper>
#else
public class Endpoint : Endpoint<Request, Response>
#endif
{
#if (!useAttributes)
    public override void Configure()
    {
        Post("api/route/here");
    }
#endif

    public override Task HandleAsync(Request req, CancellationToken ct)
    {
        return SendOkAsync(Response, ct);
    }
}