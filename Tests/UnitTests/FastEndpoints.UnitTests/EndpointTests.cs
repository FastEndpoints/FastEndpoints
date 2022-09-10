using Microsoft.AspNetCore.Http;
using Xunit;

namespace FastEndpoints.UnitTests;

public class EndpointTests
{
    public class SendShouldSetCorrectResponse : Endpoint<Request, Response>
    {
        [Fact]
        public async Task execute_test()
        {
            HttpContext = new DefaultHttpContext();
            Definition = new EndpointDefinition();

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
            Definition = new EndpointDefinition();

            await SendOkAsync(new Response
            {
                Id = 1,
                Age = 15,
                Name = "Test"
            },
                CancellationToken.None);

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
            Definition = new EndpointDefinition();

            await SendForbiddenAsync(CancellationToken.None);
            Response.Should().NotBeNull();
            ValidationFailed.Should().BeFalse();
            HttpContext.Items[0].Should().BeNull();
            HttpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
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
}