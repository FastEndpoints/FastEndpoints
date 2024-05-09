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

[//]: # (## Improvements 🚀)

[//]: # (## Fixes 🪲)

[//]: # (## Breaking Changes ⚠️)