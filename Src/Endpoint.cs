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

        internal abstract Task ExecAsync(HttpContext ctx);

        protected TService? Resolve<TService>() => serviceProvider.GetService<TService>();
        protected object? Resolve(Type typeOfService) => serviceProvider.GetService(typeOfService);
    }

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

        protected abstract Task HandleAsync(TRequest req, Context<TRequest> ctx);

        internal override async Task ExecAsync(HttpContext ctx)
        {
            var req = await BindIncomingData(ctx).ConfigureAwait(false);

            var reqCtx = new Context<TRequest>(ctx);

            try
            {
                ValidateRequest(req, reqCtx);
                await HandleAsync(req, reqCtx).ConfigureAwait(false);
            }
            catch (ValidationFailureException)
            {
                await reqCtx.SendErrorAsync().ConfigureAwait(false);
            }
        }

        private async Task<TRequest> BindIncomingData(HttpContext ctx)
        {
            TRequest? req = default;

            if (ctx.Request.HasJsonContentType())
                req = await ctx.Request.ReadFromJsonAsync<TRequest>(Context.SerializerOptions).ConfigureAwait(false);

            if (req is null) req = new();

            PopulateFromURL(req, ctx);

            if (req is null)
                throw new InvalidOperationException("Unable to populate a Request from either the JSON body or URL Params!");

            return req;
        }

        private void ValidateRequest(TRequest req, Context<TRequest> ctx)
        {
            TValidator val = new();

            var valResult = val.Validate(req);

            if (!valResult.IsValid)
                ctx.ValidationFailures.AddRange(valResult.Errors);

            if (throwIfValidationFailed && ctx.ValidationFailed)
            {
                throw new ValidationFailureException();
            }
        }

        private void PopulateFromURL(TRequest req, HttpContext ctx)
        {
            foreach (var rv in ctx.Request.RouteValues)
            {

            }
        }
    }
}
