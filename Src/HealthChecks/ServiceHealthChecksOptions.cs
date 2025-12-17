using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// Configuration options for service health check endpoints.
/// </summary>
public sealed class ServiceHealthChecksOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the liveness probe endpoint is enabled.
    /// Default is true.
    /// </summary>
    public bool EnableLive { get; set; } = true;

    /// <summary>
    /// Gets or sets the path for the liveness probe endpoint.
    /// Default is "/health/live".
    /// </summary>
    /// <remarks>
    /// The liveness probe indicates whether the application process is running.
    /// It does not check any dependencies - if the process is alive, it returns 200 OK.
    /// </remarks>
    public PathString LivePath { get; set; } = "/health/live";

    /// <summary>
    /// Gets or sets a value indicating whether the readiness probe endpoint is enabled.
    /// Default is true.
    /// </summary>
    public bool EnableReady { get; set; } = true;

    /// <summary>
    /// Gets or sets the path for the readiness probe endpoint.
    /// Default is "/health/ready".
    /// </summary>
    /// <remarks>
    /// The readiness probe indicates whether the application is ready to receive traffic.
    /// It runs all registered health checks and returns the aggregate status.
    /// </remarks>
    public PathString ReadyPath { get; set; } = "/health/ready";

    /// <summary>
    /// Gets or sets a value indicating whether to use JSON format for health check responses.
    /// When true, responses include detailed status information in JSON format.
    /// When false, responses contain only a plain text status.
    /// Default is true.
    /// </summary>
    public bool UseJsonResponse { get; set; } = true;
}