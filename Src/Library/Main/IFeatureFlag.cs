namespace FastEndpoints;

/// <summary>
/// implement this interface for creating a feature flag for an endpoint.
/// </summary>
public interface IFeatureFlag
{
    /// <summary>
    /// optional name of the feature flag
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// return <c>false</c> from this method to disable the endpoint during runtime.
    /// </summary>
    /// <param name="endpoint">the endpoint instance</param>
    public Task<bool> IsEnabledAsync(IEndpoint endpoint);
}