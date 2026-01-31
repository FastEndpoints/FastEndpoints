using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

class ExceptionHandler;

/// <summary>
/// extensions for global exception handling
/// </summary>
[UnconditionalSuppressMessage("aot", "IL2026"), UnconditionalSuppressMessage("aot", "IL3050")]
public static class ExceptionHandlerExtensions
{
    /// <summary>
    /// registers the default global exception handler which will log the exceptions on the server and return a user-friendly json response to the client
    /// when unhandled exceptions occur.
    /// TIP: when using this exception handler, you may want to turn off the asp.net core exception middleware logging to avoid duplication like so:
    /// <code>
    /// "Logging": { "LogLevel": { "Default": "Warning", "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware": "None" } }
    /// </code>
    /// </summary>
    /// <param name="logger">an optional logger instance</param>
    /// <param name="logStructuredException">set to true if you'd like to log the error in a structured manner</param>
    /// <param name="useGenericReason">set to true if you don't want to expose the actual exception reason in the json response sent to the client</param>
    public static IApplicationBuilder UseDefaultExceptionHandler(this IApplicationBuilder app,
                                                                 ILogger? logger = null,
                                                                 bool logStructuredException = false,
                                                                 bool useGenericReason = false)
    {
        app.UseExceptionHandler(
            errApp =>
            {
                errApp.Run(
                    async ctx =>
                    {
                        var exHandlerFeature = ctx.Features.Get<IExceptionHandlerFeature>();

                        if (exHandlerFeature is not null)
                        {
                            logger ??= ctx.Resolve<ILogger<ExceptionHandler>>();
                            var route = exHandlerFeature.Endpoint?.DisplayName?.Split(" => ")[0];
                            var exceptionType = exHandlerFeature.Error.GetType().Name;
                            var reason = exHandlerFeature.Error.Message;

                            if (logStructuredException)
                                logger.LogStructuredException(exHandlerFeature.Error, exceptionType, route, reason);
                            else
                            {
                                //this branch is only meant for unstructured textual logging
                                logger.LogUnStructuredException(exceptionType, route, reason, exHandlerFeature.Error.StackTrace);
                            }

                            ctx.Response.StatusCode = 500;
                            await ctx.Response.WriteAsJsonAsync(
                                new InternalErrorResponse
                                {
                                    Status = "Internal Server Error!",
                                    Code = ctx.Response.StatusCode,
                                    Reason = useGenericReason ? "An unexpected error has occurred." : reason,
                                    Note = "See application log for stack trace."
                                },
                                Cfg.SerOpts.Options,
                                "application/problem+json",
                                ctx.RequestAborted);
                        }
                    });
            });

        return app;
    }
}