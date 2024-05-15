---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

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

Please the [documentation](https://fast-endpoints.com//docs/integration-unit-testing#test-collections-collection-fixtures) for details.

</details>

## Improvements 🚀

<details><summary>Throw meaningful exception when incorrect JWT singing algo used</summary>

When creating Asymmetric JWTs, if the user forgets to change the default `SigningAlgorithm` from `HmacSha256` to something suitable for `Asymmetric` signing, a helpful exception message will be thrown instructing the user to correct the mistake. More info: #685

</details>

## Fixes 🪲

<details><summary>ACL source generator wasn't filtering out internal public static fields</summary>

Generated ACL incorrectly contained the `Descriptions` property in the permission dictionary items due to not being filtered out correctly, which has now been fixed.

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