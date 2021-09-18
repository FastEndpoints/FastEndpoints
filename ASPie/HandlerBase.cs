using FluentValidation;

namespace ASPie
{
    public abstract class HandlerBase<TRequest, TResponse, TValidator> : IHandler
        where TRequest : IRequest, new()
        where TResponse : IResponse, new()
        where TValidator : AbstractValidator<TRequest>, new()
    {
        private protected List<string> routes = new();
        private protected List<Http> verbs = new();
        private protected bool throwOnValidation;

        protected void Routes(params string[] patterns) => routes.AddRange(patterns);
        protected void Verbs(params Http[] methods) => verbs.AddRange(methods);
        protected void ThrowIfValidationFails(bool value) => throwOnValidation = value;

        protected abstract Task HandleAsync(TRequest req, RequestContext ctx);

        internal async Task ExecAsync(HttpContext ctx)
        {
            TRequest? req;

            if (ctx.Request.HasJsonContentType())
            {
                req = await ctx.Request.ReadFromJsonAsync<TRequest>(RequestContext.SerializerOptions).ConfigureAwait(false);
            }
            else
            {
                req = PopulateRequestFromUrlParams(new TRequest());
            }

            if (req == null)
                throw new InvalidOperationException("Unable to populate a Request from either the JSON body or URL Params!");

            var reqCtx = new RequestContext(ctx);

            await ValidateRequestAsync(req, reqCtx).ConfigureAwait(false);

            await HandleAsync(req, reqCtx).ConfigureAwait(false);
        }

        private Task ValidateRequestAsync(TRequest req, RequestContext reqCtx)
        {
            TValidator val = new();
            var valResult = val.Validate(req);
            if (!valResult.IsValid)
                reqCtx.ValidationFailures = valResult.Errors;

            if (throwOnValidation && reqCtx.ValidationFailures.Count > 0)
            {
                return reqCtx.SendErrorAsync();
            }

            return Task.CompletedTask;
        }

        private TRequest? PopulateRequestFromUrlParams(TRequest request)
        {
            throw new NotImplementedException();
        }
    }
}
