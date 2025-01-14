using Contracts;
using Microsoft.EntityFrameworkCore;

var bld = WebApplication.CreateBuilder(args);
bld.Services
   .SwaggerDocument()
   .AddFastEndpoints()
   .AddJobQueues<JobRecord, JobStorageProvider>();

bld.Services.AddDbContextFactory<OrderDbContext>(
    opts =>
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        var dbPath = Path.Join(path, "orders.db");
        opts.UseSqlite($"DataSource={dbPath}");
    });

var app = bld.Build();
app.UseFastEndpoints()
   .UseJobQueues()
   .UseSwaggerGen();
app.Run();

//public partial class Program;