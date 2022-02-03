namespace FastEndpoints;

/// <summary>
/// represents an endpoint definition that is auto discovered by assembly scanning
/// </summary>
/// <param name="EndpointType">the type of the endpoint</param>
/// <param name="ValidatorType">the type of the validator used for the endpoint</param>
/// <param name="Settings">the configuration settings of the endpoint</param>
public record EndpointDefinition(
    Type EndpointType,
    Type? ValidatorType,
    EndpointSettings Settings);