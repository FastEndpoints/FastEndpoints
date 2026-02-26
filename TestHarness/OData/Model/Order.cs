namespace Sample.Model;

public class Order
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}