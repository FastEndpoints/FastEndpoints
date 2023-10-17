using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using MSDA = System.ComponentModel.DataAnnotations;

namespace FastEndpoints;

/// <summary>
/// inherit from this base class to define your dto validators
/// <para>
/// HINT: validators are registered as singletons. i.e. the same validator instance is used to validate each request for best performance. hance,
/// do not maintain state in your validators.
/// </para>
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
public abstract class Validator<TRequest> : AbstractValidator<TRequest>, IServiceResolverBase, IEndpointValidator where TRequest : class
{
    /// <inheritdoc />
    public TService? TryResolve<TService>() where TService : class
        => Conf.ServiceResolver.TryResolve<TService>();

    /// <inheritdoc />
    public object? TryResolve(Type typeOfService)
        => Conf.ServiceResolver.TryResolve(typeOfService);

    /// <inheritdoc />
    public TService Resolve<TService>() where TService : class
        => Conf.ServiceResolver.Resolve<TService>();

    /// <inheritdoc />
    public object Resolve(Type typeOfService)
        => Conf.ServiceResolver.Resolve(typeOfService);

    /// <inheritdoc />
    public IServiceScope CreateScope()
        => Conf.ServiceResolver.CreateScope();

    /// <summary>
    /// PreValidate compliant DataAnnotation
    /// </summary>
    /// <param name="context"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    protected override bool PreValidate(FluentValidation.ValidationContext<TRequest> context, ValidationResult result)
    {
        var req = context.InstanceToValidate;

        //support DataAnnotation
        var mc = new MSDA.ValidationContext(req);
        var validationResults = new List<MSDA.ValidationResult>();
        var flag = MSDA.Validator.TryValidateObject(req, mc, validationResults, true);
        if (!flag)
        {
            result.Errors.AddRange(validationResults.Select(x => new ValidationFailure(x.MemberNames.FirstOrDefault(), x.ErrorMessage)));
        }
        return base.PreValidate(context, result);
    }

}