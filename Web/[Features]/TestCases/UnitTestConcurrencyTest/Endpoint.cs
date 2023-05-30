namespace TestCases.UnitTestConcurrencyTest;

public class Request
{
    public int Id { get; set; }
}

public class SingltonSVC
{
    private readonly int _id;

    public SingltonSVC(int id)
        => _id = id;

    public int GetId() => _id;
}

public class Endpoint : Endpoint<Request, int>
{
    private readonly int _id;

    public SingltonSVC Svc { get; set; }

    public Endpoint(SingltonSVC svc)
    {
        _id = svc.GetId();
    }

    public override void Configure()
    {
        Get("/test-cases/unit-test-concurrency");
        AllowAnonymous();
    }

    public override Task<int> ExecuteAsync(Request req, CancellationToken ct)
    {
        var id_resolve = Resolve<SingltonSVC>().GetId();
        var id_prop = Svc.GetId();
        var id_ctor = _id;

        if (id_resolve == id_prop && id_prop == id_ctor)
        {
            return Task.FromResult(id_prop);
        }

        return Task.FromResult(0);
    }
}