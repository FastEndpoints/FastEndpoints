using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace FastEndpoints;

static class EndpointProducesMetadata
{
    extension(RouteHandlerBuilder b)
    {
        internal void AddSwaggerDefaults(EndpointDefinition ep, string route)
        {
            //clearing all produces metadata before proceeding - https://github.com/FastEndpoints/FastEndpoints/issues/833
            //this is possibly related to .net 9+ only, but we'll be covering all bases this way.
            b.Add(
                eb =>
                {
                    for (var i = eb.Metadata.Count - 1; i >= 0; i--)
                    {
                        if (eb.Metadata[i] is IProducesResponseTypeMetadata)
                            eb.Metadata.RemoveAt(i);
                    }
                });

            var isPlainTextRequest = Types.IPlainTextRequest.IsAssignableFrom(ep.ReqDtoType);

            if (isPlainTextRequest)
            {
                b.Accepts(ep.ReqDtoType, "text/plain", "application/json");
                b.ProducesDeDuped(200, ep.ResDtoType, ["text/plain", "application/json"]);

                return;
            }

            if (ep.ReqDtoType != Types.EmptyRequest)
            {
                if (ep.ReqDtoType.AllPropsAreNonJsonSourced(route))
                    b.Accepts(ep.ReqDtoType, "*/*");
                else if (ep.Verbs.Any(m => m is "GET" or "HEAD" or "DELETE"))
                    b.Accepts(ep.ReqDtoType, "*/*", "application/json");
                else
                    b.Accepts(ep.ReqDtoType, "application/json");
            }

            if (ep.ExecuteAsyncReturnsIResult)
                b.Add(eb => ProducesMetaForResultOfResponse.AddMetadata(eb, ep.ResDtoType));
            else
            {
                if (ep.ResDtoType == Types.Object || ep.ResDtoType == Types.EmptyResponse)
                    b.ProducesDeDuped(204, Types.Void, []);
                else
                    b.ProducesDeDuped(200, ep.ResDtoType, ["application/json"]);
            }

            if (ep.AnonymousVerbs?.Length is null or 0)
                b.ProducesDeDuped(401, Types.Void, []);

            if (ep.RequiresAuthorization())
                b.ProducesDeDuped(403, Types.Void, []);

            if (Cfg.ErrOpts.ProducesMetadataType is not null && ep.ValidatorType is not null)
                b.ProducesDeDuped(Cfg.ErrOpts.StatusCode, Cfg.ErrOpts.ProducesMetadataType, [Cfg.ErrOpts.ContentType]);

            if (ep.X402PaymentMetadata is not null)
                b.ProducesDeDuped(402, Types.Void, []);
        }

        void ProducesDeDuped(int statusCode, Type type, string[] contentTypes)
        {
            b.Finally(
                b1 =>
                {
                    for (var i = 0; i < b1.Metadata.Count; i++)
                    {
                        int? code = b1.Metadata[i] switch
                        {
                            IProducesResponseTypeMetadata p => p.StatusCode,
                            IApiResponseMetadataProvider a => a.StatusCode,
                            _ => null
                        };

                        if (code is null)
                            continue;

                        switch (statusCode)
                        {
                            case >= 200 and < 300 when code is >= 200 and < 300:
                            case >= 400 and < 500 when code == statusCode:
                                return;
                        }
                    }

                    b1.Metadata.Add(new DefaultProducesResponseMetadata(type, statusCode, contentTypes));
                });
        }
    }

    static bool AllPropsAreNonJsonSourced(this Type tRequest, string route)
    {
        //a prop with no binding attribute is still non-json-sourced if its binding field name
        //matches a route parameter of this specific route, because the binder implicitly binds
        //route values to same-named fields. an endpoint can have multiple routes with differing
        //param sets, so this must be evaluated per-route rather than unioning params across all of them.
        HashSet<string>? routeParamNames = null;

        foreach (var prop in tRequest.BindableProps())
        {
            if (prop.CustomAttributes.Any(a => Types.NonJsonBindingAttribute.IsAssignableFrom(a.AttributeType)))
                continue;

            routeParamNames ??= RouteParamNames(route);

            if (!routeParamNames.Contains(prop.FieldName()))
                return false;
        }

        return true;
    }

    static HashSet<string> RouteParamNames(string route)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var start = -1;

        for (var i = 0; i < route.Length; i++)
        {
            var c = route[i];

            if (c is not ('{' or '}'))
                continue;

            if (i + 1 < route.Length && route[i + 1] == c)
            {
                //a doubled brace ("{{" or "}}") is route-template escaping for a literal brace
                //(e.g. a {{n}} quantifier inside a `:regex(...)` constraint) - not a delimiter.
                i++;

                continue;
            }

            if (c == '{')
                start = i;
            else if (start >= 0)
            {
                var nameStart = start + 1;

                while (nameStart < i && route[nameStart] == '*')
                    nameStart++;

                var nameEnd = nameStart;

                while (nameEnd < i && route[nameEnd] is not ('?' or ':' or '='))
                    nameEnd++;

                if (nameEnd > nameStart)
                    names.Add(route[nameStart..nameEnd]);

                start = -1;
            }
        }

        return names;
    }
}
