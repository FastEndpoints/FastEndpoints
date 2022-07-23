namespace TestCases.OnBeforeAfterValidationTest;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/test-cases/on-before-on-after-validate");
    }

    public override void OnBeforeValidate(Request req)
    {
        req.Verb = (Http)Enum.Parse(typeof(Http), HttpContext.Request.Method);
    }

    public override void OnAfterValidate(Request req)
    {
        req.Host = HttpContext.Request.Host.Value;
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        Response.Host = r.Host;
        return SendAsync(Response);
    }
}