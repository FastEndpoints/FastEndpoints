using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

// ReSharper disable ArrangeAttributes

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> where TRequest : notnull
{
    static readonly Type _tRequest = typeof(TRequest);
    static readonly Type _tResponse = typeof(TResponse);
    static readonly bool _isStringResponse = _tResponse.IsAssignableFrom(Types.String);
    static readonly bool _isCollectionResponse = _tResponse.IsAssignableTo(Types.IEnumerable);

    // ReSharper disable once UnusedParameter.Global
    /// <summary>
    /// if the 'FastEndpoints.Generator' package is used, calling this method will generate a static class called '{assembly-name}.Auth.Allow'
    /// with a const field with this <paramref name="keyName" /> that has a 3 digit auto generated value (permission code). doesn't do anything without the
    /// source
    /// generator package installed.
    /// </summary>
    /// <param name="keyName">the name of the constant field to generate</param>
    /// <param name="behavior">
    /// specify whether to add the generated permission code as a permission requirement for this endpoint.
    /// this does the same thing as calling "Permissions(...)" method.
    /// i.e. if this optional argument is set to <see cref="Apply.ToThisEndpoint" />, then a user principal must possess this permission code
    /// in order to be allowed access to this endpoint. you don't need to explicitly specify it via a <c>Permissions(...)</c> call, when setting the
    /// <paramref name="behavior" />.
    /// </param>
    /// <param name="groupNames">
    /// optionally specify one or more groups (sets of permissions) this <paramref name="keyName" /> belongs to.
    /// for example if you want to generate a const/key called "Edit_Stock_Item" and want to assign it to an "Admin" group as well as a "Manager"
    /// group, you'd specify it with this parameter. The generated const/key is accessible via "Allow.Admin.Edit_Stock_Item" as well as
    /// "Allow.Manager.Edit_Stock_Item"
    /// </param>
    protected void AccessControl(string keyName, Apply? behavior = null, params string[] groupNames)
    {
        if (behavior is Apply.ToThisEndpoint)
            Definition.Permissions(GetAclHash(keyName));
    }

    /// <summary>
    /// if the 'FastEndpoints.Generator' package is used, calling this method will generate a static class called '{assembly-name}.Auth.Allow'
    /// with a const field with this <paramref name="keyName" /> that has a 3 digit auto generated value (permission code). doesn't do anything without the
    /// source
    /// generator package installed.
    /// </summary>
    /// <param name="keyName">the name of the constant field to generate</param>
    /// <param name="groupNames">
    /// optionally specify one or more groups (sets of permissions) this <paramref name="keyName" /> belongs to.
    /// for example if you want to generate a const/key called "Edit_Stock_Item" and want to assign it to an "Admin" group as well as a "Manager"
    /// group, you'd specify it with this parameter. The generated const/key is accessible via "Allow.Admin.Edit_Stock_Item" as well as
    /// "Allow.Manager.Edit_Stock_Item"
    /// </param>
    protected void AccessControl(string keyName, params string[] groupNames)
    {
        AccessControl(keyName, Apply.ToThisEndpoint, groupNames);
    }

    /// <summary>
    /// allow unauthenticated requests to this endpoint. optionally specify a set of verbs to allow unauthenticated access with.
    /// i.e. if the endpoint is listening to POST, PUT &amp; PATCH and you specify AllowAnonymous(Http.POST), then only PUT &amp; PATCH will require
    /// authentication.
    /// </summary>
    protected void AllowAnonymous(params Http[] verbs)
        => Definition.AllowAnonymous(verbs);

    /// <summary>
    /// allow unauthenticated requests to this endpoint for a specified set of http verbs.
    /// i.e. if the endpoint is listening to POST, PUT &amp; PATCH and you specify AllowAnonymous(Http.POST), then only PUT &amp; PATCH will require
    /// authentication.
    /// </summary>
    protected void AllowAnonymous(string[] verbs)
        => Definition.AllowAnonymous(verbs);

    /// <summary>
    /// enable file uploads with multipart/form-data content type
    /// </summary>
    /// <param name="dontAutoBindFormData">
    /// set 'true' to disable auto binding of form data which enables uploading and reading of large files without buffering to memory/disk.
    /// you can access the multipart sections for reading via the <see cref="Endpoint{TRequest,TResponse}.FormFileSectionsAsync" /> method.
    /// </param>
    protected void AllowFileUploads(bool dontAutoBindFormData = false)
        => Definition.AllowFileUploads(dontAutoBindFormData);

    /// <summary>
    /// enable form-data submissions
    /// </summary>
    /// <param name="urlEncoded">set to true to accept `application/x-www-form-urlencoded` content instead of `multipart/form-data` content.</param>
    protected void AllowFormData(bool urlEncoded = false)
        => Definition.AllowFormData(urlEncoded);

    /// <summary>
    /// specify which authentication schemes to use for authenticating requests to this endpoint
    /// </summary>
    /// <param name="authSchemeNames">the authentication scheme names</param>
    protected void AuthSchemes(params string[] authSchemeNames)
        => Definition.AuthSchemes(authSchemeNames);

    /// <summary>
    /// allows access if the claims principal has ANY of the given claim types
    /// </summary>
    /// <param name="claimTypes">the claim types</param>
    protected void Claims(params string[] claimTypes)
        => Definition.Claims(claimTypes);

    /// <summary>
    /// allows access if the claims principal has ALL the given claim types
    /// </summary>
    /// <param name="claimTypes">the claim types</param>
    protected void ClaimsAll(params string[] claimTypes)
        => Definition.ClaimsAll(claimTypes);

    /// <summary>
    /// specify to listen for CONNECT requests on one or more routes.
    /// </summary>
    protected void Connect([StringSyntax("Route")] [RouteTemplate] params string[] routePatterns)
    {
        Verbs(Http.CONNECT);
        Routes(routePatterns);
    }

    /// <summary>
    /// specify a CONNECT route pattern using a replacement expression.
    /// </summary>
    /// <param name="routePattern">
    /// the words prefixed with @ will be replaced by property names of the `new` expression in the order they are specified.
    /// the replacement words do not have to match the request dto property names.
    /// <para>
    ///     <c>/invoice/{@id}</c>
    /// </para>
    /// </param>
    /// <param name="members">
    ///     <c>r => new { r.InvoiceID }</c>
    /// </param>
    protected void Connect([StringSyntax("Route")] [RouteTemplate] string routePattern, Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.CONNECT);
        Routes(members.BuildRoute(routePattern));
    }

    /// <summary>
    /// specify to listen for DELETE requests on one or more routes.
    /// </summary>
    protected void Delete([StringSyntax("Route")] [RouteTemplate] params string[] routePatterns)
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
    /// <para>
    ///     <c>/invoice/{@id}/soft-delete</c>
    /// </para>
    /// </param>
    /// <param name="members">
    ///     <c>r => new { r.InvoiceID }</c>
    /// </param>
    protected void Delete([StringSyntax("Route")] [RouteTemplate] string routePattern,
                          Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.DELETE);
        Routes(members.BuildRoute(routePattern));
    }

    /// <summary>
    /// describe openapi metadata for this endpoint. optionally specify whether you want to clear the default Accepts/Produces metadata.
    /// <para>
    /// EXAMPLE: <c>b => b.Accepts&lt;Request&gt;("text/plain")</c>
    /// </para>
    /// </summary>
    /// <param name="builder">the route handler builder for this endpoint</param>
    /// <param name="clearDefaults">set to true if the defaults should be cleared</param>
    protected void Description(Action<RouteHandlerBuilder> builder, bool clearDefaults = false)
        => Definition.Description(builder, clearDefaults);

    /// <summary>
    /// disables auto sending of responses when the endpoint handler doesn't explicitly send a response. most useful for allowing a post-processor to
    /// handle sending of the response.
    /// </summary>
    protected void DontAutoSendResponse()
        => Definition.DontAutoSendResponse();

    /// <summary>
    /// if swagger auto tagging based on path segment is enabled, calling this method will prevent a tag from being added to this endpoint.
    /// </summary>
    protected void DontAutoTag()
        => Definition.DontAutoTag();

    /// <summary>
    /// use this only if you have your own exception catching middleware.
    /// if this method is called in config, an automatic error response will not be sent to the client by the library.
    /// all exceptions will be thrown, and it would be the responsibility of your exception catching middleware to handle them.
    /// </summary>
    protected void DontCatchExceptions()
        => Definition.DontCatchExceptions();

    /// <summary>
    /// disable auto validation failure responses (400 bad request with error details) for this endpoint.
    /// <para>HINT: this only applies to request dto validation.</para>
    /// </summary>
    protected void DontThrowIfValidationFails()
        => Definition.DontThrowIfValidationFails();

    /// <summary>
    /// enables antiforgery token verification for this endpoint
    /// </summary>
    protected void EnableAntiforgery()
        => Definition.EnableAntiforgery();

    /// <summary>
    /// specify a feature flag to run in order to determine if this endpoint is enabled or disabled for the current request.
    /// </summary>
    /// <typeparam name="TFlag">type of the feature flag</typeparam>
    /// <param name="featureName">optional name of the feature flag</param>
    protected void FeatureFlag<TFlag>(string? featureName = null) where TFlag : IFeatureFlag
        => Definition.FeatureFlag<TFlag>(featureName);

    /// <summary>
    /// specify to listen for GET requests on one or more routes.
    /// </summary>
    protected void Get([StringSyntax("Route")] [RouteTemplate] params string[] routePatterns)
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
    /// <para>
    ///     <c>/invoice/{@id}/print/{@pageNum}</c>
    /// </para>
    /// </param>
    /// <param name="members">
    ///     <c>r => new { r.InvoiceID, r.PageNumber }</c>
    /// </param>
    protected void Get([StringSyntax("Route")] [RouteTemplate] string routePattern,
                       Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.GET);
        Routes(members.BuildRoute(routePattern));
    }

    /// <summary>
    /// if this endpoint is part of an endpoint group, specify the type of the <see cref="FastEndpoints.Group" /> concrete class where the common
    /// configuration for the group is specified.
    /// <para>
    /// WARNING: this method can only be called after the endpoint route has been specified.
    /// </para>
    /// </summary>
    /// <typeparam name="TEndpointGroup">the type of your <see cref="FastEndpoints.Group" /> concrete class</typeparam>
    /// <exception cref="InvalidOperationException">thrown if endpoint route hasn't yet been specified</exception>
    protected sealed override void Group<TEndpointGroup>()
        => Definition.Group<TEndpointGroup>();

    /// <summary>
    /// specify to listen for HEAD requests on one or more routes.
    /// </summary>
    protected void Head([StringSyntax("Route")] [RouteTemplate] params string[] routePatterns)
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
    /// <para>
    ///     <c>/invoice/{@id}/print/{@pageNum}</c>
    /// </para>
    /// </param>
    /// <param name="members">
    ///     <c>r => new { r.InvoiceID, r.PageNumber }</c>
    /// </param>
    protected void Head([StringSyntax("Route")] [RouteTemplate] string routePattern,
                        Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.HEAD);
        Routes(members.BuildRoute(routePattern));
    }

    /// <summary>
    /// specify idempotency requirements for this endpoint
    /// </summary>
    /// <param name="options">the idempotency options</param>
    protected void Idempotency(Action<IdempotencyOptions>? options = null)
        => Definition.Idempotency(options);

    /// <summary>
    /// register metadata objects for the endpoint. these will be auto added to the endpoint metadata collection during startup.
    /// </summary>
    /// <param name="metadata"></param>
    protected void Metadata(params object[] metadata)
        => Definition.Metadata(metadata);

    /// <summary>
    /// specify a custom maximum request body size to be set on <see cref="IHttpMaxRequestBodySizeFeature.MaxRequestBodySize" /> which would apply to this particular
    /// endpoint only. typically useful with <see cref="AllowFormData" /> and <see cref="AllowFileUploads" />.
    /// </summary>
    /// <param name="size"></param>
    protected void MaxRequestBodySize(long size)
        => Definition.MaxRequestBodySize(size);

    /// <summary>
    /// set endpoint configurations options using an endpoint builder action ///
    /// </summary>
    /// <param name="builder">the builder for this endpoint</param>
    protected void Options(Action<RouteHandlerBuilder> builder)
        => Definition.Options(builder);

    /// <summary>
    /// specify to listen for OPTIONS requests on one or more routes.
    /// </summary>
    protected void Options([StringSyntax("Route")] [RouteTemplate] params string[] routePatterns)
    {
        Verbs(Http.OPTIONS);
        Routes(routePatterns);
    }

    /// <summary>
    /// specify a OPTIONS route pattern using a replacement expression.
    /// </summary>
    /// <param name="routePattern">
    /// the words prefixed with @ will be replaced by property names of the `new` expression in the order they are specified.
    /// the replacement words do not have to match the request dto property names.
    /// <para>
    ///     <c>/invoice/{@id}</c>
    /// </para>
    /// </param>
    /// <param name="members">
    ///     <c>r => new { r.InvoiceID }</c>
    /// </param>
    protected void Options([StringSyntax("Route")] [RouteTemplate] string routePattern, Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.OPTIONS);
        Routes(members.BuildRoute(routePattern));
    }

    /// <summary>
    /// specify to listen for PATCH requests on one or more routes.
    /// </summary>
    protected void Patch([StringSyntax("Route")] [RouteTemplate] params string[] routePatterns)
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
    /// <para>
    ///     <c>/invoice/{@id}</c>
    /// </para>
    /// </param>
    /// <param name="members">
    ///     <c>r => new { r.InvoiceID }</c>
    /// </param>
    protected void Patch([StringSyntax("Route")] [RouteTemplate] string routePattern,
                         Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.PATCH);
        Routes(members.BuildRoute(routePattern));
    }

    /// <summary>
    /// allows access if the claims principal has ANY of the given permissions
    /// </summary>
    /// <param name="permissions">the permissions</param>
    protected void Permissions(params string[] permissions)
        => Definition.Permissions(permissions);

    /// <summary>
    /// allows access if the claims principal has ALL the given permissions
    /// </summary>
    /// <param name="permissions">the permissions</param>
    protected void PermissionsAll(params string[] permissions)
        => Definition.PermissionsAll(permissions);

    /// <summary>
    /// allows access if the 'scope' claim has ANY of the given scopes
    /// </summary>
    /// <param name="scopes">the scopes</param>
    protected void Scopes(params string[] scopes)
        => Definition.Scopes(scopes);

    /// <summary>
    /// allows access if the 'scope' claim has ALL the given scopes
    /// </summary>
    /// <param name="scopes">the scopes</param>
    protected void ScopesAll(params string[] scopes)
        => Definition.ScopesAll(scopes);

    /// <summary>
    /// specify an action for building an authorization requirement which applies only to this endpoint.
    /// </summary>
    /// <param name="policy">the policy builder action</param>
    protected void Policy(Action<AuthorizationPolicyBuilder> policy)
        => Definition.Policy(policy);

    /// <summary>
    /// specify one or more authorization policy names you have added to the middleware pipeline during app startup/ service configuration that should be
    /// applied to this endpoint.
    /// </summary>
    /// <param name="policyNames">one or more policy names (must have been added to the pipeline on startup)</param>
    protected void Policies(params string[] policyNames)
        => Definition.Policies(policyNames);

    /// <summary>
    /// specify to listen for POST requests on one or more routes.
    /// </summary>
    protected void Post([StringSyntax("Route")] [RouteTemplate] params string[] routePatterns)
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
    /// <para>
    ///     <c>/invoice/{@id}/page/{@pageNum}</c>
    /// </para>
    /// </param>
    /// <param name="members">
    ///     <c>r => new { r.InvoiceID, r.PageNumber }</c>
    /// </param>
    protected void Post([StringSyntax("Route")] [RouteTemplate] string routePattern,
                        Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.POST);
        Routes(members.BuildRoute(routePattern));
    }

    //this int is not applicable for endpoint level processor ordering
    //because Order.After below ultimately causes an append via list.Add()
    static int _unused;

    /// <summary>
    /// configure a  post-processor to be executed after the main handler function is done. call this method multiple times to add multiple post-processors.
    /// processors are executed in the order they are configured in the endpoint.
    /// </summary>
    /// <typeparam name="TPostProcessor">the post-processor to add</typeparam>
    public void PostProcessor<TPostProcessor>() where TPostProcessor : class, IPostProcessor<TRequest, TResponse>
    {
        Definition.ThrowIfLocked();
        EndpointDefinition.AddProcessor<TPostProcessor>(Order.After, Definition.PostProcessorList, ref _unused);
    }

    /// <summary>
    /// configure a collection of post-processors to be executed after the main handler function is done. processors are executed in the order they are  defined
    /// here.
    /// </summary>
    /// <param name="postProcessors">the post processors to be executed</param>
    public void PostProcessors(params IPostProcessor<TRequest, TResponse>[] postProcessors)
    {
        Definition.ThrowIfLocked();
        EndpointDefinition.AddProcessors(Order.After, postProcessors, Definition.PostProcessorList, ref _unused);
    }

    /// <summary>
    /// configure a  pre-processor to be executed before the main handler function is called. call this method multiple times to add multiple pre-processors.
    /// processors are executed in the order they are configured in the endpoint.
    /// </summary>
    /// <typeparam name="TPreProcessor">the pre-processor to add</typeparam>
    public void PreProcessor<TPreProcessor>() where TPreProcessor : class, IPreProcessor<TRequest>
    {
        Definition.ThrowIfLocked();
        EndpointDefinition.AddProcessor<TPreProcessor>(Order.After, Definition.PreProcessorList, ref _unused);
    }

    /// <summary>
    /// configure a collection of pre-processors to be executed before the main handler function is called. processors are executed in the order they are defined
    /// here.
    /// </summary>
    /// <param name="preProcessors">the pre-processors to be executed</param>
    public void PreProcessors(params IPreProcessor<TRequest>[] preProcessors)
    {
        Definition.ThrowIfLocked();
        EndpointDefinition.AddProcessors(Order.After, preProcessors, Definition.PreProcessorList, ref _unused);
    }

    /// <summary>
    /// specify to listen for PUT requests on one or more routes.
    /// </summary>
    protected void Put([StringSyntax("Route")] [RouteTemplate] params string[] routePatterns)
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
    /// <para>
    ///     <c>/invoice/{@id}/page/{@pageNum}</c>
    /// </para>
    /// </param>
    /// <param name="members">
    ///     <c>r => new { r.InvoiceID, r.PageNumber }</c>
    /// </param>
    protected void Put([StringSyntax("Route")] [RouteTemplate] string routePattern,
                       Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.PUT);
        Routes(members.BuildRoute(routePattern));
    }

    /// <summary>
    /// configure custom model binding for this endpoint by supplying an IRequestBinder implementation.
    /// by calling this method, you're completely bypassing the built-in model binding and taking things into your own hands for this endpoint.
    /// </summary>
    /// <param name="binder">custom model binder implementation to use for this endpoint</param>
    protected void RequestBinder(IRequestBinder<TRequest> binder)
    {
        Definition.ThrowIfLocked();
        Definition.EpRequestBinder = binder;
    }

    /// <summary>
    /// specify response caching settings for this endpoint
    /// </summary>
    /// <param name="durationSeconds">the duration in seconds for which the response is cached</param>
    /// <param name="location">the location where the data from a particular URL must be cached</param>
    /// <param name="noStore">specify whether the data should be stored or not</param>
    /// <param name="varyByHeader">the value for the Vary response header</param>
    /// <param name="varyByQueryKeys">the query keys to vary by</param>
    protected void ResponseCache(int durationSeconds,
                                 ResponseCacheLocation location = ResponseCacheLocation.Any,
                                 bool noStore = false,
                                 string? varyByHeader = null,
                                 string[]? varyByQueryKeys = null)
        => Definition.ResponseCache(durationSeconds, location, noStore, varyByHeader, varyByQueryKeys);

    /// <summary>
    /// configure a response interceptor to be called before the SendAsync response is sent to the browser.
    /// this will override any globally configured interceptor. if you return a response to the browser, then
    /// the rest of the SendAsync method will be skipped.
    /// </summary>
    /// <param name="responseInterceptor">the response interceptor to be executed</param>
    protected void ResponseInterceptor(IResponseInterceptor responseInterceptor)
        => Definition.ResponseInterceptor(responseInterceptor);

    /// <summary>
    /// allows access if the claims principal has ANY of the given roles
    /// </summary>
    /// <param name="rolesNames">one or more roles that has access</param>
    protected void Roles(params string[] rolesNames)
        => Definition.Roles(rolesNames);

    /// <summary>
    /// specify an override route prefix for this endpoint if a global route prefix is enabled.
    /// this is ignored if a global route prefix is not configured.
    /// global prefix can be ignored by setting <c>string.Empty</c>
    /// </summary>
    /// <param name="routePrefix">route prefix value</param>
    protected void RoutePrefixOverride(string routePrefix)
        => Definition.RoutePrefixOverride(routePrefix);

    /// <summary>
    /// specify one or more route patterns this endpoint should be listening for
    /// </summary>
    public override void Routes([StringSyntax("Route")] [RouteTemplate] params string[] patterns)
    {
        Definition.ThrowIfLocked();
        Definition.Routes = patterns;
    }

    /// <summary>
    /// specify the json serializer context if code generation for request/response dtos is being used by supplying your own instance.
    /// </summary>
    /// <typeparam name="TContext">the type of the json serializer context for this endpoint</typeparam>
    protected void SerializerContext<TContext>(TContext serializerContext) where TContext : JsonSerializerContext
    {
        Definition.ThrowIfLocked();
        Definition.SerializerContext = serializerContext;
        AddCtxToGlobalChain();
    }

    /// <summary>
    /// specify the type of the json serializer context if code generation for request/response dtos is being used.
    /// the json serializer context will be instantiated with <see cref="SerializerOptions" /> from the UseFastEndpoints(...) call.
    /// </summary>
    /// <typeparam name="TContext">the type of the json serializer context for this endpoint</typeparam>
    protected void SerializerContext<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TContext>()
        where TContext : JsonSerializerContext
    {
        Definition.ThrowIfLocked();
        Definition.SerializerContext = (TContext?)Activator.CreateInstance(typeof(TContext), new JsonSerializerOptions(Cfg.SerOpts.Options));
        AddCtxToGlobalChain();
    }

    void AddCtxToGlobalChain()
    {
        if (Definition.SerializerContext is not null)
        {
            if (!Cfg.SerOpts.Options.IsReadOnly)
                Cfg.SerOpts.Options.TypeInfoResolverChain.Insert(0, Definition.SerializerContext);

            if (Cfg.SerOpts.AspNetCoreOptions is { IsReadOnly: false })
                Cfg.SerOpts.AspNetCoreOptions.TypeInfoResolverChain.Insert(0, Definition.SerializerContext);
        }
    }

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an action that sets values of an endpoint summary object</param>
    protected void Summary(Action<EndpointSummary> endpointSummary)
        => Definition.Summary(endpointSummary);

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an action that sets values of an endpoint summary object</param>
#if NET9_0_OR_GREATER
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
#endif
    protected void Summary(Action<EndpointSummary<TRequest>> endpointSummary)
        => Definition.Summary(endpointSummary);

    /// <summary>
    /// provide a summary/description for this endpoint to be used in swagger/ openapi
    /// </summary>
    /// <param name="endpointSummary">an endpoint summary instance</param>
    protected void Summary(EndpointSummary endpointSummary)
        => Definition.Summary(endpointSummary);

    /// <summary>
    /// specify one or more string tags for this endpoint so they can be used in the exclusion filter during registration.
    /// <para>HINT: these tags have nothing to do with swagger tags!</para>
    /// </summary>
    /// <param name="endpointTags">the tag values to associate with this endpoint</param>
    protected void Tags(params string[] endpointTags)
        => Definition.Tags(endpointTags);

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
    protected void Throttle(int hitLimit, double durationSeconds, string? headerName = null)
        => Definition.Throttle(hitLimit, durationSeconds, headerName);

    /// <summary>
    /// specify to listen for TRACE requests on one or more routes.
    /// </summary>
    protected void Trace([StringSyntax("Route")] [RouteTemplate] params string[] routePatterns)
    {
        Verbs(Http.TRACE);
        Routes(routePatterns);
    }

    /// <summary>
    /// specify a TRACE route pattern using a replacement expression.
    /// </summary>
    /// <param name="routePattern">
    /// the words prefixed with @ will be replaced by property names of the `new` expression in the order they are specified.
    /// the replacement words do not have to match the request dto property names.
    /// <para>
    ///     <c>/invoice/{@id}</c>
    /// </para>
    /// </param>
    /// <param name="members">
    ///     <c>r => new { r.InvoiceID }</c>
    /// </param>
    protected void Trace([StringSyntax("Route")] [RouteTemplate] string routePattern, Expression<Func<TRequest, object>> members)
    {
        Verbs(Http.TRACE);
        Routes(members.BuildRoute(routePattern));
    }

    /// <summary>
    /// specify the validator that should be used for this endpoint.
    /// <para>
    /// TIP: you only need to call this method if you have more than one validator for the same request dto in the solution or if you just want to be
    /// explicit about what validator is used by the endpoint.
    /// </para>
    /// </summary>
    /// <typeparam name="TValidator">the type of the validator</typeparam>
    protected void Validator<TValidator>() where TValidator : IValidator
        => Definition.Validator<TValidator>();

    /// <summary>
    /// specify one or more http method verbs this endpoint should be accepting requests for
    /// </summary>
    public void Verbs(params Http[] methods)
        => Verbs(methods.Select(m => m.ToString()).ToArray());

    /// <summary>
    /// specify one or more http method verbs this endpoint should be accepting requests for
    /// </summary>
    public override void Verbs(params string[] methods)
    {
        Definition.ThrowIfLocked();
        Definition.Verbs = methods;
    }

    /// <summary>
    /// specify the version of this endpoint.
    /// </summary>
    /// <param name="version">the version of this endpoint</param>
    /// <param name="deprecateAt">the version number starting at which this endpoint should not be included in swagger document</param>
    protected EpVersion Version(int version, int deprecateAt = 0)
        => Definition.EndpointVersion(version, deprecateAt);
}

[UnconditionalSuppressMessage("aot", "IL2060"), UnconditionalSuppressMessage("aot", "IL3050")]
static class ProducesMetaForResultOfResponse
{
    static readonly MethodInfo _populateMethod =
        typeof(ProducesMetaForResultOfResponse).GetMethod(nameof(Populate), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static void AddMetadata(EndpointBuilder builder, Type tResponse)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (tResponse is not null && typeof(IEndpointMetadataProvider).IsAssignableFrom(tResponse))
        {
            var invokeArgs = new object[] { builder };
            _populateMethod.MakeGenericMethod(tResponse).Invoke(null, invokeArgs);
        }
    }

    static void Populate<T>(EndpointBuilder b) where T : IEndpointMetadataProvider
    {
        T.PopulateMetadata(_populateMethod, b);
    }
}