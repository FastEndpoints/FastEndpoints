---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Idempotency support based on 'OutputCaching' middleware</summary>

FastEndpoints now ships with built-in endpoint [idempotency support](https://fast-endpoints.com/docs/idempotency) built around the OutputCaching middleware.

</details>

<details><summary>Specify additional Http Verbs/Methods for endpoints globally</summary>

In addition to the Verbs you specify at the endpoint level, you can now specify Verbs to be added to endpoint with the global configurator as well as endpoint groups like so:

```csharp
//global configurator
app.UseFastEndpoints(
       c => c.Endpoints.Configurator =
                ep =>
                {
                    ep.AdditionalVerbs(Http.OPTIONS, Http.HEAD);
                })
    
//endpoint group
sealed class SomeGroup : Group
{
    public SomeGroup()
    {
        Configure(
            "prefix",
            ep =>
            {
                ep.AdditionalVerbs(Http.OPTIONS, Http.HEAD);
            });
    }
}
```

</details>

<details><summary>Collection-Fixture support for Testing</summary>

This release brings easy access to xUnit collection-fixtures for integration testing. Please the [documentation](https://fast-endpoints.com/docs/integration-unit-testing#test-collections-collection-fixtures) for details.

</details>

<details><summary>Export multiple swagger json files in one go</summary>

A new overload has been added to the `ExportSwaggerJsonAndExitAsync()` method for exporting multiple Swagger Json files using the `FastEndpoints.ClientGen.Kiota` library.

```csharp
await app.ExportSwaggerJsonAndExitAsync(
    ct: default,
    c =>
    {
        c.DocumentName = "v1";
        c.DestinationPath = "/export/path";
        c.DestinationFileName = "v1.json";
    },
    c =>
    {
        c.DocumentName = "v2";
        c.DestinationPath = "/export/path";
        c.DestinationFileName = "v2.json";
    });
```

</details>

## Improvements 🚀

<details><summary>Throw meaningful exception when incorrect JWT singing algorithm is used</summary>

When creating Asymmetric JWTs, if the user forgets to change the default `SigningAlgorithm` from `HmacSha256` to something suitable for `Asymmetric` signing, a helpful exception message will be thrown instructing the user to correct the mistake. More info: #685

</details>

<details><summary>Establish SSE connection before any data is available</summary>

The SSE request would previously stay in a pending state if there was no initial data available to be sent to the client. Now the client would receive the response headers and await future data in a "connection established" state, even if no data has been received from the server.

</details>

<details><summary>Workaround for NSwag bug with nullable enum parameters</summary>

Swagger UI fails to correctly render a dropdown for nullable Enum DTO properties due to a bug in NSwag where the schema is always referenced via a `OneOf`, instead of directly referencing it with a `$ref`. A workaround has been implemented to mitigate the issue. #699

</details>

## Fixes 🪲

<details><summary>ACL source generator wasn't filtering out internal public static fields</summary>

Generated ACL incorrectly contained the `Descriptions` property in the permission dictionary items due to not being filtered out correctly, which has now been fixed.

</details>

<details><summary>Swagger generation failure due to multiple route constraints</summary>

Swagger generation was throwing an exception if an endpoint with multiple route constraints on a single parameter such as the following was encountered:

```csharp
/{member:int:min(1):max(5)}/
```

</details>

## Minor Breaking Changes ⚠️

<details><summary>Move static properties of 'ProblemDetails' class to global config</summary>

Static configuration properties that used to be on the `ProblemDetails` class will have to be set from the global configuration going forward like so:

```csharp
app.UseFastEndpoints(
   c => c.Errors.UseProblemDetails(
       x =>
       {
           x.AllowDuplicateErrors = true;  //allows duplicate errors for the same error name
           x.IndicateErrorCode = true;     //serializes the fluentvalidation error code
           x.IndicateErrorSeverity = true; //serializes the fluentvalidation error severity
           x.TypeValue = "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.1";
           x.TitleValue = "One or more validation errors occurred.";
           x.TitleTransformer = pd => pd.Status switch
           {
               400 => "Validation Error",
               404 => "Not Found",
               _ => "One or more errors occurred!"
           };
       }));
```

</details>