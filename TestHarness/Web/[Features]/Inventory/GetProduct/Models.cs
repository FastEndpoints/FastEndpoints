using System.Text.Json.Serialization;

namespace Inventory.GetProduct;

public class Response
{
    public string? ProductID { get; set; }

    [JsonPropertyName("Last_Moddded")]
    public long LastModified { get; set; }

    [JsonIgnore]
    public int ThisIsIgnored { get; set; }
}