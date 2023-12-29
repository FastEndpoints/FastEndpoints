namespace FastEndpoints;

interface IProcessorAttribute
{
    void AddToEndpointDefinition(EndpointDefinition def);
}

/// <summary>
/// generic attribute for adding a pre-processor to an endpoint. only effective when attribute based endpoint configuration is being used.
/// </summary>
/// <typeparam name="TPreProcessor">the type of the pre-processor</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class PreProcessorAttribute<TPreProcessor> : Attribute, IProcessorAttribute where TPreProcessor : class, IPreProcessor
{
    static int _unused;

    void IProcessorAttribute.AddToEndpointDefinition(EndpointDefinition def)
    {
        EndpointDefinition.AddProcessor<TPreProcessor>(Order.After, def.PreProcessorList, ref _unused);
    }
}

/// <summary>
/// generic attribute for adding a post-processor to an endpoint. only effective when attribute based endpoint configuration is being used.
/// </summary>
/// <typeparam name="TPostProcessor">the type of the post-processor</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class PostProcessorAttribute<TPostProcessor> : Attribute, IProcessorAttribute where TPostProcessor : class, IPostProcessor
{
    static int _unused;

    void IProcessorAttribute.AddToEndpointDefinition(EndpointDefinition def)
    {
        EndpointDefinition.AddProcessor<TPostProcessor>(Order.After, def.PostProcessorList, ref _unused);
    }
}