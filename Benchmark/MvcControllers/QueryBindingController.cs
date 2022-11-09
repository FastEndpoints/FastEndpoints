using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MvcControllers;


public class QueryRequest
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Age { get; set; }
    public IEnumerable<string>? PhoneNumbers { get; set; }
    public NestedQueryObject? NestedQueryObject { get; set; }
}
public class NestedQueryObject
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Age { get; set; }
    public IEnumerable<string>? PhoneNumbers { get; set; }
    public MoreNestedQueryObject? MoreNestedQueryObject { get; set; }
}

public class MoreNestedQueryObject
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Age { get; set; }
    public IEnumerable<string>? PhoneNumbers { get; set; }
}

public class QueryResponse
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? PhoneNumber { get; set; }
    public NestedQueryObject? NestedQueryObject { get; set; }
}

[Authorize]
[ApiController]
public class QueryBindingController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("/benchmark/query-binding")]
    public IActionResult Index([FromQuery] QueryRequest req)
    {
        return Ok(new QueryResponse()
        {
            Id = req.Id,
            Name = req.FirstName + " " + req.LastName,
            Age = req.Age,
            PhoneNumber = req.PhoneNumbers?.FirstOrDefault(),
            NestedQueryObject = req.NestedQueryObject
        });
    }
}
