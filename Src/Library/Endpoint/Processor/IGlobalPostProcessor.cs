namespace FastEndpoints;

/// <summary>
/// interface for defining global post-processors to be executed after the main endpoint handler is done
/// </summary>
public interface IGlobalPostProcessor : IPostProcessor<object, object?> { }
