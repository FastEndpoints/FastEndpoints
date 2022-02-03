using FastEndpoints.Validation;

namespace FastEndpoints;

internal record EndpointMetadata(
    Func<object> InstanceCreator,
    IValidator? Validator,
    ServiceBoundEpProp[]? ServiceBoundEpProps,
    EndpointSettings EndpointSettings);