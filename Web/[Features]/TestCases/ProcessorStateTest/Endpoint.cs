namespace TestCases.ProcessorStateTest;

public class Endpoint : Endpoint<Request, string>
{
    public override void Configure()
    {
        Get("/test-cases/processor-state-sharing");
        AllowAnonymous();
        PreProcessors(
            new FirstPreProcessor(),
            new SecondProcessor());
        PostProcessors(new RequestDurationLogger());
    }

    public override async Task HandleAsync(Request r, CancellationToken c)
    {
        var state = ProcessorState<Thingy>();
        await Task.Delay(300);
        await SendAsync(state.Id + " " + state.Name);
    }
}