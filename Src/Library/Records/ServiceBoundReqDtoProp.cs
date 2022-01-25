namespace FastEndpoints;

internal record ServiceBoundReqDtoProp(
    Type PropType,
    Action<object, object> PropSetter);