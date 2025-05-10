using FastEndpoints.OData;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using MinimalAPIWithOData.Model;

var bld = WebApplication.CreateBuilder(args);
bld.Services
   .AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("CustomerOrderLists"))
   .AddOData(opt => opt.EnableAll())
   .AddFastEndpoints();

var app = bld.Build();
app.UseFastEndpoints();
app.Seed();
app.MapODataMetadata("odata/$metadata", EdmModel.Instance);
app.UseHttpsRedirection();
app.Run();

sealed class CustomersEndpoint(AppDbContext db) : ODataEndpoint<Customer>
{
    protected override void Setup()
    {
        Get("odata/customers/fastendpoints");
        AllowAnonymous();
    }

    public override Task<IQueryable> ExecuteAsync(ODataQueryOptions<Customer> req, CancellationToken ct)
        => Task.FromResult(req.ApplyTo(db.Customers));
}