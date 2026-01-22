var builder = WebApplication.CreateSlimBuilder(args);

var app = builder.Build();

app.MapGet("/hello", () => "Hello World");

app.Run("http://127.0.0.1:5050");
