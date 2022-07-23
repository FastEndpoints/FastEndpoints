using FluentValidation;

namespace TestCases.PreProcessorIsRunOnValidationFailure;

public class Request
{
    public string? FirstName { get; set; }
    public int FailureCount { get; set; }
}

public class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("not empty!")
            .MinimumLength(5).WithMessage("too short!");
    }
}

public class Response
{
    public string Message => "you should not be seeing this message!";
}