using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : notnull, new() where TResponse : notnull, new()
{
    /// <summary>
    /// specify to listen for GET requests on one or more routes.
    /// </summary>
    protected void Get(params string[] routePatterns)
    {
        Verbs(Http.GET);
        Routes(routePatterns);
    }
    /// <summary>
    /// specify to listen for POST requests on one or more routes.
    /// </summary>
    protected void Post(params string[] routePatterns)
    {
        Verbs(Http.POST);
        Routes(routePatterns);
    }
    /// <summary>
    /// specify to listen for PUT requests on one or more routes.
    /// </summary>
    protected void Put(params string[] routePatterns)
    {
        Verbs(Http.PUT);
        Routes(routePatterns);
    }
    /// <summary>
    /// specify to listen for PATCH requests on one or more routes.
    /// </summary>
    protected void Patch(params string[] routePatterns)
    {
        Verbs(Http.PATCH);
        Routes(routePatterns);
    }
    /// <summary>
    /// specify to listen for DELETE requests on one or more routes.
    /// </summary>
    protected void Delete(params string[] routePatterns)
    {
        Verbs(Http.DELETE);
        Routes(routePatterns);
    }
    /// <summary>
    /// specify one or more route patterns this endpoint should be listening for
    /// </summary>
    protected void Routes(params string[] patterns)
    {
        Settings.Routes = patterns;
        Settings.InternalConfigAction = b =>
        {
            if (typeof(TRequest) != typeof(EmptyRequest)) b.Accepts<TRequest>("application/json");
            b.Produces<TResponse>();
        };
    }
    /// <summary>
    /// specify one or more http method verbs this endpoint should be accepting requests for
    /// </summary>
    protected void Verbs(params Http[] methods) => Settings.Verbs = methods.Select(m => m.ToString()).ToArray();
    /// <summary>
    /// disable auto validation failure responses (400 bad request with error details) for this endpoint
    /// </summary>
    protected void DontThrowIfValidationFails() => Settings.ThrowIfValidationFails = false;
    /// <summary>
    /// allow unauthenticated requests to this endpoint. optionally specify a set of verbs to allow unauthenticated access with.
    /// i.e. if the endpoint is listening to POST, PUT &amp; PATCH and you specify AllowAnonymous(Http.POST), then only PUT &amp; PATCH will require authentication.
    /// </summary>
    protected void AllowAnonymous(params Http[] verbs)
    {
        Settings.AnonymousVerbs =
            verbs.Length > 0
            ? verbs.Select(v => v.ToString()).ToArray()
            : Enum.GetNames(typeof(Http));
    }
    /// <summary>
    /// enable file uploads with multipart/form-data content type
    /// </summary>
    protected void AllowFileUploads() => Settings.AllowFileUploads = true;
    /// <summary>
    /// specify one or more authorization policy names you have added to the middleware pipeline during app startup/ service configuration that should be applied to this endpoint.
    /// </summary>
    /// <param name="policyNames">one or more policy names (must have been added to the pipeline on startup)</param>
    protected void Policies(params string[] policyNames) => Settings.PreBuiltUserPolicies = policyNames;
    /// <summary>
    /// specify that the current claim principal/ user should posses at least one of the roles (claim type) mentioned here. access will be forbidden if the user doesn't have any of the specified roles.
    /// </summary>
    /// <param name="rolesNames">one or more roles that has access</param>
    protected void Roles(params string[] rolesNames) => Settings.Roles = rolesNames;
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
        Settings.AllowAnyPermission = allowAny;
        Settings.Permissions = permissions;
    }
    /// <summary>
    /// specify to allow access if the user has any of the given permissions
    /// </summary>
    /// <param name="permissions">the permissions</param>
    protected void AnyPermission(params string[] permissions) => Permissions(true, permissions);
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
        Settings.AllowAnyClaim = allowAny;
        Settings.Claims = claims;
    }
    /// <summary>
    /// specify to allow access if the user has any of the given claims
    /// </summary>
    /// <param name="claims">the claims</param>
    protected void AnyClaim(params string[] claims) => Claims(true, claims);
    /// <summary>
    /// configure a collection of pre-processors to be executed before the main handler function is called. processors are executed in the order they are defined here.
    /// </summary>
    /// <param name="preProcessors">the pre processors to be executed</param>
    protected void PreProcessors(params IPreProcessor<TRequest>[] preProcessors) => Settings.PreProcessors = preProcessors;
    /// <summary>
    /// configure a collection of post-processors to be executed after the main handler function is done. processors are executed in the order they are defined here.
    /// </summary>
    /// <param name="postProcessors">the post processors to be executed</param>
    protected void PostProcessors(params IPostProcessor<TRequest, TResponse>[] postProcessors) => Settings.PostProcessors = postProcessors;
    /// <summary>
    /// specify response caching settings for this endpoint
    /// </summary>
    /// <param name="durationSeconds">the duration in seconds for which the response is cached</param>
    /// <param name="location">the location where the data from a particular URL must be cached</param>
    /// <param name="noStore">specify whether the data should be stored or not</param>
    /// <param name="varyByHeader">the value for the Vary response header</param>
    /// <param name="varyByQueryKeys">the query keys to vary by</param>
    protected void ResponseCache(int durationSeconds, ResponseCacheLocation location = ResponseCacheLocation.Any, bool noStore = false, string? varyByHeader = null, string[]? varyByQueryKeys = null)
    {
        Settings.ResponseCacheSettings = new()
        {
            Duration = durationSeconds,
            Location = location,
            NoStore = noStore,
            VaryByHeader = varyByHeader,
            VaryByQueryKeys = varyByQueryKeys
        };
    }
    /// <summary>
    /// set endpoint configurations options using an endpoint builder action
    /// </summary>
    /// <param name="builder">the builder for this endpoint</param>
    protected void Options(Action<RouteHandlerBuilder> builder) => Settings.UserConfigAction = builder;
}

