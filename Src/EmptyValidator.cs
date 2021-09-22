using FluentValidation;

namespace EZEndpoints
{
    public class EmptyValidator<TRequest> : AbstractValidator<TRequest> where TRequest : IRequest { }
}
