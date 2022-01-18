using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : class, new() where TResponse : notnull, new()
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
    }

    /// <summary>
    /// specify one or more http method verbs this endpoint should be accepting requests for
    /// </summary>
    protected void Verbs(params Http[] methods)
    {
        Settings.Verbs = methods.Select(m => m.ToString()).ToArray();

        //default openapi descriptions
        Settings.InternalConfigAction = b =>
        {
            var tRequest = typeof(TRequest);
            var tResponse = typeof(TResponse);

            if (ReqTypeCache<TRequest>.IsPlainTextRequest)
            {
                b.Accepts<TRequest>("text/plain");
                b.Produces<TResponse>(200, "text/plain", "application/json");
                return;
            }

            if (tRequest != Types.EmptyRequest)
            {
                if (methods.Contains(Http.GET))
                    b.Accepts<TRequest>("*/*", "application/json");
                else
                    b.Accepts<TRequest>("application/json");
            }

            if (tResponse == Types.Object || tResponse == Types.EmptyResponse)
                b.Produces<TResponse>(200, "text/plain", "application/json");
            else
                b.Produces<TResponse>(200, "application/json");
        };
    }

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
            : Enum.GetNames(Types.Http);
    }

    /// <summary>
    /// enable file uploads with multipart/form-data content type
    /// </summary>
    protected void AllowFileUploads() => Settings.DtoTypeForFormData = typeof(TRequest);

    /// <summary>
    /// enable multipart/form-data submissions
    /// </summary>
    protected void AllowFormData() => Settings.DtoTypeForFormData = typeof(TRequest);

    /// <summary>
    /// specify one or more authorization policy names you have added to the middleware pipeline during app startup/ service configuration that should be applied to this endpoint.
    /// </summary>
    /// <param name="policyNames">one or more policy names (must have been added to the pipeline on startup)</param>
    protected void Policies(params string[] policyNames) => Settings.PreBuiltUserPolicies = policyNames;

    /// <summary>
    /// allows access if the claims principal has ANY of the given roles
    /// </summary>
    /// <param name="rolesNames">one or more roles that has access</param>
    protected void Roles(params string[] rolesNames) => Settings.Roles = rolesNames;

    /// <summary>
    /// allows access if the claims principal has ANY of the given permissions
    /// </summary>
    /// <param name="permissions">the permissions</param>
    protected void Permissions(params string[] permissions)
    {
        Settings.AllowAnyPermission = true;
        Settings.Permissions = permissions;
    }

    /// <summary>
    /// allows access if the claims principal has ALL of the given permissions
    /// </summary>
    /// <param name="permissions">the permissions</param>
    protected void PermissionsAll(params string[] permissions)
    {
        Settings.AllowAnyPermission = false;
        Settings.Permissions = permissions;
    }

    /// <summary>
    /// allows access if the claims principal has ANY of the given claim types
    /// </summary>
    /// <param name="claimTypes">the claim types</param>
    protected void Claims(params string[] claimTypes)
    {
        Settings.AllowAnyClaim = true;
        Settings.ClaimTypes = claimTypes;
    }

    /// <summary>
    /// allows access if the claims principal has ALL of the given claim types
    /// </summary>
    /// <param name="claimTypes">the claim types</param>
    protected void ClaimsAll(params string[] claimTypes)
    {
        Settings.AllowAnyClaim = false;
        Settings.ClaimTypes = claimTypes;
    }

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
    /// set endpoint configurations options using an endpoint builder action ///
    /// </summary>
    /// <param name="builder">the builder for this endpoint</param>
    protected void Options(Action<RouteHandlerBuilder> builder) => Settings.UserConfigAction = builder;

    /// <summary>
    /// describe openapi metadata for this endpoint. this method clears the default Accepts/Produces metadata.
    /// <c>b => b.Accepts&lt;Request&gt;("text/plain")</c>
    /// </summary>
    /// <param name="builder">the route handler builder for this endpoint</param>
    protected void Describe(Action<RouteHandlerBuilder> builder)
    {
        Action<RouteHandlerBuilder> clearDefaultsAction = b =>
        {
            b.Add(epBuilder =>
            {
                foreach (var m in epBuilder.Metadata.Where(
                    o => o.GetType().Name is "ProducesResponseTypeMetadata" or "AcceptsMetadata").ToArray())
                {
                    epBuilder.Metadata.Remove(m);
                }
            });
        };

        Settings.UserConfigAction = clearDefaultsAction + builder;
    }

    /// <summary>
    /// specify one or more string tags for this endpoint so they can be used in the exclusion filter during registration.
    /// </summary>
    /// <param name="endpointTags">the tag values to associate with this endpoint</param>
    protected void Tags(params string[] endpointTags)
    {
        Settings.Tags = endpointTags;
    }
}