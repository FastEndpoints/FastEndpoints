#pragma warning disable CA1822

    using FluentValidation.Results;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    namespace FastEndpoints;

    /// <summary>
    /// binder context supplied to custom request binders.
    /// </summary>
    public readonly struct BinderContext : IServiceResolverBase
    {
        /// <summary>
        /// the http context of the current request
        /// </summary>
        public HttpContext HttpContext { get; init; }

        /// <summary>
        /// a list of validation failures for the endpoint. you can add your own validation failures for properties of the request dto using this property.
        /// </summary>
        public List<ValidationFailure> ValidationFailures { get; init; }

        /// <summary>
        /// the configured json serializer options of the app, which was specified at app startup.
        /// </summary>
        public JsonSerializerOptions SerializerOptions => Cfg.SerOpts.Options;

        /// <summary>
        /// if the current endpoint is configured with a json serializer context, it will be provided to the custom request binder with this property.
        /// </summary>
        public JsonSerializerContext? JsonSerializerContext { get; init; }

        /// <summary>
        /// set 'true' to disable auto binding of form data which enables uploading and reading of large files without buffering to memory/disk.
        /// you can access the multipart sections for reading via the FormFileSectionsAsync() method.
        /// </summary>
        public bool DontAutoBindForms { get; init; }

        /// <summary>
        /// constructor of the binder context
        /// </summary>
        /// <param name="httpContext">the http context of the current request</param>
        /// <param name="validationFailures">the validation failure collection of the endpoint</param>
        /// <param name="jsonSerializerContext">json serializer context of the endpoint if applicable</param>
        /// <param name="dontAutoBindForms">whether or not to enable auto binding of form data</param>
        public BinderContext(HttpContext httpContext,
                             List<ValidationFailure> validationFailures,
                             JsonSerializerContext? jsonSerializerContext,
                             bool dontAutoBindForms)
        {
            HttpContext = httpContext;
            ValidationFailures = validationFailures;
            JsonSerializerContext = jsonSerializerContext;
            DontAutoBindForms = dontAutoBindForms;
        }

        /// <inheritdoc />
        public TService? TryResolve<TService>() where TService : class
            => Cfg.ServiceResolver.TryResolve<TService>();

        /// <inheritdoc />
        public object? TryResolve(Type typeOfService)
            => Cfg.ServiceResolver.TryResolve(typeOfService);

        /// <inheritdoc />
        public TService Resolve<TService>() where TService : class
            => Cfg.ServiceResolver.Resolve<TService>();

        /// <inheritdoc />
        public object Resolve(Type typeOfService)
            => Cfg.ServiceResolver.Resolve(typeOfService);

        /// <inheritdoc />
        public IServiceScope CreateScope()
            => Cfg.ServiceResolver.CreateScope();

    #if NET8_0_OR_GREATER
        /// <inheritdoc />
        public TService? TryResolve<TService>(string keyName) where TService : class
            => Cfg.ServiceResolver.TryResolve<TService>(keyName);

        /// <inheritdoc />
        public object? TryResolve(Type typeOfService, string keyName)
            => Cfg.ServiceResolver.TryResolve(typeOfService, keyName);

        /// <inheritdoc />
        public TService Resolve<TService>(string keyName) where TService : class
            => Cfg.ServiceResolver.Resolve<TService>(keyName);

        /// <inheritdoc />
        public object Resolve(Type typeOfService, string keyName)
            => Cfg.ServiceResolver.Resolve(typeOfService, keyName);
    #endif
    }