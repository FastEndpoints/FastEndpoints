using FastEndpoints;
using FastEndpoints.OData;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using Sample.Model;

var bld = WebApplication.CreateBuilder(args);
bld.Services
   .AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("CustomerOrderLists"))
   .AddOData(opt => opt.EnableAll())
   .AddFastEndpoints();

var app = bld.Build();
app.UseFastEndpoints();
app.Seed();
app.MapODataMetadata("odata/$metadata", EdmModel.Instance);
app.Run();

sealed class CustomersEndpoint(AppDbContext db) : ODataEndpoint<Customer>
{
    protected override void Setup()
    {
        Get("/odata/customers");
        AllowAnonymous();
    }

    public override async Task<IQueryable> ExecuteAsync(ODataQueryOptions<Customer> req, CancellationToken ct)
    {
        await Task.CompletedTask;

        return req.ApplyTo(db.Customers);
    }
}