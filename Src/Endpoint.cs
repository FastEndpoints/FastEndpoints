using FluentValidation;

namespace EZEndpoints
{
    public abstract class Endpoint : IEndpoint
    {
        internal static IServiceProvider serviceProvider;
        private static IConfiguration? config;
        private static IWebHostEnvironment? env;
        private static ILogger? logger;

        protected IConfiguration Config => config ??= serviceProvider.GetService<IConfiguration>();
        protected IWebHostEnvironment Env => env ??= serviceProvider.GetService<IWebHostEnvironment>();
        protected ILogger Logger => logger ??= serviceProvider.GetService<ILogger<Endpoint>>();

        internal string[]? routes;
        internal string[]? verbs;
        internal bool throwIfValidationFailed = true;
        internal bool allowAnnonymous;
        internal string[]? policies;
        internal string[]? roles;
        internal string[]? permissions;
        internal bool allowAnyPermission;
        internal bool acceptFiles;

        internal abstract Task ExecAsync(HttpContext ctx, CancellationToken cancellation);

        protected TService? Resolve<TService>() => serviceProvider.GetService<TService>();
        protected object? Resolve(Type typeOfService) => serviceProvider.GetService(typeOfService);
    }

    public abstract class Endpoint<TRequest> : Endpoint<TRequest, EmptyValidator<TRequest>>
        where TRequest : IRequest, new()
    { }

    public abstract class Endpoint<TRequest, TValidator> : Endpoint
        where TRequest : IRequest, new()
        where TValidator : AbstractValidator<TRequest>, new()
    {
        protected void Routes(params string[] patterns) => routes = patterns;
        protected void Verbs(params Http[] methods) => verbs = methods.Select(m => m.ToString()).ToArray();
        protected void DontThrowIfValidationFails() => throwIfValidationFailed = false;
        protected void AllowAnnonymous() => allowAnnonymous = true;
        protected void Policies(params string[] policyNames) => policies = policyNames;
        protected void Roles(params string[] rolesNames) => roles = rolesNames;
        protected void Permissions(params string[] permissions) => Permissions(false, permissions);
        protected void Permissions(bool allowAny, params string[] permissions)
        {
            allowAnyPermission = allowAny;
            this.permissions = permissions;
        }
        protected void AcceptFiles() => acceptFiles = true;

        protected abstract Task ExecuteAsync(TRequest req, CancellationToken cancellation);

        internal override async Task ExecAsync(HttpContext ctx, CancellationToken cancellation)
        {
            var req = await BindIncomingData(ctx).ConfigureAwait(false);

            var reqCtx = new Context<TRequest>(ctx);

            try
            {
                ValidateRequest(req, reqCtx);
                await ExecuteAsync(req, cancellation).ConfigureAwait(false);
            }
            catch (ValidationFailureException)
            {
                await reqCtx.SendErrorAsync().ConfigureAwait(false);
            }
        }

        private static async Task<TRequest> BindIncomingData(HttpContext ctx)
        {
            TRequest? req = default;

            if (ctx.Request.HasJsonContentType())
                req = await ctx.Request.ReadFromJsonAsync<TRequest>(Context.SerializerOptions).ConfigureAwait(false);

            if (req is null) req = new();

            BindFromRouteValues(req, ctx);

            if (req is null)
                throw new InvalidOperationException("Unable to populate a Request from either the JSON body or URL Params!");

            return req;
        }

        private void ValidateRequest(TRequest req, Context<TRequest> ctx)
        {
            if (typeof(TValidator) == typeof(EmptyValidator<TRequest>))
                return;

            TValidator val = new();

            var valResult = val.Validate(req);

            if (!valResult.IsValid)
                ctx.ValidationFailures.AddRange(valResult.Errors);

            if (throwIfValidationFailed && ctx.ValidationFailed)
            {
                throw new ValidationFailureException();
            }
        }

        private static void BindFromRouteValues(TRequest req, HttpContext ctx)
        {
            foreach (var rv in ctx.Request.RouteValues)
            {
                ReqTypeCache<TRequest>.Props.TryGetValue(rv.Key.ToLower(), out var pInfo);

                if (pInfo?.PropertyType != typeof(string))
                    pInfo?.SetValue(req, Convert.ChangeType(rv.Value, pInfo.PropertyType));
                else
                    pInfo?.SetValue(req, rv.Value);
            }
        }
    }
}
