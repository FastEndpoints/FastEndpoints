using FluentValidation;
using FluentValidation.Results;

namespace TestCases.PreProcessorIsRunOnValidationFailure
{
    public class MyPreProcessor : IPreProcessor<Request>
    {
        public Task PreProcessAsync(Request req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
        {
            var validationFailure = new ValidationFailure("x", "blah") {
                Severity = Severity.Warning,
                ErrorCode = "EC001"
            };
            
            failures.Add(validationFailure);
            
            return Task.CompletedTask;
        }
    }
}
