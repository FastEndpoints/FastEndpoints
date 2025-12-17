using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FastEndpoints;

sealed class ServiceHealthChecksStartupFilter(IOptions<ServiceHealthChecksOptions> opts) : IStartupFilter
{
    readonly ServiceHealthChecksOptions _opts = opts.Value;

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
           {
               if (_opts.EnableLive)
               {
                   // Liveness: no dependency checks - if the process is alive, return 200 OK
                   var liveOptions = new HealthCheckOptions { Predicate = _ => false };
                   if (_opts.UseJsonResponse)
                       liveOptions.ResponseWriter = WriteJson;

                   app.UseHealthChecks(_opts.LivePath, liveOptions);
               }

               if (_opts.EnableReady)
               {
                   // Readiness: run all registered health checks
                   var readyOptions = new HealthCheckOptions { Predicate = _ => true };
                   if (_opts.UseJsonResponse)
                       readyOptions.ResponseWriter = WriteJson;

                   app.UseHealthChecks(_opts.ReadyPath, readyOptions);
               }

               next(app);
           };

    static readonly JsonSerializerOptions _defaultSerializerOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    static Task WriteJson(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(
                e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    durationMs = e.Value.Duration.TotalMilliseconds,
                    exception = e.Value.Exception?.Message
                })
        };

        return ctx.Response.WriteAsync(
            JsonSerializer.Serialize(
                payload,
                ctx.RequestServices.GetService<IOptions<JsonOptions>>()?.Value.SerializerOptions ?? _defaultSerializerOpts));
    }
}