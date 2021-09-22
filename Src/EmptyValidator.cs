using FluentValidation;

namespace ApiExpress
{
    public class EmptyValidator<TRequest> : AbstractValidator<TRequest> where TRequest : IRequest { }
}
