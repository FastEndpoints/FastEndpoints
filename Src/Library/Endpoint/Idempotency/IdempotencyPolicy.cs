#if NET7_0_OR_GREATER
    using System.Text;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.OutputCaching;
    using Microsoft.Extensions.Primitives;

    namespace FastEndpoints;

    sealed class IdempotencyPolicy : IOutputCachePolicy
    {
        public async ValueTask CacheRequestAsync(OutputCacheContext ctx, CancellationToken ct)
        {
            var opts = ctx.HttpContext.GetEndpoint()?.Metadata.OfType<EndpointDefinition>().SingleOrDefault()?.IdempotencyOptions;

            if (opts is null)
            {
                ctx.EnableOutputCaching = false;
                ctx.AllowCacheLookup = false;
                ctx.AllowCacheStorage = false;
                ctx.AllowLocking = false;

                return;
            }

            if (!ctx.HttpContext.Request.Headers.TryGetValue(opts.HeaderName, out var idmpKey) || StringValues.IsNullOrEmpty(idmpKey))
            {
                ctx.EnableOutputCaching = false;

                if (idmpKey.Count > 1)
                {
                    await ctx.HttpContext.Response.SendErrorsAsync(
                        [new(Cfg.ErrOpts.GeneralErrorsField, "Multiple idempotency headers not allowed!")],
                        cancellation: ct);

                    return;
                }

                await ctx.HttpContext.Response.SendErrorsAsync(
                    [new(Cfg.ErrOpts.GeneralErrorsField, $"Idempotency header [{opts.HeaderName}] is required!")],
                    cancellation: ct);

                return;
            }

            ctx.EnableOutputCaching = true;
            ctx.AllowCacheLookup = true;
            ctx.AllowCacheStorage = true;
            ctx.AllowLocking = true;
            ctx.ResponseExpirationTimeSpan = opts.CacheDuration;
            ctx.CacheVaryByRules.HeaderNames = new([opts.HeaderName, .. opts.AdditionalHeaders]);
            ctx.CacheVaryByRules.QueryKeys = "*";
            ctx.CacheVaryByRules.RouteValueNames = "*";

            if (opts.AddHeaderToResponse)
                ctx.HttpContext.Response.Headers.TryAdd(opts.HeaderName, idmpKey);

            if (!opts.IgnoreRequestBody)
            {
                var req = ctx.HttpContext.Request;
                var sb = new StringBuilder();

                if (req.HasFormContentType)
                {
                    foreach (var f in req.Form)
                        sb.Append(f.Key).Append(f.Value);

                    foreach (var file in req.Form.Files)
                        sb.Append(file.Name).Append(file.FileName).Append(file.Length);
                }
                else
                {
                    req.EnableBuffering();
                    using var reader = new StreamReader(req.Body, leaveOpen: true);
                    sb.Append(await reader.ReadToEndAsync(ct));
                    req.Body.Position = 0;
                }

                ctx.CacheVaryByRules.VaryByValues.Add("body", sb.ToString());
            }
        }

        public ValueTask ServeFromCacheAsync(OutputCacheContext ctx, CancellationToken ct)
            => ValueTask.CompletedTask;

        public ValueTask ServeResponseAsync(OutputCacheContext ctx, CancellationToken ct)
            => ValueTask.CompletedTask;
    }
#endif