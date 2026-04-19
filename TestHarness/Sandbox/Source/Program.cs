var bld = WebApplication.CreateBuilder(args);
bld.Services
   .OpenApiDocument()
   .AddFastEndpoints();

var app = bld.Build();
app.UseFastEndpoints();
app.MapOpenApi();
app.MapScalarApiReference();
app.Run();