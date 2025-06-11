using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query;

namespace FastEndpoints.OData;

/// <summary>
/// implement this abstract class and override the <c>ExecuteAsync()</c> method to create OData endpoints.
/// </summary>
/// <typeparam name="TEntity">the type of the ef core entity of the OData endpoint</typeparam>
public abstract class ODataEndpoint<TEntity> : Endpoint<ODataQueryOptions<TEntity>, IQueryable>
{
    /// <summary>
    /// implement this method to configure this endpoint instead of the usual <see cref="BaseEndpoint.Configure" /> method.
    /// </summary>
    protected abstract void Setup();

    public sealed override void Configure()
    {
        RequestBinder(new ODataBinder());
        DontAutoSendResponse();
        Description(
            x => x.ClearDefaultAccepts()
                  .ClearDefaultProduces()
                  .Produces<ODataResult<TEntity>>(200, "application/json")); //not sure if this is the correct thing to do with swagger
        Options(
            x =>
            {
                x.WithODataResult();
                x.AddEndpointFilter(
                    async (ctx, next) =>
                    {
                        await next(ctx);

                        return ctx.HttpContext.Items.TryGetValue("FastEndpointsResponse", out var res)
                                   ? res
                                   : null;
                    });
            });
        Setup();
    }

    [NotImplemented]
    public sealed override Task HandleAsync(ODataQueryOptions<TEntity> _, CancellationToken __)
        => throw new NotSupportedException();

    class ODataBinder : IRequestBinder<ODataQueryOptions<TEntity>>
    {
        public ValueTask<ODataQueryOptions<TEntity>> BindAsync(BinderContext ctx, CancellationToken ct)
            => ODataQueryOptions<TEntity>.BindAsync(ctx.HttpContext, new ODataParamInfo());
    }
}