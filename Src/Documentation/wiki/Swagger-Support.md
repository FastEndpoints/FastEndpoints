# enabling swagger ui

in order to easily enable swaggergen/ui for your project, first install the `FastEndpoints.Swagger` package and add the following 3 lines to your app startup:

```csharp
global using FastEndpoints;

var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();

var app = builder.Build();
app.UseAuthorization();
app.UseFastEndpoints();
app.Run();
```