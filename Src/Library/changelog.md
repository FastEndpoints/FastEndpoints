---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Customize error response Content-Type globally</summary>
The default `content-type` header value for all error responses is `application/problem+json`. The default can be customized as follows:

```cs
app.UseFastEndpoints(c => c.Errors.ContentType = "application/json")
```

</details>

<details><summary>'DontAutoSend()' support for 'Results&lt;T1,T2,...&gt;' returning endpoint handler methods</summary>

When putting a [post-processor in charge](https://fast-endpoints.com/docs/pre-post-processors#abstracting-response-sending-logic-into-a-post-processor) of sending the 
response, it was not previously not supported when the handler method returns a `Results<T1,T2,...>`. You can now use the `DontAutoSend()` config option with such 
handlers.

</details>

<details><summary>'ProblemDetails' per instance title transformer</summary>

You can now supply a delegate that will transform the `Title` field of `ProblemDetails` responses based on some info present on the final problem details instance. 
For example, you can transform the final title value depending on the status code of the response like so:

```cs
ProblemDetails.TitleTransformer = p => p.Status switch
{
    400 => "Validation Error",
    404 => "Not Found",
    _ => "One or more errors occurred!"
};
```

</details>

<details><summary>Setting for allowing empty request DTOs</summary>

By default, an exception will be thrown if you set the `TRequest` of an endpoint to a class type that does not have any bindable properties. This behavior can now be 
turned off if your use case requires empty request DTOs.

```cs
app.UseFastEndpoints(c => c.Endpoints.AllowEmptyRequestDtos = true)
```

```cs
sealed record HelloRequest;

sealed class MyEndpoint : Endpoint<HelloRequest>
{
    public override void Configure()
    {
        Post("test");
        Description(x => x.ClearDefaultAccepts()); //this will be needed for POST requests
        AllowAnonymous();
    }
}
```

</details>

<details><summary>Ability to specify output file name with 'ExportSwaggerJsonAndExitAsync()'</summary>

It is now possible to customize the name of the exported `swagger.json` file when exporting a swagger document to disk with the `ExportSwaggerJsonAndExitAsync()` method.

</details>

## Improvements 🚀

<details><summary>Support async setup activity that contributes to WAF creation in 'AppFixture'</summary>

Previously it was not possible to do any setup activity that directly contributes to the creation of the WAF instance. Now it can be achieved like so:

```cs
using Testcontainers.MongoDb;

public class Sut : AppFixture<Program>
{
    const string Database = "TestingDB";
    const string RootUsername = "root";
    const string RootPassword = "password";

    MongoDbContainer _container = null!;

    protected override async Task PreSetupAsync()
    {
        // anything that needs to happen before the WAF is initialized can be done here.
        
        _container = new MongoDbBuilder()
                     .WithImage("mongo")
                     .WithUsername(RootUsername)
                     .WithPassword(RootPassword)
                     .WithCommand("mongod")
                     .Build();
        await _container.StartAsync();
    }

    protected override void ConfigureApp(IWebHostBuilder b)
    {
        b.ConfigureAppConfiguration(
            c =>
            {
                c.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        { "Mongo:Host", _container.Hostname },
                        { "Mongo:Port", _container.GetMappedPublicPort(27017).ToString() },
                        { "Mongo:DbName", Database },
                        { "Mongo:UserName", RootUsername },
                        { "Mongo:Password", RootPassword }
                    });
            });
    }

    protected override async Task TearDownAsync()
        => await _container.DisposeAsync();
}
```

</details>

- automatically rewind request body with `IPlainTextRequest` if `EnableBuffering()` is used. (https://github.com/FastEndpoints/FastEndpoints/issues/631)
- filter out illegal header names from being created as request parameters in swagger. (https://github.com/FastEndpoints/FastEndpoints/issues/615)
- implement `[FromBody]` attribute support for routeless integration testing. (https://github.com/FastEndpoints/FastEndpoints/issues/645)
- hydrate integration testing route url with values from request dto. (https://github.com/FastEndpoints/FastEndpoints/pull/648)
- upgrade dependencies to latest

## Fixes 🪲

- duplicate swagger tag descriptions issue (https://github.com/FastEndpoints/FastEndpoints/issues/654)

[//]: # (## Breaking Changes ⚠️)