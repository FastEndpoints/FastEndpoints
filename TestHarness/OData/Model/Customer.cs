namespace Sample.Model;

public class Customer
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = string.Empty;
    public required Address Address { get; set; }
}