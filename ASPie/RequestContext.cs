using FluentValidation.Results;
using System.Text.Json;

namespace ASPie
{
    public class RequestContext
    {
        public static JsonSerializerOptions SerializerOptions { get; set; } = new()
        {
            PropertyNamingPolicy = null
        };

        public HttpContext HttpContext { get; set; }
        internal List<ValidationFailure> ValidationFailures { get; set; } = new();

        public RequestContext(HttpContext httpContext)
        {
            HttpContext = httpContext;
        }

        public Task SendErrorAsync()
        {
            HttpContext.Response.StatusCode = 400;
            return HttpContext.Response.WriteAsJsonAsync(new ErrorResponse(ValidationFailures), SerializerOptions);
        }
    }
}
