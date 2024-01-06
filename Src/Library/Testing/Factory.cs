using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// a factory for instantiating endpoints/event/mappers/validators/etc. for testing purposes
/// </summary>
public static class Factory
{
    static readonly EndpointFactory _epFactory = new();

    /// <summary>
    /// get an instance of an endpoint suitable for unit testing
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint to create an instance of</typeparam>
    /// <param name="httpContext">a default http context object</param>
    /// <param name="ctorDependencies">the dependencies of the endpoint if it has any constructor injected dependencies</param>
    public static TEndpoint Create<TEndpoint>(DefaultHttpContext httpContext, params object?[] ctorDependencies) where TEndpoint : class, IEndpoint
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (httpContext.RequestServices is null)
            httpContext.AddTestServices(_ => { });

        BaseEndpoint ep;
        var tEndpoint = typeof(TEndpoint);

        //because this is typically done by type discovery and it doesn't run in unit tests.
        var epDef = new EndpointDefinition(
            tEndpoint,
            tEndpoint.GetGenericArgumentsOfType(Types.EndpointOf2)?[0] ?? Types.EmptyRequest,
            tEndpoint.GetGenericArgumentsOfType(Types.EndpointOf2)?[1] ?? Types.EmptyRequest)
        {
            MapperType = tEndpoint.GetGenericArgumentsOfType(Types.EndpointOf3)?[2]
        };

        if (ctorDependencies.Length > 0)
            ep = (BaseEndpoint)Activator.CreateInstance(tEndpoint, ctorDependencies)!; //ctor injection only
        else
            ep = _epFactory.Create(epDef, httpContext); //ctor & property injection

        //https://github.com/FastEndpoints/FastEndpoints/issues/569
        epDef.EndpointAttributes = epDef.EndpointType.GetCustomAttributes(true);
        epDef.ImplementsConfigure = epDef.EndpointType.GetMethod(nameof(BaseEndpoint.Configure))?.IsDefined(Types.NotImplementedAttribute, false) is false;

        epDef.Initialize(ep, httpContext);

        return (ep as TEndpoint)!;
    }

    /// <summary>
    /// get an instance of an endpoint suitable for unit testing
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint to create an instance of</typeparam>
    /// <param name="httpContext">an action for configuring the default http context object</param>
    /// <param name="ctorDependencies">the dependencies of the endpoint if it has any constructor injected arguments</param>
    public static TEndpoint Create<TEndpoint>(Action<DefaultHttpContext> httpContext, params object?[] ctorDependencies)
        where TEndpoint : class, IEndpoint
    {
        var ctx = new DefaultHttpContext();
        httpContext(ctx);

        return Create<TEndpoint>(ctx, ctorDependencies);
    }

    /// <summary>
    /// get an instance of an endpoint suitable for unit testing
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint to create an instance of</typeparam>
    /// <param name="ctorDependencies">the dependencies of the endpoint if it has any constructor injected dependencies</param>
    public static TEndpoint Create<TEndpoint>(params object?[] ctorDependencies) where TEndpoint : class, IEndpoint
        => Create<TEndpoint>(new DefaultHttpContext(), ctorDependencies);

    /// <summary>
    /// adds the minimum required set of services for unit testing FE endpoints
    /// </summary>
    public static IServiceCollection AddServicesForUnitTesting(this IServiceCollection services)
        => services
           .AddSingleton<ILoggerFactory, LoggerFactory>()
           .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
           .AddSingleton<CommandHandlerRegistry>()
           .AddSingleton(typeof(EventBus<>));

    /// <summary>
    /// register fake/mock/test services for the http context. typically only used with unit tests with the <c>Factory.Create()</c>" method/>
    /// </summary>
    /// <param name="s">an action for adding services to the <see cref="IServiceCollection" /></param>
    /// <exception cref="InvalidOperationException">thrown if the <see cref="HttpContext.RequestServices" /> is not empty</exception>
    public static void AddTestServices(this HttpContext ctx, Action<IServiceCollection> s)
    {
        if (ctx.RequestServices is not null)
            throw new InvalidOperationException("You cannot add services to this http context because it's not empty!");

        if (Cfg.ResolverIsNotSet)
        {
            var testingProvider = new ServiceCollection().AddHttpContextAccessor().BuildServiceProvider();
            Cfg.ServiceResolver = new ServiceResolver(
                provider: testingProvider,
                ctxAccessor: testingProvider.GetRequiredService<IHttpContextAccessor>(),
                isUnitTestMode: true);
        }

        var collection = new ServiceCollection();
        collection.AddServicesForUnitTesting();
        s(collection);
        ctx.RequestServices = collection.BuildServiceProvider();
        Cfg.ServiceResolver.Resolve<IHttpContextAccessor>().HttpContext = ctx;
    }

    /// <summary>
    /// register fake/mock/test services for the current test execution context.
    /// </summary>
    /// <param name="s">an action for adding services to the <see cref="IServiceCollection" /></param>
    public static void RegisterTestServices(Action<IServiceCollection> s)
    {
        new DefaultHttpContext().AddTestServices(s);
    }

    /// <summary>
    /// get an instance of a validator that uses Resolve&lt;T&gt;() methods to obtain services registered in the DI container.
    /// </summary>
    /// <typeparam name="TValidator">the type of the validator</typeparam>
    /// <param name="s">an action for adding services to the <see cref="IServiceCollection" /></param>
    public static TValidator CreateValidator<TValidator>(Action<IServiceCollection> s) where TValidator : class, IValidator
    {
        new DefaultHttpContext().AddTestServices(s);

        return (TValidator)Cfg.ServiceResolver.CreateInstance(typeof(TValidator));
    }

    /// <summary>
    /// get an instance of a mapper that uses Resolve&lt;T&gt;() methods to obtain services registered in the DI container.
    /// </summary>
    /// <typeparam name="TMapper">the type of the mapper</typeparam>
    /// <param name="s">an action for adding services to the <see cref="IServiceCollection" /></param>
    public static TMapper CreateMapper<TMapper>(Action<IServiceCollection> s) where TMapper : class, IMapper
    {
        new DefaultHttpContext().AddTestServices(s);

        return (TMapper)Cfg.ServiceResolver.CreateInstance(typeof(TMapper));
    }

    /// <summary>
    /// get an instance of an event suitable for unit testing.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event</typeparam>
    /// <param name="handlers">the fake/mock event handlers to register for this event</param>
    /// <param name="s">an optional action for adding services to the <see cref="IServiceCollection" /></param>
    public static TEvent CreateEvent<TEvent>(IEnumerable<IEventHandler<TEvent>> handlers, Action<IServiceCollection>? s = null)
        where TEvent : class, IEvent
    {
        new DefaultHttpContext().AddTestServices(Action + s);

        return (TEvent)Cfg.ServiceResolver.CreateInstance(typeof(TEvent));

        void Action(IServiceCollection sc)
            => sc.AddSingleton(handlers);
    }
}