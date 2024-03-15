global using Conf = FastEndpoints.Config;

namespace FastEndpoints.Swagger;

/// <summary>
/// gives access to the fastendpoints global configuration settings
/// </summary>
public static class GlobalConfig
{
    /// <summary>
    /// the prefix used in front of the version (for example 'v' produces 'v{version}').
    /// </summary>
    public static string? VersioningPrefix => Conf.VerOpts.Prefix;

    /// <summary>
    /// prefix for all routes (example 'api').
    /// </summary>
    public static string? EndpointRoutePrefix => Conf.EpOpts.RoutePrefix;

    /// <summary>
    /// Asp.Versioning.Http library is being used for versioning
    /// </summary>
    public static bool IsUsingAspVersioning => Conf.VerOpts.IsUsingAspVersioning;

    /// <summary>
    /// allows the use of empty request dtos
    /// </summary>
    public static bool AllowEmptyRequestDtos => Conf.EpOpts.AllowEmptyRequestDtos;
}