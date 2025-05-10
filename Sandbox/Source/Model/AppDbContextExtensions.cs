namespace MinimalAPIWithOData.Model;

public static class AppDbContextExtensions
{
    public static void Seed(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<AppDbContext>();

        // Check if database already has customers to avoid re-seeding
        if (context.Customers.Any())
        {
            return; // Database already seeded
        }

        var customers = new List<Customer>()
        {
            new()
            {
                Name = "John Doe",
                Address = new Address
                {
                    City = "New York",
                    Street = "156 Avenue"
                }
            },
            new()
            {
                Name = "Jane Smith",
                Address = new Address
                {
                    City = "Los Angeles",
                    Street = "237 Palm Drive"
                }
            },
            new()
            {
                Name = "Michael Johnson",
                Address = new Address
                {
                    City = "Chicago",
                    Street = "543 Lake Street"
                }
            },
            new()
            {
                Name = "Emily Williams",
                Address = new Address
                {
                    City = "Boston",
                    Street = "89 Harbor Road"
                }
            },
            new()
            {
                Name = "Robert Brown",
                Address = new Address
                {
                    City = "Seattle",
                    Street = "421 Pine Avenue"
                }
            }
        };

        foreach (var customer in customers)
        {
            context.Customers.Add(customer);
        }

        context.SaveChanges();
    }
}