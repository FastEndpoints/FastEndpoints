namespace ASPie
{
    public abstract class HandlerBase<TRequest, TResponse> : IHandler
        where TRequest : IRequest, new()
        where TResponse : IResponse, new()
    {
        private protected List<string> routes = new();
        private protected List<Http> verbs = new();

        protected void Routes(params string[] patterns) => routes.AddRange(patterns);
        protected void Verbs(params Http[] methods) => verbs.AddRange(methods);

        protected abstract Task HandleAsync(TRequest req, RequestContext ctx);

        protected internal async Task ExecAsync(HttpContext ctx)
        {
            TRequest? req;

            if (ctx.Request.HasJsonContentType())
            {
                req = await ctx.Request.ReadFromJsonAsync<TRequest>().ConfigureAwait(false);
            }
            else
            {
                req = PopulateRequestFromUrlParams(new TRequest());
            }

            if (req == null)
                throw new InvalidOperationException("Unable to populate a Request DTO from either the JSON body or URL Params!");

            await HandleAsync(req, new RequestContext(ctx)).ConfigureAwait(false);
        }

        private TRequest? PopulateRequestFromUrlParams(TRequest request)
        {
            throw new NotImplementedException();
        }


    }
}
