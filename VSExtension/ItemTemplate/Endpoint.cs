namespace $fileinputname$;

public class Endpoint : Endpoint<Request, Response>
{
    public Endpoint()
    {
        Verbs(Http.GET);
        Routes("/route/path/here");
    }

    protected override Task HandleAsync(Request r, CancellationToken c)
    {
        return SendAsync(Response);
    }
}