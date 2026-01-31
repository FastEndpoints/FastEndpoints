using FastEndpoints;
using System.Reflection;

namespace NativeAotChecker.Endpoints;

// Source DTO
public class SourceDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<string> Tags { get; set; } = [];
}

// Destination DTO (different structure)
public class DestinationDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string CreatedDate { get; set; } = string.Empty;
    public int TagCount { get; set; }
}

// Response
public class ObjectMapperResponse
{
    public DestinationDto? MappedObject { get; set; }
    public int PropertiesMapped { get; set; }
    public List<string> MappedProperties { get; set; } = [];
    public bool ObjectMappingWorked { get; set; }
}

/// <summary>
/// Tests object-to-object mapping via reflection in AOT mode.
/// AOT ISSUE: Property enumeration uses GetProperties().
/// PropertyInfo.SetValue/GetValue use reflection.
/// Dynamic mapping logic is entirely reflection-based.
/// </summary>
public class ObjectToObjectMapperEndpoint : Endpoint<SourceDto, ObjectMapperResponse>
{
    public override void Configure()
    {
        Post("object-mapper-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SourceDto req, CancellationToken ct)
    {
        var destination = new DestinationDto();
        var mappedProps = new List<string>();

        try
        {
            // Reflection-based mapping
            var sourceProps = typeof(SourceDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var destProps = typeof(DestinationDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var sourceProp in sourceProps)
            {
                var destProp = destProps.FirstOrDefault(p => p.Name == sourceProp.Name);
                if (destProp != null && destProp.CanWrite && sourceProp.PropertyType == destProp.PropertyType)
                {
                    var value = sourceProp.GetValue(req);
                    destProp.SetValue(destination, value);
                    mappedProps.Add(sourceProp.Name);
                }
            }

            // Manual mapping for non-matching properties
            destination.FullName = $"{req.FirstName} {req.LastName}";
            destination.ContactEmail = req.Email;
            destination.CreatedDate = req.CreatedAt.ToString("yyyy-MM-dd");
            destination.TagCount = req.Tags.Count;
        }
        catch
        {
            // Mapping failed
        }

        await Send.OkAsync(new ObjectMapperResponse
        {
            MappedObject = destination,
            PropertiesMapped = mappedProps.Count,
            MappedProperties = mappedProps,
            ObjectMappingWorked = mappedProps.Count > 0
        });
    }
}
