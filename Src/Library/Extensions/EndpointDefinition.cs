namespace FastEndpoints;
#pragma warning disable CS8618

internal class EndpointDefinition
{
    public Type EndpointType { get; set; }
    public Type? ValidatorType { get; set; }
    public EndpointSettings Settings { get; set; }
}

