---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Specify a label, summary & description for Swagger request examples</summary>

When specifying multiple swagger request examples, you can now specify the additional info like this:

```csharp
Summary(
    x =>
    {
        x.RequestExamples.Add(
            new(
                new MyRequest { ... },
                "label",
                "summary",
                "description"));
    });
```

</details>

## Improvements 🚀

## Fixes 🪲

## Breaking Changes ⚠️

<details><summary>The way multiple Swagger request examples are set has been changed</summary>

Previous way:

```csharp
Summary(s =>
{
    s.RequestExamples.Add(new MyRequest {...});
});
```

New way:

```csharp
s.RequestExamples.Add(new(new MyRequest { ... })); // wrapped in a RequestExample class
```

</details>