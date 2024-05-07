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

<details><summary>Assembly level AppFixture support</summary>

todo:

- write docs

</details>

<details><summary>Test collection fixture support</summary>

todo:

- write docs

</details>

[//]: # (## Improvements 🚀)

[//]: # (## Fixes 🪲)

[//]: # (## Breaking Changes ⚠️)