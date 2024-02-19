using Microsoft.AspNetCore.Antiforgery;

namespace TestCases.AntiforgeryTest;

public class RenderFormHtml : EndpointWithoutRequest
{
    readonly IAntiforgery _antiforgery;

    public RenderFormHtml(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    public override void Configure()
    {
        Get(AntiforgeryTest.Routes.GetFormHtml);
        Tags("antiforgery");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var token = _antiforgery.GetAndStoreTokens(HttpContext!);
        var html = $"""
                    <html>
                    <body>
                     <h3>Upload a image test</h3>
                      <form name="form1" action="/api/ant" method="post" enctype="multipart/form-data">
                        <input name="{token.FormFieldName}" type="hidden" value="{token.RequestToken}"/>
                        <input type="file" name="file" placeholder="Upload an image..." accept=".jpg,.png" />
                        <input type="submit" />
                      </form>
                    </body>
                    </html>
                    """;

        await SendStringAsync(html, 200, "text/html");
    }
}