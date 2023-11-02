using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace FastEndpoints;

class ExceptionHandler { }

/// <summary>
/// extensions for global exception handling
/// </summary>
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
                            var http = exHandlerFeature.Endpoint?.DisplayName?.Split(" => ")[0];
                            var type = exHandlerFeature.Error.GetType().Name;
                            var reason = exHandlerFeature.Error.Message;
                            var msg =
                                $"""
                                 =================================
                                 {http}
                                 TYPE: {type}
                                 REASON: {reason}
                                 ---------------------------------
                                 {exHandlerFeature.Error.StackTrace}
                                 """;
                            if (logStructuredException)
                                logger.LogError("{@http}{@type}{@reason}{@exception}", http, type, reason, exHandlerFeature.Error);
                            else
                                logger.LogError(msg);

                            ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            ctx.Response.ContentType = "application/problem+json";
                            await ctx.Response.WriteAsJsonAsync(
                                new InternalErrorResponse
                                {
                                    Status = "Internal Server Error!",
                                    Code = ctx.Response.StatusCode,
                                    Reason = useGenericReason ? "Something unexpected has happened" : reason,
                                    Note = "See application log for stack trace."
                                });
                        }
                    });
            });

        return app;
    }
}