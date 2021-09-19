using FluentValidation;

namespace ASPie
{
    public abstract class Endpoint : IEndpoint
    {
        protected internal string[]? routes;
        protected internal string[]? verbs;
        protected internal bool throwIfValidationFailed = true;
        protected internal bool allowAnnonymous;
        protected internal string[]? policies;
        protected internal string[]? roles;
        protected internal string[]? permissions;
        protected internal bool allowAnyPermission;

        protected internal abstract Task ExecAsync(HttpContext ctx);
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

        protected abstract Task HandleAsync(TRequest req, Context<TRequest> ctx);

        protected internal override async Task ExecAsync(HttpContext ctx)
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
            TRequest? req;

            if (ctx.Request.HasJsonContentType())
                req = await ctx.Request.ReadFromJsonAsync<TRequest>(Context.SerializerOptions).ConfigureAwait(false);
            else
                req = PopulateFromURL(ctx);

            if (req is null)
                throw new InvalidOperationException("Unable to populate a Request from either the JSON body or URL Params!");

            return req;
        }

        private void ValidateRequest(TRequest req, Context<TRequest> ctx)
        {
            TValidator val = new();

            var valResult = val.Validate(req);

            if (!valResult.IsValid)
                ctx.ValidationFailures = valResult.Errors;

            if (throwIfValidationFailed && ctx.ValidationFailed)
            {
                throw new ValidationFailureException();
            }
        }

        private TRequest PopulateFromURL(HttpContext ctx)
        {
            //todo: parameter binding
            throw new NotImplementedException();
        }
    }
}
