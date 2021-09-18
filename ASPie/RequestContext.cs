using FluentValidation.Results;
using System.Linq.Expressions;
using System.Text.Json;

namespace ASPie
{
    public class RequestContext
    {
        public static JsonSerializerOptions SerializerOptions { get; set; } = new() { PropertyNamingPolicy = null };

        public HttpContext HttpContext { get; set; }

        public bool ValidationFailed { get => ValidationFailures.Count > 0; }

        internal List<ValidationFailure> ValidationFailures { get; set; } = new();

        public RequestContext(HttpContext httpContext)
        {
            HttpContext = httpContext;
        }

        public void AddError(string message)
            => ValidationFailures.Add(new ValidationFailure("GeneralErrors", message));

        public void AddError<TRequest>(Expression<Func<TRequest, object>> property, string errorMessage) where TRequest : IRequest
        {
            var exp = (MemberExpression)property.Body;
            if (exp is null) throw new ArgumentException("Please supply a valid member expression!");
            ValidationFailures.Add(
                new ValidationFailure(
                    exp.Member.Name,
                    errorMessage));
        }

        public void ThrowIfAnyErrors()
        {
            if (ValidationFailed)
                throw new ValidationFailureException();
        }

        public void ThrowError(string message)
        {
            AddError(message);
            ThrowIfAnyErrors();
        }

        public void ThrowError<TRequest>(Expression<Func<TRequest, object>> property, string errorMessage) where TRequest : IRequest
        {
            AddError(property, errorMessage);
            ThrowIfAnyErrors();
        }

        public Task SendErrorAsync()
        {
            if (HttpContext.Response.HasStarted)
                return Task.CompletedTask;

            HttpContext.Response.StatusCode = 400;
            return HttpContext.Response.WriteAsJsonAsync(new ErrorResponse(ValidationFailures), SerializerOptions);
        }

        public Task SendAsync(object response)
        {
            if (HttpContext.Response.HasStarted)
                return Task.CompletedTask;

            HttpContext.Response.StatusCode = 200;
            return HttpContext.Response.WriteAsJsonAsync(response, SerializerOptions);
        }

        public Task SendOkAsync()
        {
            if (HttpContext.Response.HasStarted)
                return Task.CompletedTask;

            HttpContext.Response.StatusCode = 200;
            return Task.CompletedTask;
        }

        public Task SendNoContentAsync()
        {
            if (HttpContext.Response.HasStarted)
                return Task.CompletedTask;

            HttpContext.Response.StatusCode = 204;
            return Task.CompletedTask;
        }

        public Task SendNotFoundAsync()
        {
            if (HttpContext.Response.HasStarted)
                return Task.CompletedTask;

            HttpContext.Response.StatusCode = 404;
            return Task.CompletedTask;
        }
    }
}
