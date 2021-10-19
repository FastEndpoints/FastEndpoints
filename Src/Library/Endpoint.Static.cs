using FastEndpoints.Validation;
using FastEndpoints.Validation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : notnull, new() where TResponse : notnull, new()
{
    private static async Task<TRequest> BindIncomingDataAsync(HttpContext ctx, CancellationToken cancellation)
    {
        TRequest? req = default;

        if (ctx.Request.HasJsonContentType())
            req = await ctx.Request.ReadFromJsonAsync<TRequest>(SerializerOptions, cancellation).ConfigureAwait(false);

        if (req is null) req = new();

        BindFromFormValues(req, ctx.Request);

        BindFromRouteValues(req, ctx.Request.RouteValues);

        BindFromQueryParams(req, ctx.Request.Query);

        return req;
    }

    private static async Task ValidateRequestAsync(TRequest req, IValidator<TRequest>? validator, HttpContext ctx, object? preProcessors, List<ValidationFailure> validationFailures, CancellationToken cancellation)
    {
        if (validator is null) return;

        var valResult = await validator.ValidateAsync(req, cancellation).ConfigureAwait(false);

        if (!valResult.IsValid)
            validationFailures.AddRange(valResult.Errors);

        if (validationFailures.Count > 0 && ((IValidatorWithState)validator).ThrowIfValidationFails)
        {
            await RunPreprocessors(preProcessors, req, ctx, validationFailures, cancellation).ConfigureAwait(false);
            throw new ValidationFailureException();
        }
    }

    private static async Task RunPostProcessors(object? postProcessors, TRequest req, TResponse resp, HttpContext ctx, List<ValidationFailure> validationFailures, CancellationToken cancellation)
    {
        if (postProcessors is not null)
        {
            foreach (var pp in (IPostProcessor<TRequest, TResponse>[])postProcessors)
                await pp.PostProcessAsync(req, resp, ctx, validationFailures, cancellation).ConfigureAwait(false);
        }
    }

    private static async Task RunPreprocessors(object? preProcessors, TRequest req, HttpContext ctx, List<ValidationFailure> validationFailures, CancellationToken cancellation)
    {
        if (preProcessors is not null)
        {
            foreach (var p in (IPreProcessor<TRequest>[])preProcessors)
                await p.PreProcessAsync(req, ctx, validationFailures, cancellation).ConfigureAwait(false);
        }
    }

    private static void BindFromFormValues(TRequest req, HttpRequest httpRequest)
    {
        if (!httpRequest.HasFormContentType) return;

        var formFields = httpRequest.Form.Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value[0])).ToArray();

        for (int x = 0; x < formFields.Length; x++)
            Bind(req, formFields[x]);

        for (int y = 0; y < httpRequest.Form.Files.Count; y++)
        {
            if (ReqTypeCache<TRequest>.CachedProps.TryGetValue(httpRequest.Form.Files[y].Name.ToLower(), out var prop))
            {
                if (prop.PropInfo.PropertyType == typeof(IFormFile))
                    prop.PropInfo.SetValue(req, httpRequest.Form.Files[y]);
                else
                    throw new NotSupportedException($"{typeof(TRequest).FullName}.{prop.PropInfo.Name} is not an IFormFile property!");
            }
        }
    }

    private static void BindFromUserClaims(TRequest req, HttpContext ctx, List<ValidationFailure> failures)
    {
        for (int i = 0; i < ReqTypeCache<TRequest>.CachedFromClaimProps.Count; i++)
        {
            var (claimType, forbidIfMissing, propInfo) = ReqTypeCache<TRequest>.CachedFromClaimProps[i];
            var claimVal = ctx.User.FindFirst(c => c.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase))?.Value;

            if (claimVal is null && forbidIfMissing)
                failures.Add(new(claimType, "User doesn't have this claim type!"));

            if (claimVal is not null)
                propInfo.SetValue(req, claimVal);
        }
        if (failures.Count > 0) throw new ValidationFailureException();
    }

    private static void BindFromRouteValues(TRequest req, RouteValueDictionary routeValues)
    {
        var routeKVPs = routeValues.Where(rv => ((string?)rv.Value)?.StartsWith("{") == false).ToArray();

        for (int i = 0; i < routeKVPs.Length; i++)
            Bind(req, routeKVPs[i]);
    }

    private static void BindFromQueryParams(TRequest req, IQueryCollection query)
    {
        var queryParams = query.Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value[0])).ToArray();

        for (int i = 0; i < queryParams.Length; i++)
            Bind(req, queryParams[i]);
    }

    private static void Bind(TRequest req, KeyValuePair<string, object?> rv)
    {
        if (ReqTypeCache<TRequest>.CachedProps.TryGetValue(rv.Key.ToLower(), out var prop))
        {
            bool success = false;

            switch (prop.TypeCode)
            {
                case TypeCode.String:
                    success = true;
                    prop.PropInfo.SetValue(req, rv.Value);
                    break;

                case TypeCode.Boolean:
                    success = bool.TryParse((string?)rv.Value, out var resBool);
                    prop.PropInfo.SetValue(req, resBool);
                    break;

                case TypeCode.Int32:
                    success = int.TryParse((string?)rv.Value, out var resInt);
                    prop.PropInfo.SetValue(req, resInt);
                    break;

                case TypeCode.Int64:
                    success = long.TryParse((string?)rv.Value, out var resLong);
                    prop.PropInfo.SetValue(req, resLong);
                    break;

                case TypeCode.Double:
                    success = double.TryParse((string?)rv.Value, out var resDbl);
                    prop.PropInfo.SetValue(req, resDbl);
                    break;

                case TypeCode.Decimal:
                    success = decimal.TryParse((string?)rv.Value, out var resDec);
                    prop.PropInfo.SetValue(req, resDec);
                    break;
            }

            if (!success)
            {
                throw new NotSupportedException(
                "Model binding failed! " +
                $"{typeof(TRequest).FullName}.{prop.PropInfo.Name}[{prop.TypeCode}] Tried: \"{rv.Value}\"");
            }
        }
    }
}

