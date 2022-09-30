namespace FastEndpoints;

/// <summary>
/// interface for defining global pre-processors to be executed before the main endpoint handler is called
/// </summary>
public interface IGlobalPreProcessor : IPreProcessor<object> { }
