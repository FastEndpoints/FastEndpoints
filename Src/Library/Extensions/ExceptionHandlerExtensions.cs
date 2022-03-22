using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;

namespace FastEndpoints;

internal class ExceptionHandler { }

/// <summary>
/// extensions for global exception handling
/// </summary>
public static class ExceptionHandlerExtensions
{
    /// <summary>
    /// registers the default global exception handler which will log the exceptions on the server and return a user-friendly json response to the client when unhandled exceptions occur.
    /// TIP: when using this exception handler, you may want to turn off the asp.net core exception middleware logging to avoid duplication like so:
    /// <code>
    /// "Logging": { "LogLevel": { "Default": "Warning", "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware": "None" } }
    /// </code>
    /// </summary>
    /// <param name="logger">an optional logger instance</param>
    public static void UseDefaultExceptionHandler(this IApplicationBuilder app, ILogger? logger = null)
    {
        app.UseExceptionHandler(errApp =>
        {
            errApp.Run(async ctx =>
            {
                var exHandlerFeature = ctx.Features.Get<IExceptionHandlerFeature>();
                if (exHandlerFeature is not null)
                {
                    var type = exHandlerFeature.Error.GetType().Name;
                    var error = exHandlerFeature.Error.Message;
                    var msg =
$@"=================================
{exHandlerFeature.Endpoint?.DisplayName?.Split(" => ")[0]}
TYPE: {type}
REASON: {error}
---------------------------------
{exHandlerFeature.Error.StackTrace}";

                    logger ??= ctx.RequestServices.GetRequiredService<ILogger<ExceptionHandler>>();
                    logger.LogError(msg);

                    ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    ctx.Response.ContentType = "application/problem+json";
                    await ctx.Response.WriteAsJsonAsync(new
                    {
                        Status = "Internal Server Error!",
                        Code = ctx.Response.StatusCode,
                        Reason = error,
                        Note = "See application log for stack trace."
                    });
                }
            });
        });
    }
}
