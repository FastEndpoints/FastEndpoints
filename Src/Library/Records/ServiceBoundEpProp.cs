namespace FastEndpoints;

internal record ServiceBoundEpProp(
    Type PropType,
    Action<object, object> PropSetter);