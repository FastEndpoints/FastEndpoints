var bld = WebApplication.CreateBuilder(args);
bld.Services
   .SwaggerDocument()
   .AddFastEndpoints();

var app = bld.Build();
app.UseFastEndpoints()
   .UseSwaggerGen();
app.Run();

sealed class BetaTestersOnly(ILogger<BetaTestersOnly> logger) : IFeatureFlag
{
    public async Task<bool> IsEnabledAsync(IEndpoint endpoint)
    {
        logger.LogInformation("Beta testers only flag is running!"); //inject and use whatever you like

        //use whatever mechanism/library you like to determine if this endpoint is disabled for the current request.
        if (endpoint.HttpContext.Request.Headers.TryGetValue("x-beta-tester", out _))
            return true; // return true to enable

        //this is optional. if you don't send anything, a 404 is sent automatically.
        await endpoint.HttpContext.Response.SendErrorsAsync([new("featureDisabled", "You are not a beta tester!")]);

        return false; // return false to disable
    }
}

sealed class BetaEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("beta-feature");
        AllowAnonymous();
        FeatureFlag<BetaTestersOnly>();
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        await Send.OkAsync("this is the beta!");
    }
}