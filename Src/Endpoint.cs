using FluentValidation;
using FluentValidation.Results;
using System.Linq.Expressions;
using System.Text.Json;

namespace EZEndpoints
{
    public abstract class Endpoint : IEndpoint
    {
        public static JsonSerializerOptions SerializerOptions { get; set; } = new() { PropertyNamingPolicy = null };

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
        protected HttpContext HttpContext { get; set; } //this is set when ExecAsync is called by EndpointExecutor
        protected string BaseURL { get => HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/"; }
        protected Http Verb { get => Enum.Parse<Http>(HttpContext.Request.Method); }
        protected List<ValidationFailure> ValidationFailures { get; } = new();
        protected bool ValidationFailed { get => ValidationFailures.Count > 0; }

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
            HttpContext = ctx;
            var req = await BindIncomingData(ctx).ConfigureAwait(false);
            try
            {
                ValidateRequest(req);
                await ExecuteAsync(req, cancellation).ConfigureAwait(false);
            }
            catch (ValidationFailureException)
            {
                await SendErrorAsync().ConfigureAwait(false);
            }
        }

        public void AddError(string message)
            => ValidationFailures.Add(new ValidationFailure("GeneralErrors", message));

        public void AddError(Expression<Func<TRequest, object>> property, string errorMessage)
        {
            var exp = (MemberExpression)property.Body;
            if (exp is null) throw new ArgumentException("Please supply a valid member expression!");
            ValidationFailures.Add(
                new ValidationFailure(exp.Member.Name, errorMessage));
        }

        public void ThrowIfAnyErrors()
        {
            if (ValidationFailed)
                throw new ValidationFailureException();
        }

        public void ThrowError(string message)
        {
            AddError(message);
            ThrowIfAnyErrors();
        }

        public void ThrowError(Expression<Func<TRequest, object>> property, string errorMessage)
        {
            AddError(property, errorMessage);
            ThrowIfAnyErrors();
        }

        public Task SendErrorAsync()
        {
            if (HttpContext.Response.HasStarted)
                return Task.CompletedTask;

            HttpContext.Response.StatusCode = 400;
            return HttpContext.Response.WriteAsJsonAsync(new ErrorResponse(ValidationFailures), SerializerOptions);
        }

        public Task SendAsync(object response)
        {
            if (HttpContext.Response.HasStarted)
                return Task.CompletedTask;

            HttpContext.Response.StatusCode = 200;
            return HttpContext.Response.WriteAsJsonAsync(response, SerializerOptions);
        }

        public ValueTask SendBytesAsync(byte[] bytes, string contentType = "application/octet-stream")
        {
            if (HttpContext.Response.HasStarted)
                return ValueTask.CompletedTask;

            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.ContentType = contentType;
            HttpContext.Response.ContentLength = bytes.Length;
            return HttpContext.Response.Body.WriteAsync(bytes);
        }

        public Task SendOkAsync()
        {
            if (!HttpContext.Response.HasStarted)
                HttpContext.Response.StatusCode = 200;

            return Task.CompletedTask;
        }

        public Task SendNoContentAsync()
        {
            if (!HttpContext.Response.HasStarted)
                HttpContext.Response.StatusCode = 204;

            return Task.CompletedTask;
        }

        public Task SendNotFoundAsync()
        {
            if (!HttpContext.Response.HasStarted)
                HttpContext.Response.StatusCode = 404;

            return Task.CompletedTask;
        }

        public Task<IFormCollection> GetFormAsync()
        {
            var req = HttpContext.Request;

            if (!req.HasFormContentType)
                throw new InvalidOperationException("This request doesn't have any multipart form data!");

            return req.ReadFormAsync();
        }

        public async Task<IFormFileCollection> GetFilesAsync()
        {
            return (await GetFormAsync().ConfigureAwait(false)).Files;
        }

        private static async Task<TRequest> BindIncomingData(HttpContext ctx)
        {
            TRequest? req = default;

            if (ctx.Request.HasJsonContentType())
                req = await ctx.Request.ReadFromJsonAsync<TRequest>(SerializerOptions).ConfigureAwait(false);

            if (req is null) req = new();

            BindFromRouteValues(req, ctx);

            return req;
        }

        private void ValidateRequest(TRequest req)
        {
            if (typeof(TValidator) == typeof(EmptyValidator<TRequest>))
                return;

            TValidator val = new();

            var valResult = val.Validate(req);

            if (!valResult.IsValid)
                ValidationFailures.AddRange(valResult.Errors);

            if (throwIfValidationFailed && ValidationFailed)
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
