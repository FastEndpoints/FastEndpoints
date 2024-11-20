using FluentValidation;

namespace TestCases.IncludedValidatorTest;

abstract class BaseRequest
{
    public int Id { get; set; }

    internal class BaseValidator : Validator<BaseRequest>
    {
        public BaseValidator()
        {
            RuleFor(x => x.Id).NotEmpty().GreaterThan(5);
        }
    }
}

sealed class Request : BaseRequest
{
    public string Name { get; set; }

    internal class Validator : Validator<Request>
    {
        public Validator()
        {
            Include(new BaseValidator());
            RuleFor(x => x.Name).NotEmpty().MinimumLength(5);
        }
    }
}

sealed class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Post("/test-cases/included-validator");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request r, CancellationToken c)
    {
        await SendOkAsync();
    }
}