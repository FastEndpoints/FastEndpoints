using FluentValidation;

namespace TestCases.OnBeforeAfterValidationTest;

public class Request
{
    public Http Verb { get; set; }
    public string? Host { get; set; }
}

public class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Verb).Equal(Http.POST).WithMessage("has to be post method!");
    }
}

public class Response
{
    public string? Host { get; set; }
}