using ApiExpress;
using ApiExpress.Security;

var builder = WebApplication.CreateBuilder();
builder.Services.AddApiExpress();
builder.Services.AddAuthenticationJWTBearer(builder.Configuration["TokenKey"]);
builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole("Admin")));

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseApiExpress();
app.Run();

//todo: write tests
// - sending more than one response in a handler
// - GET request with route bound values
// - auto resolved services with endpoint properties
// - DontThrowIfValidationFails()
// - Policies()
// - Roles()
// - AcceptFiles()
// - SendBytesAsync()

//todo: add xml documentation
//todo: write wiki/documentation on github