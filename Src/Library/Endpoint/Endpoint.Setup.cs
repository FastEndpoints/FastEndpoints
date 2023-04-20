using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : notnull
{
    private static readonly Type tRequest = typeof(TRequest);
    private static readonly Type tResponse = typeof(TResponse);
    private static readonly bool isStringResponse = tResponse.IsAssignableFrom(Types.String);
    private static readonly bool isCollectionResponse = tResponse.IsAssignableTo(Types.IEnumerable);

    /// <summary>
    /// allow unauthenticated requests to this endpoint. optionally specify a set of verbs to allow unauthenticated access with.
    /// i.e. if the endpoint is listening to POST, PUT &amp; PATCH and you specify AllowAnonymous(Http.POST), then only PUT &amp; PATCH will require authentication.
    /// </summary>
    protected void AllowAnonymous(params Http[] verbs) => Definition.AllowAnonymous(verbs);

    /// <summary>
    /// allow unauthenticated requests to this endpoint for a specified set of http verbs.
    /// i.e. if the endpoint is listening to POST, PUT &amp; PATCH and you specify AllowAnonymous(Http.POST), then only PUT &amp; PATCH will require authentication.
    /// </summary>
    protected void AllowAnonymous(string[] verbs) => Definition.AllowAnonymous(verbs);

    /// <summary>
    /// enable file uploads with multipart/form-data content type
    /// </summary>
    /// <param name="dontAutoBindFormData">
    /// set 'true' to disable auto binding of form data which enables uploading and reading of large files without buffering to memory/disk.
    /// you can access the multipart sections for reading via the FormFileSectionsAsync() method.
    /// </param>
    protected void AllowFileUploads(bool dontAutoBindFormData = false) => Definition.AllowFileUploads(dontAutoBindFormData);

    /// <summary>
    /// enable form-data submissions
    /// </summary>
    /// <param name="urlEncoded">set to true to accept `application/x-www-form-urlencoded` content instead of `multipart/form-data` content.</param>
    protected void AllowFormData(bool urlEncoded = false) => Definition.AllowFormData(urlEncoded);

    /// <summary>
    /// specify which authentication schemes to use for authenticating requests to this endpoint
    /// </summary>
    /// <param name="authSchemeNames">the authentication scheme names</param>
    protected void AuthSchemes(params string[] authSchemeNames) => Definition.AuthSchemes(authSchemeNames);

    /// <summary>
    /// allows access if the claims principal has ANY of the given claim types
    /// </summary>
    /// <param name="claimTypes">the claim types</param>
    protected void Claims(params string[] claimTypes) => Definition.Claims(claimTypes);

    /// <summary>
    /// allows access if the claims principal has ALL of the given claim types
    /// </summary>
    /// <param name="claimTypes">the claim types</param>
    protected void ClaimsAll(params string[] claimTypes) => Definition.ClaimsAll(claimTypes);

    /// <summary>
    /// specify to listen for DELETE requests on one or more routes.
    /// </summary>
    protected void Delete(params string[] routePatterns)
    {
        Verbs(Http.DELETE);
        Routes(routePatterns);
    }

    /// <summary>
    /// specify a DELETE route pattern using a replacement expression.
    /// </summary>
    /// <param name="routePattern">
    /// the words prefixed with @ will be replaced by property names of the `new` expression in the order they are specified.
    /// the replacement words do not have to match the request dto property names.
    /// <para><c>/invoice/{@id}/soft-delete</c></para></param>
    /// <param name="members"><c>r => new { r.InvoiceID }</c></param>
    protected void Delete(string routePattern, Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.DELETE);
        Routes(members.BuildRoute(routePattern));
    }

    /// <summary>
    /// describe openapi metadata for this endpoint. optionaly specify whether or not you want to clear the default Accepts/Produces metadata.
    /// <para>
    /// EXAMPLE: <c>b => b.Accepts&lt;Request&gt;("text/plain")</c>
    /// </para>
    /// </summary>
    /// <param name="builder">the route handler builder for this endpoint</param>
    /// <param name="clearDefaults">set to true if the defaults should be cleared</param>
    protected void Description(Action<RouteHandlerBuilder> builder, bool clearDefaults = false) => Definition.Description(builder, clearDefaults);

    /// <summary>
    /// if swagger auto tagging based on path segment is enabled, calling this method will prevent a tag from being added to this endpoint.
    /// </summary>
    protected void DontAutoTag() => Definition.DontAutoTag();

    /// <summary>
    /// use this only if you have your own exception catching middleware.
    /// if this method is called in config, an automatic error response will not be sent to the client by the library.
    /// all exceptions will be thrown and it would be the responsibility of your exeception catching middleware to handle them.
    /// </summary>
    protected void DontCatchExceptions() => Definition.DontCatchExceptions();

    /// <summary>
    /// disable auto validation failure responses (400 bad request with error details) for this endpoint.
    /// <para>HINT: this only applies to request dto validation.</para>
    /// </summary>
    protected void DontThrowIfValidationFails() => Definition.DontThrowIfValidationFails();

    /// <summary>
    /// specify to listen for GET requests on one or more routes.
    /// </summary>
    protected void Get(params string[] routePatterns)
    {
        Verbs(Http.GET);
        Routes(routePatterns);
    }

    /// <summary>
    /// specify a GET route pattern using a replacement expression.
    /// </summary>
    /// <param name="routePattern">
    /// the words prefixed with @ will be replaced by property names of the `new` expression in the order they are specified.
    /// the replacement words do not have to match the request dto property names.
    /// <para><c>/invoice/{@id}/print/{@pageNum}</c></para></param>
    /// <param name="members"><c>r => new { r.InvoiceID, r.PageNumber }</c></param>
    protected void Get(string routePattern, Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.GET);
        Routes(members.BuildRoute(routePattern));
    }

    /// <summary>
    /// if this endpoint is part of an endpoint group, specify the type of the <see cref="FastEndpoints.Group"/> concrete class where the common configuration for the group is specified.
    /// <para>
    /// WARNING: this method can only be called after the endpoint route has been specified.
    /// </para>
    /// </summary>
    /// <typeparam name="TEndpointGroup">the type of your <see cref="FastEndpoints.Group"/> concrete class</typeparam>
    /// <exception cref="InvalidOperationException">thrown if endpoint route hasn't yet been specified</exception>
    protected sealed override void Group<TEndpointGroup>()
    {
        if (Definition.Routes is null)
        {
            throw new InvalidOperationException($"Endpoint group can only be specified after the route has been configured in the [{Definition.EndpointType.FullName}] endpoint class!");
        }
        new TEndpointGroup().Action(Definition);
    }

    /// <summary>
    /// specify to listen for HEAD requests on one or more routes.
    /// </summary>
    protected void Head(params string[] routePatterns)
    {
        Verbs(Http.HEAD);
        Routes(routePatterns);
    }

    /// <summary>
    /// specify a HEAD route pattern using a replacement expression.
    /// </summary>
    /// <param name="routePattern">
    /// the words prefixed with @ will be replaced by property names of the `new` expression in the order they are specified.
    /// the replacement words do not have to match the request dto property names.
    /// <para><c>/invoice/{@id}/print/{@pageNum}</c></para></param>
    /// <param name="members"><c>r => new { r.InvoiceID, r.PageNumber }</c></param>
    protected void Head(string routePattern, Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.HEAD);
        Routes(members.BuildRoute(routePattern));
    }

    /// <summary>
    /// set endpoint configurations options using an endpoint builder action ///
    /// </summary>
    /// <param name="builder">the builder for this endpoint</param>
    protected void Options(Action<RouteHandlerBuilder> builder) => Definition.Options(builder);

    /// <summary>
    /// specify to listen for PATCH requests on one or more routes.
    /// </summary>
    protected void Patch(params string[] routePatterns)
    {
        Verbs(Http.PATCH);
        Routes(routePatterns);
    }

    /// <summary>
    /// specify a PATCH route pattern using a replacement expression.
    /// </summary>
    /// <param name="routePattern">
    /// the words prefixed with @ will be replaced by property names of the `new` expression in the order they are specified.
    /// the replacement words do not have to match the request dto property names.
    /// <para><c>/invoice/{@id}</c></para></param>
    /// <param name="members"><c>r => new { r.InvoiceID }</c></param>
    protected void Patch(string routePattern, Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.PATCH);
        Routes(members.BuildRoute(routePattern));
    }

    /// <summary>
    /// allows access if the claims principal has ANY of the given permissions
    /// </summary>
    /// <param name="permissions">the permissions</param>
    protected void Permissions(params string[] permissions) => Definition.Permissions(permissions);

    /// <summary>
    /// allows access if the claims principal has ALL of the given permissions
    /// </summary>
    /// <param name="permissions">the permissions</param>
    protected void PermissionsAll(params string[] permissions) => Definition.PermissionsAll(permissions);

    /// <summary>
    /// specify an action for building an authorization requirement which applies only to this endpoint.
    /// </summary>
    /// <param name="policy">the policy builder action</param>
    protected void Policy(Action<AuthorizationPolicyBuilder> policy) => Definition.Policy(policy);

    /// <summary>
    /// specify one or more authorization policy names you have added to the middleware pipeline during app startup/ service configuration that should be applied to this endpoint.
    /// </summary>
    /// <param name="policyNames">one or more policy names (must have been added to the pipeline on startup)</param>
    protected void Policies(params string[] policyNames) => Definition.Policies(policyNames);

    /// <summary>
    /// specify to listen for POST requests on one or more routes.
    /// </summary>
    protected void Post(params string[] routePatterns)
    {
        Verbs(Http.POST);
        Routes(routePatterns);
    }

    /// <summary>
    /// specify a POST route pattern using a replacement expression.
    /// </summary>
    /// <param name="routePattern">
    /// the words prefixed with @ will be replaced by property names of the `new` expression in the order they are specified.
    /// the replacement words do not have to match the request dto property names.
    /// <para><c>/invoice/{@id}/page/{@pageNum}</c></para></param>
    /// <param name="members"><c>r => new { r.InvoiceID, r.PageNumber }</c></param>
    protected void Post(string routePattern, Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.POST);
        Routes(members.BuildRoute(routePattern));
    }

    /// <summary>
    /// configure a collection of post-processors to be executed after the main handler function is done. processors are executed in the order they are defined here.
    /// </summary>
    /// <param name="postProcessors">the post processors to be executed</param>
    protected void PostProcessors(params IPostProcessor<TRequest, TResponse>[] postProcessors) => AddProcessors(postProcessors, Definition.PostProcessorList);

    /// <summary>
    /// configure a collection of pre-processors to be executed before the main handler function is called. processors are executed in the order they are defined here.
    /// </summary>
    /// <param name="preProcessors">the pre processors to be executed</param>
    protected void PreProcessors(params IPreProcessor<TRequest>[] preProcessors) => AddProcessors(preProcessors, Definition.PreProcessorList);

    /// <summary>
    /// specify to listen for PUT requests on one or more routes.
    /// </summary>
    protected void Put(params string[] routePatterns)
    {
        Verbs(Http.PUT);
        Routes(routePatterns);
    }

    /// <summary>
    /// specify a PUT route pattern using a replacement expression.
    /// </summary>
    /// <param name="routePattern">
    /// the words prefixed with @ will be replaced by property names of the `new` expression in the order they are specified.
    /// the replacement words do not have to match the request dto property names.
    /// <para><c>/invoice/{@id}/page/{@pageNum}</c></para></param>
    /// <param name="members"><c>r => new { r.InvoiceID, r.PageNumber }</c></param>
    protected void Put(string routePattern, Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.PUT);
        Routes(members.BuildRoute(routePattern));
    }

    /// <summary>
    /// configure custom model binding for this endpoint by supplying an IRequestBinder implementation.
    /// by calling this method, you're completely bypassing the built-in model binding and taking things into your own hands for this endpoint.
    /// </summary>
    /// <param name="binder">custom model binder implementation to use for this endpoint</param>
    protected void RequestBinder(IRequestBinder<TRequest> binder) => Definition.RequestBinder = binder;

    /// <summary>
    /// specify response caching settings for this endpoint
    /// </summary>
    /// <param name="durationSeconds">the duration in seconds for which the response is cached</param>
    /// <param name="location">the location where the data from a particular URL must be cached</param>
    /// <param name="noStore">specify whether the data should be stored or not</param>
    /// <param name="varyByHeader">the value for the Vary response header</param>
    /// <param name="varyByQueryKeys">the query keys to vary by</param>
    protected void ResponseCache(int durationSeconds, ResponseCacheLocation location = ResponseCacheLocation.Any, bool noStore = false, string? varyByHeader = null, string[]? varyByQueryKeys = null) => Definition.ResponseCache(durationSeconds, location, noStore, varyByHeader, varyByQueryKeys);

    /// <summary>
    /// configure a response interceptor to be called before the SendAsync response is sent to the browser.
    /// this will override any globally configured interceptor. if you return a response to the browser, then
    /// the rest of the SendAsync method will be skipped.
    /// </summary>
    /// <param name="responseInterceptor">the response interceptor to be executed</param>
    protected void ResponseInterceptor(IResponseInterceptor responseInterceptor) => Definition.ResponseInterceptor(responseInterceptor);

    /// <summary>
    /// allows access if the claims principal has ANY of the given roles
    /// </summary>
    /// <param name="rolesNames">one or more roles that has access</param>
    protected void Roles(params string[] rolesNames) => Definition.Roles(rolesNames);

    /// <summary>
    /// specify an override route prefix for this endpoint if a global route prefix is enabled.
    /// this is ignored if a global route prefix is not configured.
    /// global prefix can be ignored by setting <c>string.Empty</c>
    /// </summary>
    /// <param name="routePrefix">route prefix value</param>
    protected void RoutePrefixOverride(string routePrefix) => Definition.RoutePrefixOverride(routePrefix);

    /// <summary>
    /// specify one or more route patterns this endpoint should be listening for
    /// </summary>
    protected void Routes(params string[] patterns) => Definition.Routes = patterns;

    /// <summary>
    /// specify the json serializer context if code generation for request/response dtos is being used
    /// </summary>
    /// <typeparam name="TContext">the type of the json serializer context for this endpoint</typeparam>
    protected void SerializerContext<TContext>(TContext serializerContext) where TContext : JsonSerializerContext => Definition.SerializerContext = serializerContext;

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an action that sets values of an endpoint summary object</param>
    protected void Summary(Action<EndpointSummary> endpointSummary) => Definition.Summary(endpointSummary);

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an action that sets values of an endpoint summary object</param>
    protected void Summary(Action<EndpointSummary<TRequest>> endpointSummary) => Definition.Summary(endpointSummary);

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an endpoint summary instance</param>
    protected void Summary(EndpointSummary endpointSummary) => Definition.Summary(endpointSummary);

    /// <summary>
    /// specify one or more string tags for this endpoint so they can be used in the exclusion filter during registration.
    /// <para>HINT: these tags have nothing to do with swagger tags!</para>
    /// </summary>
    /// <param name="endpointTags">the tag values to associate with this endpoint</param>
    protected void Tags(params string[] endpointTags) => Definition.Tags(endpointTags);

    /// <summary>
    /// rate limit requests to this endpoint based on a request http header sent by the client.
    /// </summary>
    /// <param name="hitLimit">how many requests are allowed within the given duration</param>
    /// <param name="durationSeconds">the frequency in seconds where the accrued hit count should be reset</param>
    /// <param name="headerName">
    /// the name of the request header used to uniquely identify clients.
    /// header name can also be configured globally using <c>app.UseFastEndpoints(c=> c.ThrottleOptions...)</c>
    /// not specifying a header name will first look for 'X-Forwarded-For' header and if not present, will use `HttpContext.Connection.RemoteIpAddress`.
    /// </param>
    protected void Throttle(int hitLimit, double durationSeconds, string? headerName = null) => Definition.Throttle(hitLimit, durationSeconds, headerName);

    /// <summary>
    /// specify the validator that should be used for this endpoint.
    /// <para>TIP: you only need to call this method if you have more than one validator for the same request dto in the solution or if you just want to be explicit about what validator is used by the endpoint.</para>
    /// </summary>
    /// <typeparam name="TValidator">the type of the validator</typeparam>
    protected void Validator<TValidator>() where TValidator : IValidator => Definition.Validator<TValidator>();

    /// <summary>
    /// specify one or more http method verbs this endpoint should be accepting requests for
    /// </summary>
    protected void Verbs(params Http[] methods)
    {
        Verbs(methods.Select(m => m.ToString()).ToArray());
    }

    /// <summary>
    /// specify one or more http method verbs this endpoint should be accepting requests for
    /// </summary>
    public sealed override void Verbs(params string[] methods)
    {
        //note: this method is sealed to not allow user to override it because we neeed to perform
        //      the following setup activities, which require access to TRequest/TResponse

        Definition.Verbs = methods;

        //set default openapi descriptions
        Definition.InternalConfigAction = b =>
        {
            var tRequest = typeof(TRequest);
            var isPlainTextRequest = Types.IPlainTextRequest.IsAssignableFrom(tRequest);

            if (isPlainTextRequest)
            {
                b.Accepts<TRequest>("text/plain", "application/json");
                b.Produces<TResponse>(200, "text/plain", "application/json");
                return;
            }

            if (tRequest != Types.EmptyRequest)
            {
                if (methods.Any(m => m is "GET" or "HEAD" or "DELETE"))
                    b.Accepts<TRequest>("*/*", "application/json");
                else
                    b.Accepts<TRequest>("application/json");
            }

            if (tResponse == Types.Object || tResponse == Types.EmptyResponse)
                b.Produces<TResponse>(200, "text/plain", "application/json");
            else
                b.Produces<TResponse>(200, "application/json");

            if (Definition.AnonymousVerbs?.Any() is not true)
                b.Produces(401);

            if (Definition.RequiresAuthorization())
                b.Produces(403);

            if (FastEndpoints.Config.ErrOpts.ProducesMetadataType is not null && Definition.ValidatorType is not null)
            {
                b.Produces(
                    FastEndpoints.Config.ErrOpts.StatusCode,
                    FastEndpoints.Config.ErrOpts.ProducesMetadataType,
                    "application/problem+json");
            }
        };
    }

    /// <summary>
    /// specify the version of the endpoint if versioning is enabled
    /// </summary>
    /// <param name="version">the version of this endpoint</param>
    /// <param name="deprecateAt">the version group number starting at which this endpoint should not be included in swagger document</param>
    protected void Version(int version, int? deprecateAt = null) => Definition.EndpointVersion(version, deprecateAt);
}