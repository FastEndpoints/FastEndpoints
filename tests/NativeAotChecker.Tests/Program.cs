using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var exePath = Path.Combine(
    AppContext.BaseDirectory,
    "..", "..", "..", "obj", "aot", "NativeAotChecker.exe");

builder.AddExecutable("api", exePath, Path.GetDirectoryName(exePath)!)
    .WithEnvironment("ASPNETCORE_URLS", "http://127.0.0.1:5050")
    .WithHttpEndpoint(port: 5050, name: "http");

builder.Build().Run();
