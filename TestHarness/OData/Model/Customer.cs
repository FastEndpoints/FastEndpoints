namespace Sample.Model;

public class Customer
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Name { get; set; } = string.Empty;
    public Address Address { get; set; } = null!;
}