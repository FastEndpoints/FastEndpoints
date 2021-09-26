using FastEndpoints;
using FastEndpoints.Security;
using Web.Auth;
using Web.Services;

var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();
builder.Services.AddAuthenticationJWTBearer(builder.Configuration["TokenKey"]);
builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)));
builder.Services.AddScoped<IEmailService, EmailService>();

WebApplication app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints();

var test = app.Services;
var service = app.Services.GetService<IEmailService>();

app.Run();

//todo: write tests
// - DontThrowIfValidationFails()
// - AcceptFiles()
// - SendBytesAsync()

//todo: add xml documentation
//todo: write wiki/documentation on github