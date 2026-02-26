using Microsoft.EntityFrameworkCore;

namespace Sample.Model;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>().HasKey(x => x.Id);
        modelBuilder.Entity<Order>().HasKey(x => x.Id);
        modelBuilder.Entity<Customer>().OwnsOne(x => x.Address);
    }
}