using FluentValidation.Results;
using Microsoft.AspNetCore.Http.Features;
using System.Linq.Expressions;
using System.Text.Json;
namespace EZEndpoints
{
    public abstract class Context
    {
        public static JsonSerializerOptions SerializerOptions { get; set; } = new() { PropertyNamingPolicy = null };
    }

    public class Context<TRequest> : Context where TRequest : IRequest
    {
        public HttpContext HttpContext { get; set; }
        public string BaseURL { get => HttpContext.Request.Scheme + "://" + HttpContext.Request.Host + "/"; }
        public Http Verb { get => Enum.Parse<Http>(HttpContext.Request.Method); }
        public bool ValidationFailed { get => ValidationFailures.Count > 0; }
        public List<ValidationFailure> ValidationFailures { get; } = new();

        public Context(HttpContext httpContext)
        {
            HttpContext = httpContext;
        }

        public void AddError(string message)
            => ValidationFailures.Add(new ValidationFailure("GeneralErrors", message));

        public void AddError(Expression<Func<TRequest, object>> property, string errorMessage)
        {
            var exp = (MemberExpression)property.Body;
            if (exp is null) throw new ArgumentException("Please supply a valid member expression!");
            ValidationFailures.Add(
                new ValidationFailure(exp.Member.Name, errorMessage));
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

        public void ThrowError(Expression<Func<TRequest, object>> property, string errorMessage)
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
            if (!HttpContext.Response.HasStarted)
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

        public Task<IFormCollection> GetFormAsync()
        {
            var req = HttpContext.Request;

            if (!req.HasFormContentType)
                throw new InvalidOperationException("This request doesn't have any multipart form data!");

            return req.ReadFormAsync();
        }

        public async Task<IFormFileCollection> GetFilesAsync()
        {
            return (await GetFormAsync().ConfigureAwait(false)).Files;
        }
    }
}
