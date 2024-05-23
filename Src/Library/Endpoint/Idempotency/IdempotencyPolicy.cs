#if NET7_0_OR_GREATER
    using System.Buffers;
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
                return;

            ctx.EnableOutputCaching = false;
            ctx.AllowCacheLookup = false;
            ctx.AllowCacheStorage = false;
            ctx.AllowLocking = false;

            ctx.HttpContext.Request.Headers.TryGetValue(opts.HeaderName, out var idmpKey);

            if (StringValues.IsNullOrEmpty(idmpKey))
            {
                await ctx.HttpContext.Response.SendErrorsAsync(
                    [new(Cfg.ErrOpts.GeneralErrorsField, $"Idempotency header [{opts.HeaderName}] is required!")],
                    cancellation: ct);

                return;
            }

            if (idmpKey.Count > 1)
            {
                await ctx.HttpContext.Response.SendErrorsAsync(
                    [new(Cfg.ErrOpts.GeneralErrorsField, "Multiple idempotency headers not allowed!")],
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

            SHA256? hasher = null;

            if (!opts.IgnoreRequestBody)
            {
                hasher = SHA256.Create();
                var req = ctx.HttpContext.Request;

                if (req.HasFormContentType) //because multipart boundary info can be different in each request
                {
                    var form = await req.ReadFormAsync(ct);

                    foreach (var f in form)
                    {
                        var val = Encoding.UTF8.GetBytes($"{f.Key}{f.Value}");
                        hasher.TransformBlock(val, 0, val.Length, null, 0);
                    }

                    foreach (var f in form.Files)
                    {
                        var val = Encoding.UTF8.GetBytes($"{f.Name}{f.FileName}{f.Length}");
                        hasher.TransformBlock(val, 0, val.Length, null, 0);
                    }

                    //this removes 'Content-Type' header from cache-key participation.(because the boundary info is different for each request)
                    opts.IsMultipartFormRequest ??= req.ContentType?.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase) is true;
                }
                else
                {
                    req.EnableBuffering();
                    req.Body.Position = 0;

                    var buffer = ArrayPool<byte>.Shared.Rent(4096);
                    int bytesRead;

                    while ((bytesRead = await req.Body.ReadAsync(buffer, ct)) > 0)
                        hasher.TransformBlock(buffer, 0, bytesRead, null, 0);

                    ArrayPool<byte>.Shared.Return(buffer);
                    req.Body.Position = 0;
                }
            }

            ctx.CacheVaryByRules.RouteValueNames = "*";
            ctx.CacheVaryByRules.QueryKeys = "*";
            ctx.CacheVaryByRules.HeaderNames = new([opts.HeaderName, .. opts.AdditionalHeaders]);

            if (hasher is not null)
            {
                hasher.TransformFinalBlock([], 0, 0);

                if (hasher.Hash is not null)
                    ctx.CacheVaryByRules.VaryByValues.Add("body", BitConverter.ToString(hasher.Hash));

                ctx.HttpContext.Response.RegisterForDispose(hasher);
            }
        }

        public ValueTask ServeFromCacheAsync(OutputCacheContext ctx, CancellationToken ct)
            => ValueTask.CompletedTask;

        public ValueTask ServeResponseAsync(OutputCacheContext ctx, CancellationToken ct)
            => ValueTask.CompletedTask;
    }
#endif