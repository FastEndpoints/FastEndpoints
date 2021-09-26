using FastEndpoints;
using FastEndpoints.Security;
using Web.Auth;

var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();
builder.Services.AddAuthenticationJWTBearer(builder.Configuration["TokenKey"]);
builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)));

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints();
app.Run();

//todo: main list
// - make endpoint methods more static

//todo: write tests
// - auto resolved services with endpoint properties
// - DontThrowIfValidationFails()
// - AcceptFiles()
// - SendBytesAsync()

//todo: add xml documentation
//todo: write wiki/documentation on github