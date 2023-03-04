using FluentValidation.Results;

namespace TestCases.ProcessorStateTest;

public class FirstPreProcessor : PreProcessor<Request, Thingy>
{
    public override Task PreProcessAsync(Request req, Thingy state, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
    {
        state.Id = req.Id;
        state.Name = "john doe";
        return Task.CompletedTask;
    }
}
