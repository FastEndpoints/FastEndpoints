using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

public static class HttpContextExtensions
{
    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    public static TService? TryResolve<TService>(this HttpContext ctx) where TService : class
        => ctx.RequestServices.GetService<TService>();

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    public static object? TryResolve(this HttpContext ctx, Type typeOfService)
        => ctx.RequestServices.GetService(typeOfService);

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public static TService Resolve<TService>(this HttpContext ctx) where TService : class
        => ctx.RequestServices.GetRequiredService<TService>();

    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public static object Resolve(this HttpContext ctx, Type typeOfService)
        => ctx.RequestServices.GetRequiredService(typeOfService);

    /// <summary>
    /// marks the current response as started so that <see cref="ResponseStarted(HttpContext)"/> can return the correct result.
    /// </summary>
    /// <param name="ctx"></param>
    public static void MarkResponseStart(this HttpContext ctx)
        => ctx.Items[0] = null; //item must match below

    /// <summary>
    /// check if the current response has already started or not.
    /// </summary>
    public static bool ResponseStarted(this HttpContext ctx)
        => ctx.Response.HasStarted || ctx.Items.ContainsKey(0); //item must match above
}