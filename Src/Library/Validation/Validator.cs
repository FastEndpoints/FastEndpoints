using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;
using ValidationResult = FluentValidation.Results.ValidationResult;

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

    protected override bool PreValidate(FluentValidation.ValidationContext<TRequest> context, ValidationResult result)
    {
        if (!Conf.ValOpts.EnableDataAnnotationsSupport)
            return true;

        var req = context.InstanceToValidate;
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        if (Validator.TryValidateObject(req, new(req), validationResults, true))
            return true;

        for (var i = 0; i < validationResults.Count; i++)
        {
            var res = validationResults[i];
            result.Errors.Add(new(res.MemberNames.FirstOrDefault(), res.ErrorMessage));
        }

        return true;
    }
}