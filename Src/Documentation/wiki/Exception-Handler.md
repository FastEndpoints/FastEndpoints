# unhandled exception handler
the library ships with a default exception handler middleware you can use to log the exception details on the server and return a user-friendly http 500 response to the requesting client.

### example json response:
```
{
  "Status": "Internal Server Error!",
  "Code": 500,
  "Reason": "'x' is an invalid start of a value. Path: $.ValMin | LineNumber: 4...",
  "Note": "See application log for stack trace."
}
```

### example server log entry:
```
fail: FastEndpoints.ExceptionHandler[0]
      =================================
      HTTP: POST /inventory/adjust-stock
      TYPE: JsonException
      REASON: 'x' is an invalid start of a value. Path: $.ValMin | LineNumber: 4...
      ---------------------------------
         at System.Text.Json.ThrowHelper.ReThrowWithPath(ReadStack& state,...
         at System.Text.Json.Serialization.JsonConverter`1.ReadCore(Utf8JsonReader& reader,...
         at System.Text.Json.JsonSerializer.ReadCore[TValue](JsonConverter jsonConverter,...
         ...
```

## enabling the exception handler
**Program.cs**

enable the middleware as shown below during app startup.
```csharp
var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();

var app = builder.Build();
app.UseDefaultExceptionHandler(); //add this
app.UseAuthorization();
app.UseFastEndpoints();
app.Run();
```

**appsettings.json**

disable the aspnetcore diagnostic logging for unhandled exceptions in order to avoid duplicate log entries.
```
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware": "None" //add this
    }
}
```