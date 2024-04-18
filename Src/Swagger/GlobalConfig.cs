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

    /// <summary>
    /// this route constraint type map will be used to determine the type for a route parameter if there's no matching property on the request dto.
    /// the dictionary key is the name of the constraint and the value is the  corresponding <see cref="System.Type" />
    /// </summary>
    public static Dictionary<string, Type> RouteConstraintMap { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        { "int", typeof(int) },
        { "bool", typeof(bool) },
        { "datetime", typeof(DateTime) },
        { "decimal", typeof(decimal) },
        { "double", typeof(double) },
        { "float", typeof(float) },
        { "guid", typeof(Guid) },
        { "long", typeof(long) },
        { "min", typeof(long) },
        { "max", typeof(long) },
        { "range", typeof(long) }
    };
}