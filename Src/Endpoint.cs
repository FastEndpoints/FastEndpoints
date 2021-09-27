using FastEndpoints.Validation;
using FastEndpoints.Validation.Results;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace FastEndpoints
{
    public abstract class EndpointBase : IEndpoint
    {
        internal static JsonSerializerOptions? SerializerOptions { get; set; } //set on app startup from .UseFastEndpoints()

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

        internal string GetTestURL()
        {
            if (routes is null)
                throw new ArgumentNullException(nameof(routes));

            return routes[0].Replace("{", "").Replace("}", "");
        }
    }

    public abstract class BasicEndpoint : Endpoint<EmptyRequest, EmptyValidator<EmptyRequest>> { }

    public abstract class Endpoint<TRequest> : Endpoint<TRequest, EmptyValidator<TRequest>> where TRequest : new() { }

    public abstract class Endpoint<TRequest, TValidator> : EndpointBase
        where TRequest : new()
        where TValidator : Validator<TRequest>, new()
    {
#pragma warning disable CS8618
        protected HttpContext HttpContext { get; private set; }
#pragma warning restore CS8618

        protected IConfiguration Config => HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        protected IWebHostEnvironment Env => HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        protected ILogger Logger => HttpContext.RequestServices.GetRequiredService<ILogger<Endpoint<TRequest>>>();
        protected string BaseURL { get => HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/"; }
        protected Http HttpMethod { get => Enum.Parse<Http>(HttpContext.Request.Method); }
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

        protected abstract Task HandleAsync(TRequest req, CancellationToken ct);

        internal override async Task ExecAsync(HttpContext ctx, CancellationToken cancellation)
        {
            HttpContext = ctx;
            var req = await BindIncomingDataAsync(ctx, cancellation).ConfigureAwait(false);
            try
            {
                BindFromUserClaims(req, ctx, ValidationFailures);
                ValidateRequest(req);
                await HandleAsync(req, cancellation).ConfigureAwait(false);
            }
            catch (ValidationFailureException)
            {
                await SendErrorsAsync(cancellation).ConfigureAwait(false);
            }
        }

        protected void AddError(string message)
            => ValidationFailures.Add(new ValidationFailure("GeneralErrors", message));

        protected void AddError(Expression<Func<TRequest, object>> property, string errorMessage)
        {
            ValidationFailures.Add(
                new ValidationFailure(property.PropertyName(), errorMessage));
        }

        protected void ThrowIfAnyErrors()
        {
            if (ValidationFailed)
                throw new ValidationFailureException();
        }

        protected void ThrowError(string message)
        {
            AddError(message);
            ThrowIfAnyErrors();
        }

        protected void ThrowError(Expression<Func<TRequest, object>> property, string errorMessage)
        {
            AddError(property, errorMessage);
            ThrowIfAnyErrors();
        }

        protected Task SendErrorsAsync(CancellationToken cancellation = default)
        {
            HttpContext.Response.StatusCode = 400;
            return HttpContext.Response.WriteAsJsonAsync(new ErrorResponse(ValidationFailures), SerializerOptions, cancellation);
        }

        protected Task SendAsync(object response, int statusCode = 200, CancellationToken cancellation = default)
        {
            HttpContext.Response.StatusCode = statusCode;
            return HttpContext.Response.WriteAsJsonAsync(response, SerializerOptions, cancellation);
        }

        protected ValueTask SendBytesAsync(byte[] bytes, string contentType = "application/octet-stream", CancellationToken cancellation = default)
        {
            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.ContentType = contentType;
            HttpContext.Response.ContentLength = bytes.Length;
            return HttpContext.Response.Body.WriteAsync(bytes, cancellation);
        }

        protected Task SendOkAsync()
        {
            HttpContext.Response.StatusCode = 200;
            return Task.CompletedTask;
        }

        protected Task SendNoContentAsync()
        {
            HttpContext.Response.StatusCode = 204;
            return Task.CompletedTask;
        }

        protected Task SendNotFoundAsync()
        {
            HttpContext.Response.StatusCode = 404;
            return Task.CompletedTask;
        }

        protected TService? Resolve<TService>() => HttpContext.RequestServices.GetService<TService>();
        protected object? Resolve(Type typeOfService) => HttpContext.RequestServices.GetService(typeOfService);

        protected Task<IFormCollection> GetFormAsync(CancellationToken cancellation = default)
        {
            var req = HttpContext.Request;

            if (!req.HasFormContentType)
                throw new InvalidOperationException("This request doesn't have any multipart form data!");

            return req.ReadFormAsync(cancellation);
        }

        protected async Task<IFormFileCollection> GetFilesAsync(CancellationToken cancellation = default)
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

        private static void BindFromUserClaims(TRequest req, HttpContext ctx, List<ValidationFailure> failures)
        {
            for (int i = 0; i < ReqTypeCache<TRequest, TValidator>.FromClaimProps.Count; i++)
            {
                (string claimType, bool forbidIfMissing, PropertyInfo propInfo) cacheEntry
                    = ReqTypeCache<TRequest, TValidator>.FromClaimProps[i];

                var claimType = cacheEntry.claimType;
                var claimVal = ctx.User.FindFirst(c => c.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase))?.Value;

                if (claimVal is null && cacheEntry.forbidIfMissing)
                    failures.Add(new(claimType, "User doesn't have this claim type!"));

                if (claimVal is not null)
                    cacheEntry.propInfo.SetValue(req, claimVal);
            }
            if (failures.Count > 0) throw new ValidationFailureException();
        }

        private void ValidateRequest(TRequest req)
        {
            if (typeof(TValidator) == typeof(EmptyValidator<TRequest>))
                return;

            var valResult = ReqTypeCache<TRequest, TValidator>.Validator.Validate(req);

            if (!valResult.IsValid)
                ValidationFailures.AddRange(valResult.Errors);

            if (ValidationFailed && throwIfValidationFailed)
                throw new ValidationFailureException();
        }

        private static void BindFromRouteValues(TRequest req, RouteValueDictionary routeValues)
        {
            foreach (var rv in routeValues)
            {
                if (ReqTypeCache<TRequest, TValidator>.Props.TryGetValue(rv.Key.ToLower(), out var prop))
                {
                    bool success = false;

                    switch (prop.typeCode)
                    {
                        case TypeCode.Boolean:
                            success = bool.TryParse((string?)rv.Value, out var resBool);
                            prop.propInfo.SetValue(req, resBool);
                            break;

                        case TypeCode.Int32:
                            success = int.TryParse((string?)rv.Value, out var resInt);
                            prop.propInfo.SetValue(req, resInt);
                            break;

                        case TypeCode.Int64:
                            success = long.TryParse((string?)rv.Value, out var resLong);
                            prop.propInfo.SetValue(req, resLong);
                            break;

                        case TypeCode.Double:
                            success = double.TryParse((string?)rv.Value, out var resDbl);
                            prop.propInfo.SetValue(req, resDbl);
                            break;

                        case TypeCode.Decimal:
                            success = decimal.TryParse((string?)rv.Value, out var resDec);
                            prop.propInfo.SetValue(req, resDec);
                            break;

                        case TypeCode.String:
                            success = true;
                            prop.propInfo.SetValue(req, rv.Value);
                            break;
                    }

                    if (!success)
                    {
                        throw new NotSupportedException(
                        "Binding route value failed! " +
                        $"{typeof(TRequest).FullName}.{prop.propInfo.Name}[{prop.typeCode}] Tried: \"{rv.Value}\"");
                    }
                }
            }
        }
    }
}

//using (var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, true, 1024, true))
//{
//    var bodyStr = await reader.ReadToEndAsync();
//}