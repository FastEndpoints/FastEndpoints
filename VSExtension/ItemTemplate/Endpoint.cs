namespace $fileinputname$;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/route/path/here");
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        return SendAsync(Response);
    }
}