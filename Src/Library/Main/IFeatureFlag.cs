namespace FastEndpoints;

/// <summary>
/// implement this interface for creating a feature flag for an endpoint.
/// </summary>
public interface IFeatureFlag
{
    /// <summary>
    /// return <c>false</c> from this method to disable the endpoint during runtime.
    /// </summary>
    /// <param name="endpoint">the endpoint instance</param>
    public Task<bool> IsEnabledAsync(IEndpoint endpoint);
}