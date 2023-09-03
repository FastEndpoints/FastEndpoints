
---

### ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

<!-- <details><summary>title text</summary></details> -->

## 🔖 New

<details><summary>'FastEndpoints.Testing' helper library for Integration testing</summary>

Please see the [documentation](https://fast-endpoints.com/docs/integration-unit-testing#integration-testing) for details.

</details>

<!-- ## 🚀 Improvements -->

## 🪲 Fixes

<details><summary>Example generation for swagger nullable request params</summary>

Swagger schema example was being auto generated for request parameters even if the field (DTO property) is nullable. See bug report [here](https://github.com/FastEndpoints/FastEndpoints/issues/477). Which is not the desired behavior. 

Now the examples are only auto generated if developer hasn't decorated the property with a XML example or `[DefaultValue(...)]` attribute for nullable properties.

Non-nullable properties will always get the example/default values filled in the following order: `[DefaultValue(...)]` attribute > XML Comment > Auto generated example

</details>


## ⚠️ Minor Breaking Changes

<details><summary>Type discovery source generator behavior change</summary>

The source generator no longer automatically discovers types from referenced assemblies/projects.
You now have to add the `FastEndpoints.Generator` package to each project you'd like to use type discovery with and register the discovered types per assembly like so:
```cs
builder.Services.AddFastEndpoints(o =>
{
    o.SourceGeneratorDiscoveredTypes.AddRange(MyApplication.DiscoveredTypes.All);
    o.SourceGeneratorDiscoveredTypes.AddRange(SomeClassLib.DiscoveredTypes.All);
})
```

</details>