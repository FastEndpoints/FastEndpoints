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
        Definition = new(typeof(SendShouldSetCorrectResponse), typeof(Request), typeof(Response));

        await SendAsync(
            new()
            {
                Id = 1,
                Age = 15,
                Name = "Test"
            });

        Response.ShouldNotBeNull();
        Response.Id.ShouldBe(1);
        ValidationFailed.ShouldBeFalse();
    }
}

public class SendOkShouldSetCorrectResponse : Endpoint<Request, Response>
{
    [Fact]
    public async Task execute_test()
    {
        HttpContext = new DefaultHttpContext();
        Definition = new(typeof(SendOkShouldSetCorrectResponse), typeof(Request), typeof(Response));

        await SendOkAsync(
            new()
            {
                Id = 1,
                Age = 15,
                Name = "Test"
            },
            CancellationToken.None);

        Response.ShouldNotBeNull();
        Response.Id.ShouldBe(1);
        ValidationFailed.ShouldBeFalse();
    }
}

public class SendForbiddenShouldSetCorrectResponse : Endpoint<Request, Response>
{
    [Fact]
    public async Task execute_test()
    {
        HttpContext = new DefaultHttpContext();
        Definition = new(typeof(SendForbiddenShouldSetCorrectResponse), typeof(Request), typeof(Response));

        await SendForbiddenAsync(CancellationToken.None);
        Response.ShouldNotBeNull();
        ValidationFailed.ShouldBeFalse();
        HttpContext.Items[0].ShouldBeNull();
        HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }
}

public class SendShouldCallResponseInterceptorIfUntypedResponseObjectIsSupplied : Endpoint<Request, Response>
{
    [Fact]
    public async Task execute_test()
    {
        HttpContext = new DefaultHttpContext();
        Definition = new(typeof(SendShouldCallResponseInterceptorIfUntypedResponseObjectIsSupplied), typeof(Request), typeof(Response));
        Definition.ResponseInterceptor(new ResponseInterceptor());

        await Assert.ThrowsAsync<ResponseInterceptor.InterceptedResponseException>(
            () =>
            {
                return SendInterceptedAsync(
                    new
                    {
                        Id = 0,
                        Age = 1,
                        Name = "Test"
                    });
            });
    }
}

public class SendInterceptedShouldThrowInvalidOperationExceptionIfCalledWithNoInterceptor : Endpoint<Request, Response>
{
    [Fact]
    public async Task execute_test()
    {
        HttpContext = new DefaultHttpContext();
        Definition = new(typeof(SendInterceptedShouldThrowInvalidOperationExceptionIfCalledWithNoInterceptor), typeof(Request), typeof(Response));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                SendInterceptedAsync(
                    new
                    {
                        Id = 0,
                        Age = 1,
                        Name = "Test"
                    }));
    }
}

public class SendShouldNotCallResponseInterceptorIfExpectedTypedResponseObjectIsSupplied : Endpoint<Request, Response>
{
    [Fact]
    public async Task execute_test()
    {
        HttpContext = new DefaultHttpContext();
        Definition = new(typeof(SendShouldNotCallResponseInterceptorIfExpectedTypedResponseObjectIsSupplied), typeof(Request), typeof(Response));
        Definition.ResponseInterceptor(new ResponseInterceptor());

        await SendAsync(
            new()
            {
                Id = 1,
                Age = 15,
                Name = "Test"
            });

        Response.ShouldNotBeNull();
        Response.Id.ShouldBe(1);
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
        Definition = new(typeof(NestedRequestParamTest), typeof(Request), typeof(Response));

        Configure();

        var summary = new EndpointSummary<NestedRequest>();

        summary.RequestParam(r => r.Request.FirstName, "request > firstname");
        summary.RequestParam(r => r.Request.PhoneNumbers, "request > phonenumbers");
        summary.RequestParam(r => r.Request.Items[0].Description, "request > items > description");

        summary.Params.ShouldContainKey("Request.FirstName");
        summary.Params["Request.FirstName"].ShouldBe("request > firstname");

        summary.Params.ShouldContainKey("Request.PhoneNumbers");
        summary.Params["Request.PhoneNumbers"].ShouldBe("request > phonenumbers");

        summary.Params.ShouldContainKey("Request.Items[0].Description");
        summary.Params["Request.Items[0].Description"].ShouldBe("request > items > description");
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
    public List<Item> Items { get; set; } = [];

    public class Item
    {
        public string Description { get; set; } = null!;
    }
}

public class NestedRequest
{
    public string IdentificationCard { get; set; } = null!;
    public Request Request { get; set; } = null!;
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