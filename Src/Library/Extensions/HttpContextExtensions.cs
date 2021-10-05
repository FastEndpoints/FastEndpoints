using Microsoft.AspNetCore.Http;

namespace FastEndpoints
{
    internal static class HttpContextExtensions
    {
        internal static Task WriteToResponseAsync(this HttpContext ctx, Stream inputStream, CancellationToken cancellation)
        {
            if (inputStream is null) throw new ArgumentNullException(nameof(inputStream), "The supplied stream cannot be null!");

            if (inputStream.Position > 0 && !inputStream.CanSeek)
                throw new ArgumentException("The supplied stream is not seekable and the postition can't be set back to 0!");

            return inputStream.CopyToAsync(ctx.Response.Body, 64 * 1024, cancellation);
        }
    }
}
