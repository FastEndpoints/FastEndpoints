using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Text.Json;

namespace FastEndpoints
{
    public abstract class Endpoint : IEndpoint
    {
        public static JsonSerializerOptions SerializerOptions { get; set; } = new() { PropertyNamingPolicy = null };

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        internal static IServiceProvider serviceProvider; //this is set by UseFastEndpoints()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        private static IConfiguration? config;
        private static IWebHostEnvironment? env;
        private static ILogger? logger;

#pragma warning disable CS8603 // Possible null reference return.
        protected static IConfiguration Config => config ??= serviceProvider.GetService<IConfiguration>();
        protected static IWebHostEnvironment Env => env ??= serviceProvider.GetService<IWebHostEnvironment>();
        protected static ILogger Logger => logger ??= serviceProvider.GetService<ILogger<Endpoint>>();
#pragma warning restore CS8603 // Possible null reference return.

        internal string[]? routes;
        internal string[]? verbs;
        internal bool throwIfValidationFailed = true;
        internal bool allowAnnonymous;
        internal string[]? policies;
        internal string[]? roles;
        internal string[]? permissions;
        internal bool allowAnyPermission;
        internal bool acceptFiles;

        internal abstract Task ExecAsync(HttpContext ctx, CancellationToken ct);

        protected static TService? Resolve<TService>() => serviceProvider.GetService<TService>();
        protected static object? Resolve(Type typeOfService) => serviceProvider.GetService(typeOfService);
    }

    public abstract class Endpoint<TRequest> : Endpoint<TRequest, EmptyValidator<TRequest>> where TRequest : IRequest, new() { }

    public abstract class Endpoint<TRequest, TValidator> : Endpoint
        where TRequest : IRequest, new()
        where TValidator : AbstractValidator<TRequest>, new()
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected HttpContext HttpContext { get; set; } //this is set when ExecAsync is called by EndpointExecutor
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

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

        protected abstract Task ExecuteAsync(TRequest req, CancellationToken ct);

        internal override async Task ExecAsync(HttpContext ctx, CancellationToken cancellation)
        {
            HttpContext = ctx;
            var req = await BindIncomingDataAsync(ctx, cancellation).ConfigureAwait(false);
            try
            {
                BindFromUserClaims(req, ctx);
                ValidateRequest(req);
                await ExecuteAsync(req, cancellation).ConfigureAwait(false);
            }
            catch (ValidationFailureException)
            {
                await SendErrorAsync(cancellation).ConfigureAwait(false);
            }
        }

        public void AddError(string message)
            => ValidationFailures.Add(new ValidationFailure("GeneralErrors", message));

        public void AddError(Expression<Func<TRequest, object>> property, string errorMessage)
        {
            ValidationFailures.Add(
                new ValidationFailure(property.PropertyName(), errorMessage));
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

        public Task SendErrorAsync(CancellationToken cancellation = default)
        {
            HttpContext.Response.StatusCode = 400;
            return HttpContext.Response.WriteAsJsonAsync(new ErrorResponse(ValidationFailures), SerializerOptions, cancellation);
        }

        public Task SendAsync(object response, int statusCode = 200, CancellationToken cancellation = default)
        {
            HttpContext.Response.StatusCode = statusCode;
            return HttpContext.Response.WriteAsJsonAsync(response, SerializerOptions, cancellation);
        }

        public ValueTask SendBytesAsync(byte[] bytes, string contentType = "application/octet-stream", CancellationToken cancellation = default)
        {
            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.ContentType = contentType;
            HttpContext.Response.ContentLength = bytes.Length;
            return HttpContext.Response.Body.WriteAsync(bytes, cancellation);
        }

        public Task SendOkAsync()
        {
            HttpContext.Response.StatusCode = 200;
            return Task.CompletedTask;
        }

        public Task SendNoContentAsync()
        {
            HttpContext.Response.StatusCode = 204;
            return Task.CompletedTask;
        }

        public Task SendNotFoundAsync()
        {
            HttpContext.Response.StatusCode = 404;
            return Task.CompletedTask;
        }

        public Task<IFormCollection> GetFormAsync(CancellationToken cancellation = default)
        {
            var req = HttpContext.Request;

            if (!req.HasFormContentType)
                throw new InvalidOperationException("This request doesn't have any multipart form data!");

            return req.ReadFormAsync(cancellation);
        }

        public async Task<IFormFileCollection> GetFilesAsync(CancellationToken cancellation = default)
        {
            return (await GetFormAsync(cancellation).ConfigureAwait(false)).Files;
        }

        private static async Task<TRequest> BindIncomingDataAsync(HttpContext ctx, CancellationToken cancellation)
        {
            TRequest? req = default;

            if (ctx.Request.HasJsonContentType())
                req = await ctx.Request.ReadFromJsonAsync<TRequest>(SerializerOptions, cancellation).ConfigureAwait(false);

            if (req is null) req = new();

            BindFromRouteValues(req, ctx.Request.RouteValues);

            return req;
        }

        private void BindFromUserClaims(TRequest req, HttpContext ctx)
        {
            foreach (var cacheEntry in ReqTypeCache<TRequest>.FromClaimProps)
            {
                var claimType = cacheEntry.claimType;
                var claimVal = ctx.User.FindFirst(c => c.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase))?.Value;

                if (claimVal is null && cacheEntry.forbidIfMissing)
                    ValidationFailures.Add(new(claimType, "User doesn't have this claim type!"));

                if (claimVal is not null)
                    cacheEntry.propInfo.SetValue(req, claimVal);
            }
            if (ValidationFailed) throw new ValidationFailureException();
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

        private static void BindFromRouteValues(TRequest req, RouteValueDictionary routeValues)
        {
            foreach (var rv in routeValues)
            {
                ReqTypeCache<TRequest>.Props.TryGetValue(rv.Key.ToLower(), out var prop);

                if (prop?.PropertyType != typeof(string))
                    prop?.SetValue(req, Convert.ChangeType(rv.Value, prop.PropertyType));
                else
                    prop?.SetValue(req, rv.Value);
            }
        }
    }
}

//using (var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, true, 1024, true))
//{
//    var bodyStr = await reader.ReadToEndAsync();
//}