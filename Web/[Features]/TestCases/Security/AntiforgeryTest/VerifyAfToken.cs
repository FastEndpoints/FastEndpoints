namespace TestCases.AntiforgeryTest;

public class VerificationRequest
{
    public IFormFile File { get; set; }
    public TokenResponse TokenResponse { get; set; }
}

public class VerifyAfToken : Endpoint<VerificationRequest, string>
{
    public override void Configure()
    {
        Post(AntiforgeryTest.Routes.Validate);
        Tags("antiforgery");
        AllowAnonymous();
        EnableAntiforgery();
        AllowFileUploads();
    }

    public override Task<string> ExecuteAsync(VerificationRequest verificationRequest, CancellationToken ct)
        => Task.FromResult("antiforgery success");
}