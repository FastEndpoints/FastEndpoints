using FluentValidation;

namespace FastEndpoints
{
    public class EmptyValidator<TRequest> : AbstractValidator<TRequest> where TRequest : IRequest { }
}
