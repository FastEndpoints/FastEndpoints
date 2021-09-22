using EZEndpoints;

var builder = WebApplication.CreateBuilder();

builder.Services.AddEZEndpoints();
builder.Services.AddAuthenticationJWTBearer(builder.Configuration["TokenKey"]);
builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole("Admin")));

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseEZEndpoints();
app.Run();

//todo: write tests
//todo: add xml documentation
//todo: [From(Claim.ClaimType)] attribute - should forbid if current user doesn't have claim
