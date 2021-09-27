using FastEndpoints.Validation;

namespace FastEndpoints
{
    /// <summary>
    /// a validator that doesn't do anything
    /// </summary>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    public class EmptyValidator<TRequest> : Validator<TRequest> { }
}
