using FastEndpoints.Validation;
using FastEndpoints.Validation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.Json;

namespace FastEndpoints
{
    /// <summary>
    /// base class for all endpoint classes
    /// </summary>
    public abstract class BaseEndpoint : IEndpoint
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
        internal string[]? claims;
        internal bool allowAnyClaim;
        internal bool allowFileUploads;
        internal Action<DelegateEndpointConventionBuilder>? internalConfigAction;
        internal Action<DelegateEndpointConventionBuilder>? userConfigAction;

        internal abstract Task ExecAsync(HttpContext ctx, IValidator validator, CancellationToken ct);

        internal string GetTestURL()
        {
            if (routes is null)
                throw new ArgumentNullException(nameof(routes));

            return routes[0];
        }
    }

    /// <summary>
    /// use this base class for defining endpoints that doesn't need a request dto. usually used for routes that doesn't have any parameters.
    /// </summary>
    public abstract class EndpointWithoutRequest : Endpoint<EmptyRequest> { }

    /// <summary>
    /// use this base class for defining endpoints that doesn't need a request dto but return a response dto.
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    public abstract class EndpointWithoutRequest<TResponse> : Endpoint<EmptyRequest, TResponse> where TResponse : new() { }

    /// <summary>
    /// use this base class for defining endpoints that only use a request dto and don't use a response dto.
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    public abstract class Endpoint<TRequest> : Endpoint<TRequest, object> where TRequest : new() { };

    /// <summary>
    /// use this base class for defining endpoints that use both request and response dtos.
    /// </summary>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public abstract class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : new() where TResponse : new()
    {
        private IPreProcessor<TRequest>[] preProcessors = Array.Empty<IPreProcessor<TRequest>>();
        private IPostProcessor<TRequest, TResponse>[] postProcessors = Array.Empty<IPostProcessor<TRequest, TResponse>>();

#pragma warning disable CS8618
        /// <summary>
        /// the http context of the current request
        /// </summary>
        protected HttpContext HttpContext { get; private set; }
#pragma warning restore CS8618
        /// <summary>
        /// the current user principal
        /// </summary>
        protected ClaimsPrincipal User => HttpContext.User;
        /// <summary>
        /// the response that is sent to the client.
        /// </summary>
        protected TResponse Response { get; set; } = new();
        /// <summary>
        /// gives access to the configuration
        /// </summary>
        protected IConfiguration Config => HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        /// <summary>
        /// gives access to the hosting environment
        /// </summary>
        protected IWebHostEnvironment Env => HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        /// <summary>
        /// the logger for the current endpoint type
        /// </summary>
        protected ILogger Logger => HttpContext.RequestServices.GetRequiredService<ILogger<Endpoint<TRequest, TResponse>>>();
        /// <summary>
        /// the base url of the current request
        /// </summary>
        protected string BaseURL => HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/";
        /// <summary>
        /// the http method of the current request
        /// </summary>
        protected Http HttpMethod => Enum.Parse<Http>(HttpContext.Request.Method);
        /// <summary>
        /// the list of validation failures for the current request dto
        /// </summary>
        protected List<ValidationFailure> ValidationFailures { get; } = new();
        /// <summary>
        /// indicates if there are any validation failures for the current request
        /// </summary>
        protected bool ValidationFailed => ValidationFailures.Count > 0;
        /// <summary>
        /// the form sent with the request. only populated if content-type is 'application/x-www-form-urlencoded' or 'multipart/form-data'
        /// </summary>
        protected IFormCollection Form => HttpContext.Request.Form;
        /// <summary>
        /// the files sent with the request. only populated is content-type is 'multipart/form-data'
        /// </summary>
        protected IFormFileCollection Files => Form.Files;

        /// <summary>
        /// specify one or more route patterns this endpoint should be listening for
        /// </summary>
        /// <param name="patterns"></param>
        protected void Routes(params string[] patterns)
        {
            routes = patterns;
            internalConfigAction = b =>
            {
                if (typeof(TRequest) != typeof(EmptyRequest)) b.Accepts<TRequest>("application/json");
                b.Produces<TResponse>();
            };
        }
        /// <summary>
        /// specify one or more http method verbs this endpoint should be accepting requests for
        /// </summary>
        /// <param name="methods"></param>
        protected void Verbs(params Http[] methods) => verbs = methods.Select(m => m.ToString()).ToArray();
        /// <summary>
        /// disable auto validation failure responses (400 bad request with error details) for this endpoint
        /// </summary>
        protected void DontThrowIfValidationFails() => throwIfValidationFailed = false;
        /// <summary>
        /// allow unauthenticated requests to this endpoint
        /// </summary>
        protected void AllowAnnonymous() => allowAnnonymous = true;
        /// <summary>
        /// enable file uploads with multipart/form-data content type
        /// </summary>
        protected void AllowFileUploads() => allowFileUploads = true;
        /// <summary>
        /// specify one or more authorization policy names you have added to the middleware pipeline during app startup/ service configuration that should be applied to this endpoint.
        /// </summary>
        /// <param name="policyNames">one or more policy names (must have been added to the pipeline on startup)</param>
        protected void Policies(params string[] policyNames) => policies = policyNames;
        /// <summary>
        /// specify that the current claim principal/ user should posses at least one of the roles (claim type) mentioned here. access will be forbidden if the user doesn't have any of the specified roles.
        /// </summary>
        /// <param name="rolesNames">one or more roles that has access</param>
        protected void Roles(params string[] rolesNames) => roles = rolesNames;
        /// <summary>
        /// specify the permissions a user principal should posses in order to access this endpoint. they must posses ALL of the permissions mentioned here. if not, a 403 forbidden response will be sent.
        /// </summary>
        /// <param name="permissions">the permissions needed to access this endpoint</param>
        protected void Permissions(params string[] permissions) => Permissions(false, permissions);
        /// <summary>
        /// specify the permissions a user principal should posses in order to access this endpoint.
        /// </summary>
        /// <param name="allowAny">if set to true, having any 1 of the specified permissions will enable access</param>
        /// <param name="permissions">the permissions</param>
        protected void Permissions(bool allowAny, params string[] permissions)
        {
            allowAnyPermission = allowAny;
            this.permissions = permissions;
        }
        /// <summary>
        /// specify the claim types a user principal should posses in order to access this endpoint. they must posses ALL of the claim types mentioned here. if not, a 403 forbidden response will be sent.
        /// </summary>
        /// <param name="claims">the claims needed to access this endpoint</param>
        protected void Claims(params string[] claims) => Claims(false, claims);
        /// <summary>
        /// specify the claim types a user principal should posses in order to access this endpoint.
        /// </summary>
        /// <param name="allowAny">if set to true, having any 1 of the specified permissions will enable access</param>
        /// <param name="claims">the claims</param>
        protected void Claims(bool allowAny, params string[] claims)
        {
            allowAnyClaim = allowAny;
            this.claims = claims;
        }
        /// <summary>
        /// configure a collection of pre-processors to be executed before the main handler function is called. processors are executed in the order they are defined here.
        /// </summary>
        /// <param name="preProcessors">the pre processors to be executed</param>
        protected void PreProcessors(params IPreProcessor<TRequest>[] preProcessors) => this.preProcessors = preProcessors;
        /// <summary>
        /// configure a collection of post-processors to be executed after the main handler function is done. processors are executed in the order they are defined here.
        /// </summary>
        /// <param name="postProcessors">the post processors to be executed</param>
        protected void PostProcessors(params IPostProcessor<TRequest, TResponse>[] postProcessors) => this.postProcessors = postProcessors;
        /// <summary>
        /// set endpoint configurations options using an endpoint builder action
        /// </summary>
        /// <param name="builder">the builder for this endpoint</param>
        protected void Options(Action<DelegateEndpointConventionBuilder> builder) => userConfigAction = builder;

        /// <summary>
        /// the handler method for the endpoint. this method is called for each request received.
        /// </summary>
        /// <param name="req">the request dto</param>
        /// <param name="ct">a cancellation token</param>
        protected abstract Task HandleAsync(TRequest req, CancellationToken ct);

        internal override async Task ExecAsync(HttpContext ctx, IValidator? validator, CancellationToken cancellation)
        {
            HttpContext = ctx;
            var req = await BindIncomingDataAsync(ctx, cancellation).ConfigureAwait(false);
            try
            {
                BindFromUserClaims(req, ctx, ValidationFailures);
                await ValidateRequestAsync(req, (IValidator<TRequest>?)validator, cancellation).ConfigureAwait(false);

                foreach (var p in preProcessors)
                    await p.PreProcessAsync(req, ctx, ValidationFailures, cancellation).ConfigureAwait(false);

                await HandleAsync(req, cancellation).ConfigureAwait(false);

                foreach (var pp in postProcessors)
                    await pp.PostProcessAsync(req, Response, ctx, ValidationFailures, cancellation).ConfigureAwait(false);
            }
            catch (ValidationFailureException)
            {
                await SendErrorsAsync(cancellation).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// adds a "GeneralError" to the current list of validation failures
        /// </summary>
        /// <param name="message">the error message</param>
        protected void AddError(string message)
            => ValidationFailures.Add(new ValidationFailure("GeneralErrors", message));

        /// <summary>
        /// adds an error message for the specified property of the request dto
        /// </summary>
        /// <param name="property">the property to add teh error message for</param>
        /// <param name="errorMessage">the error message</param>
        protected void AddError(Expression<Func<TRequest, object>> property, string errorMessage)
        {
            ValidationFailures.Add(
                new ValidationFailure(property.PropertyName(), errorMessage));
        }

        /// <summary>
        /// interrupt the flow of handler execution and send a 400 bad request with error details if there are any validation failures in the current request. if there are no validation failures, execution will continue past this call.
        /// </summary>
        protected void ThrowIfAnyErrors()
        {
            if (ValidationFailed) throw new ValidationFailureException();
        }

        /// <summary>
        /// add a "GeneralError" to the validation failure list and send back a 400 bad request with error details immediately interrupting handler execution flow. if there are any vallidation failures, no execution will continue past this call.
        /// </summary>
        /// <param name="message">the error message</param>
        protected void ThrowError(string message)
        {
            AddError(message);
            ThrowIfAnyErrors();
        }

        /// <summary>
        /// adds an error message for the specified property of the request dto and sends back a 400 bad request with error details immediately interrupting handler execution flow. no execution will continue past this call.
        /// </summary>
        /// <param name="property"></param>
        /// <param name="errorMessage"></param>
        protected void ThrowError(Expression<Func<TRequest, object>> property, string errorMessage)
        {
            AddError(property, errorMessage);
            ThrowIfAnyErrors();
        }

        /// <summary>
        /// send the supplied response dto serialized as json to the client.
        /// </summary>
        /// <param name="response">the object to serialize to json</param>
        /// <param name="statusCode">optional custom http status code</param>
        /// <param name="cancellation">optional cancellation token</param>
        protected Task SendAsync(TResponse response, int statusCode = 200, CancellationToken cancellation = default)
        {
            Response = response;
            HttpContext.Response.StatusCode = statusCode;
            return HttpContext.Response.WriteAsJsonAsync(response, SerializerOptions, cancellation);
        }

        /// <summary>
        /// send an http 200 ok response without any body
        /// </summary>
        protected Task SendOkAsync()
        {
            HttpContext.Response.StatusCode = 200;
            return Task.CompletedTask;
        }

        /// <summary>
        /// send a 400 bad request with error details of the current validation failures
        /// </summary>
        /// <param name="cancellation"></param>
        protected Task SendErrorsAsync(CancellationToken cancellation = default)
        {
            HttpContext.Response.StatusCode = 400;
            return HttpContext.Response.WriteAsJsonAsync(new ErrorResponse(ValidationFailures), SerializerOptions, cancellation);
        }

        /// <summary>
        /// send a 204 no content response
        /// </summary>
        protected Task SendNoContentAsync()
        {
            HttpContext.Response.StatusCode = 204;
            return Task.CompletedTask;
        }

        /// <summary>
        /// send a 404 not found response
        /// </summary>
        protected Task SendNotFoundAsync()
        {
            HttpContext.Response.StatusCode = 404;
            return Task.CompletedTask;
        }

        /// <summary>
        /// send a 401 unauthorized response
        /// </summary>
        protected Task SendUnauthorizedAsync()
        {
            HttpContext.Response.StatusCode = 401;
            return Task.CompletedTask;
        }

        /// <summary>
        /// send a 403 unauthorized response
        /// </summary>
        protected Task SendForbiddenAsync()
        {
            HttpContext.Response.StatusCode = 403;
            return Task.CompletedTask;
        }

        /// <summary>
        /// send a byte array to the client
        /// </summary>
        /// <param name="bytes">the bytes to send</param>
        /// <param name="contentType">optional content type to set on the http response</param>
        /// <param name="cancellation">optional cancellation token</param>
        protected async Task SendBytesAsync(byte[] bytes, string? fileName = null, string contentType = "application/octet-stream", CancellationToken cancellation = default)
        {
            using var memoryStream = new MemoryStream(bytes);
            await SendStreamAsync(memoryStream, fileName, bytes.Length, contentType, cancellation).ConfigureAwait(false);
        }

        /// <summary>
        /// send a file to the client
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="contentType">optional content type to set on the http response</param>
        /// <param name="cancellation">optional cancellation token</param>
        protected Task SendFileAsync(FileInfo fileInfo, string contentType = "application/octet-stream", CancellationToken cancellation = default)
        {
            return SendStreamAsync(fileInfo.OpenRead(), fileInfo.Name, fileInfo.Length, contentType, cancellation);
        }

        /// <summary>
        /// send the contents of a stream to the client
        /// </summary>
        /// <param name="stream">the stream to read the data from</param>
        /// <param name="fileName">and optional file name to set in the content-disposition header</param>
        /// <param name="fileLengthBytes">optional total size of the file/stream</param>
        /// <param name="contentType">optional content type to set on the http response</param>
        /// <param name="cancellation">optional cancellation token</param>
        protected Task SendStreamAsync(Stream stream, string? fileName = null, long? fileLengthBytes = null, string contentType = "application/octet-stream", CancellationToken cancellation = default)
        {
            HttpContext.Response.StatusCode = 200;
            HttpContext.Response.ContentType = contentType;
            HttpContext.Response.ContentLength = fileLengthBytes;

            if (fileName is not null)
                HttpContext.Response.Headers.Add("Content-Disposition", $"attachment; filename={fileName}");

            return HttpContext.WriteToResponseAsync(stream, cancellation == default ? HttpContext.RequestAborted : cancellation);
        }

        /// <summary>
        /// publish the given model/dto to all the subscribers of the event notification
        /// </summary>
        /// <param name="eventModel">the notification event model/dto to publish</param>
        /// <param name="waitMode">specify whether to wait for none, any or all of the subscribers to complete their work</param>
        ///<param name="cancellation">an optional cancellation token</param>
        /// <returns>a Task that matches the wait mode specified.
        /// Mode.WaitForNone returns an already completed Task (fire and forget).
        /// Mode.WaitForAny returns a Task that will complete when any of the subscribers complete their work.
        /// Mode.WaitForAll return a Task that will complete only when all of the subscribers complete their work.</returns>
        protected Task PublishAsync<TEvent>(TEvent eventModel, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default) where TEvent : class
            => Event<TEvent>.PublishAsync(eventModel, waitMode, cancellation);

        /// <summary>
        /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
        /// </summary>
        /// <typeparam name="TService">the type of the service to resolve</typeparam>
        protected TService? Resolve<TService>() => HttpContext.RequestServices.GetService<TService>();
        /// <summary>
        /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
        /// </summary>
        /// <param name="typeOfService">the type of the service to resolve</param>
        protected object? Resolve(Type typeOfService) => HttpContext.RequestServices.GetService(typeOfService);

        private static async Task<TRequest> BindIncomingDataAsync(HttpContext ctx, CancellationToken cancellation)
        {
            TRequest? req = default;

            if (ctx.Request.HasJsonContentType())
                req = await ctx.Request.ReadFromJsonAsync<TRequest>(SerializerOptions, cancellation).ConfigureAwait(false);

            if (req is null) req = new();

            BindFromFormValues(req, ctx);

            BindFromRouteValues(req, ctx.Request.RouteValues);

            return req;
        }

        private async Task ValidateRequestAsync(TRequest req, IValidator<TRequest>? validator, CancellationToken cancellation)
        {
            if (validator is null) return;

            var valResult = await validator.ValidateAsync(req, cancellation).ConfigureAwait(false);

            if (!valResult.IsValid)
                ValidationFailures.AddRange(valResult.Errors);

            if (ValidationFailed && throwIfValidationFailed)
                throw new ValidationFailureException();
        }

        private static void BindFromFormValues(TRequest req, HttpContext ctx)
        {
            if (!ctx.Request.HasFormContentType) return;

            var formFields = ctx.Request.Form.Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value[0])).ToArray();

            for (int i = 0; i < formFields.Length; i++)
                Bind(req, formFields[i]);
        }

        private static void BindFromUserClaims(TRequest req, HttpContext ctx, List<ValidationFailure> failures)
        {
            for (int i = 0; i < ReqTypeCache<TRequest>.FromClaimProps.Count; i++)
            {
                var cacheEntry = ReqTypeCache<TRequest>.FromClaimProps[i];
                var claimVal = ctx.User.FindFirst(c => c.Type.Equals(cacheEntry.claimType, StringComparison.OrdinalIgnoreCase))?.Value;

                if (claimVal is null && cacheEntry.forbidIfMissing)
                    failures.Add(new(cacheEntry.claimType, "User doesn't have this claim type!"));

                if (claimVal is not null)
                    cacheEntry.propInfo.SetValue(req, claimVal);
            }
            if (failures.Count > 0) throw new ValidationFailureException();
        }

        private static void BindFromRouteValues(TRequest req, RouteValueDictionary routeValues)
        {
            var routeKVPs = routeValues.Where(rv => ((string?)rv.Value)?.StartsWith("{") == false).ToArray();

            for (int i = 0; i < routeKVPs.Length; i++)
                Bind(req, routeKVPs[i]);
        }

        private static void Bind(TRequest req, KeyValuePair<string, object?> rv)
        {
            if (ReqTypeCache<TRequest>.Props.TryGetValue(rv.Key.ToLower(), out var prop))
            {
                bool success = false;

                switch (prop.typeCode)
                {
                    case TypeCode.String:
                        success = true;
                        prop.propInfo.SetValue(req, rv.Value);
                        break;

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