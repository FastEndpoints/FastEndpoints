#if NET7_0_OR_GREATER
    using System.Security.Cryptography;
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

            if (opts.AddHeaderToResponse)
                ctx.HttpContext.Response.Headers.TryAdd(opts.HeaderName, idmpKey);

            if (!opts.IgnoreRequestBody)
            {
                var req = ctx.HttpContext.Request;

                if (req.HasFormContentType) //because multipart boundary info can be different in each request
                {
                    var sb = new StringBuilder();
                    var form = await req.ReadFormAsync(ct);

                    foreach (var f in form)
                        sb.Append(f.Key).Append(f.Value);

                    foreach (var file in form.Files)
                        sb.Append(file.Name).Append(file.FileName).Append(file.Length); //ignoring actual file content bytes

                    ctx.CacheVaryByRules.VaryByValues.Add("form", sb.ToString());

                    //remove 'Content-Type' header from cache-key participation due to boundary info being different for each request
                    opts.IsMultipartFormRequest ??= req.ContentType?.Contains("multipart/form-data") is true;
                }
                else
                {
                    req.EnableBuffering();
                    var hash = BitConverter.ToString(await SHA256.HashDataAsync(req.Body, ct));
                    ctx.CacheVaryByRules.VaryByValues.Add("body", hash);
                    req.Body.Position = 0;
                }
            }

            ctx.CacheVaryByRules.HeaderNames = new([opts.HeaderName, .. opts.AdditionalHeaders]);
            ctx.CacheVaryByRules.RouteValueNames = "*";
            ctx.CacheVaryByRules.QueryKeys = "*";
        }

        public ValueTask ServeFromCacheAsync(OutputCacheContext ctx, CancellationToken ct)
            => ValueTask.CompletedTask;

        public ValueTask ServeResponseAsync(OutputCacheContext ctx, CancellationToken ct)
            => ValueTask.CompletedTask;
    }
#endif