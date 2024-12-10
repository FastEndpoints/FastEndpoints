using FastEndpoints;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Endpoints;

public class SendShouldSetCorrectResponse : Endpoint<Request, Response>
{
    [Fact]
    public async Task execute_test()
    {
        HttpContext = new DefaultHttpContext();
        Definition = new EndpointDefinition(typeof(SendShouldSetCorrectResponse), typeof(Request), typeof(Response));

        await SendAsync(new Response
        {
            Id = 1,
            Age = 15,
            Name = "Test"
        }, StatusCodes.Status200OK, default);

        Response.Should().NotBeNull();
        Response.Id.Should().Be(1);
        ValidationFailed.Should().BeFalse();
    }
}

public class SendOkShouldSetCorrectResponse : Endpoint<Request, Response>
{
    [Fact]
    public async Task execute_test()
    {
        HttpContext = new DefaultHttpContext();
        Definition = new EndpointDefinition(typeof(SendOkShouldSetCorrectResponse), typeof(Request), typeof(Response));

        await SendOkAsync(new Response
        {
            Id = 1,
            Age = 15,
            Name = "Test"
        }, CancellationToken.None);

        Response.Should().NotBeNull();
        Response.Id.Should().Be(1);
        ValidationFailed.Should().BeFalse();
    }
}

public class SendForbiddenShouldSetCorrectResponse : Endpoint<Request, Response>
{
    [Fact]
    public async Task execute_test()
    {
        HttpContext = new DefaultHttpContext();
        Definition = new EndpointDefinition(typeof(SendForbiddenShouldSetCorrectResponse), typeof(Request), typeof(Response));

        await SendForbiddenAsync(CancellationToken.None);
        Response.Should().NotBeNull();
        ValidationFailed.Should().BeFalse();
        HttpContext.Items[0].Should().BeNull();
        HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}

public class SendShouldCallResponseInterceptorIfUntypedResponseObjectIsSupplied : Endpoint<Request, Response>
{
    [Fact]
    public async Task execute_test()
    {
        HttpContext = new DefaultHttpContext();
        Definition = new EndpointDefinition(typeof(SendShouldCallResponseInterceptorIfUntypedResponseObjectIsSupplied), typeof(Request), typeof(Response));
        Definition.ResponseInterceptor(new ResponseInterceptor());

        await Assert.ThrowsAsync<ResponseInterceptor.InterceptedResponseException>(() =>
        {
            return SendInterceptedAsync(new {
                Id = 0,
                Age = 1,
                Name = "Test"
            }, StatusCodes.Status200OK, default);
        });

    }
}

public class SendInterceptedShouldThrowInvalidOperationExceptionIfCalledWithNoInterceptor : Endpoint<Request, Response>
{
    [Fact]
    public async Task execute_test()
    {
        HttpContext = new DefaultHttpContext();
        Definition = new EndpointDefinition(typeof(SendInterceptedShouldThrowInvalidOperationExceptionIfCalledWithNoInterceptor), typeof(Request), typeof(Response));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SendInterceptedAsync(new {
                Id = 0,
                Age = 1,
                Name = "Test"
            }, StatusCodes.Status200OK, default));

    }
}

public class SendShouldNotCallResponseInterceptorIfExpectedTypedResponseObjectIsSupplied : Endpoint<Request, Response>
{
    [Fact]
    public async Task execute_test()
    {
        HttpContext = new DefaultHttpContext();
        Definition = new EndpointDefinition(typeof(SendShouldNotCallResponseInterceptorIfExpectedTypedResponseObjectIsSupplied), typeof(Request), typeof(Response));
        Definition.ResponseInterceptor(new ResponseInterceptor());

        await SendAsync(new Response
        {
            Id = 1,
            Age = 15,
            Name = "Test"
        }, StatusCodes.Status200OK, default);

        Response.Should().NotBeNull();
        Response.Id.Should().Be(1);
    }
}

public class NestedRequestParamTest : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/nested-request-param");
    }

    [Fact]
    public async Task execute_test()
    {
        Definition = new EndpointDefinition(typeof(NestedRequestParamTest), typeof(Request), typeof(Response));

        Configure();

        var summary = new EndpointSummary<NestedRequest>();

        summary.RequestParam(r => r.Request.FirstName, "First name in request");

        summary.Params.Should().ContainKey("Request.FirstName");
        summary.Params["Request.FirstName"].Should().Be("First name in request");
    }
}


public class Response
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? PhoneNumber { get; set; }
}

public class Request
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Age { get; set; }
    public IEnumerable<string>? PhoneNumbers { get; set; }
}

public class NestedRequest
{
    public string IdentificationCard { get; set; }
    public Request Request { get; set; }
}

public class ResponseInterceptor : IResponseInterceptor
{
    public Task InterceptResponseAsync(object res, int statusCode, HttpContext ctx, IReadOnlyCollection<ValidationFailure> failures, CancellationToken ct)
        => throw new InterceptedResponseException();

    public class InterceptedResponseException : Exception
    {
        public override string Message => "Intercepted Response";
    }
}
