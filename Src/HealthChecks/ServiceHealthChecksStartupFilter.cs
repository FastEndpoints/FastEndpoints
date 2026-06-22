using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

    static Task WriteJson(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json; charset=utf-8";

        var payload = new HealthCheckResponse
        {
            Status = report.Status.ToString(),
            TotalDurationMs = report.TotalDuration.TotalMilliseconds,
            Checks = report.Entries.Select(
                e => new HealthCheckEntryResponse
                {
                    Name = e.Key,
                    Status = e.Value.Status.ToString(),
                    Description = e.Value.Description,
                    DurationMs = e.Value.Duration.TotalMilliseconds,
                    Exception = e.Value.Exception?.Message
                }).ToArray()
        };

        return ctx.Response.WriteAsync(JsonSerializer.Serialize(payload, HealthChecksJsonContext.Default.HealthCheckResponse));
    }
}

sealed class HealthCheckResponse
{
    public string Status { get; set; } = null!;
    public double TotalDurationMs { get; set; }
    public HealthCheckEntryResponse[] Checks { get; set; } = [];
}

sealed class HealthCheckEntryResponse
{
    public string Name { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? Description { get; set; }
    public double DurationMs { get; set; }
    public string? Exception { get; set; }
}

[JsonSerializable(typeof(HealthCheckResponse)), JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
sealed partial class HealthChecksJsonContext : JsonSerializerContext;