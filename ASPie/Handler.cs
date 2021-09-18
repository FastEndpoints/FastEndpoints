using FluentValidation;

namespace ASPie
{
    public abstract class Handler<TRequest, TResponse, TValidator> : IHandler
        where TRequest : IRequest, new()
        where TResponse : IResponse, new()
        where TValidator : AbstractValidator<TRequest>, new()
    {
        private protected List<string> routes = new();
        private protected List<Http> verbs = new();
        private protected bool throwIfValidationFailed = true;

        protected void Routes(params string[] patterns) => routes.AddRange(patterns);
        protected void Verbs(params Http[] methods) => verbs.AddRange(methods);
        protected void DontThrowIfValidationFails() => throwIfValidationFailed = false;

        protected abstract Task HandleAsync(TRequest req, RequestContext ctx);

        internal async Task ExecAsync(HttpContext ctx)
        {
            var req = await BindIncomingData(ctx).ConfigureAwait(false);

            var reqCtx = new RequestContext(ctx);

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
                req = await ctx.Request.ReadFromJsonAsync<TRequest>(RequestContext.SerializerOptions).ConfigureAwait(false);
            else
                req = PopulateFromURL(ctx);

            if (req is null)
                throw new InvalidOperationException("Unable to populate a Request from either the JSON body or URL Params!");

            return req;
        }

        private void ValidateRequest(TRequest req, RequestContext ctx)
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
