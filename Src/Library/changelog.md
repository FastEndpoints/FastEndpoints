
---

### ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

<!-- <details><summary>title text</summary></details> -->

<!-- ## 🔖 New -->


<!-- ## 🚀 Improvements -->


<!-- ## 🪲 Fixes -->


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