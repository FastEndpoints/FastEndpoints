namespace ASPie
{
    public abstract class Handler<TRequest, TResponse> : IHandler where TRequest : IRequest where TResponse : IResponse, new()
    {
        private protected List<string> routes = new();
        private protected List<Http> verbs = new();

        private protected void Routes(params string[] patterns) => routes.AddRange(patterns);
        private protected void Verbs(params Http[] methods) => verbs.AddRange(methods);

        public virtual Task<TResponse> HandleAsync(TRequest r) => throw new NotImplementedException();
        protected TResponse NewResponse() => new();
    }
}
