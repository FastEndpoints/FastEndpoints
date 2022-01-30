using FastEndpoints.Validation;

namespace FastEndpoints;

internal record EndpointMetadata(
    Func<object> InstanceCreator,
    IValidator? Validator,
    ServiceBoundReqDtoProp[]? ServiceBoundReqDtoProps,
    object? PreProcessors,
    object? PostProcessors,
    EpVersion Version,
    EndpointSummary? Summary);