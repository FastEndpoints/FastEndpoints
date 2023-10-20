---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

<details><summary>Ability to model bind collections of 'IFormFile' from incoming form data</summary>

The following forms of properties can now be model bound from file form data fields.

```csharp
class Request
{
    public IEnumerable<IFormFile> Cars { get; set; }
    public List<IFormFile> Boats { get; set; }    
    public IFormFileCollection Jets { get; set; }
}
```

</details>

<details><summary>Ability to specify multiple request examples for Swagger</summary>

Multiple examples for the request DTO can be specified by setting the `ExampleRequest` property of the Summary class multiple times like so:

```csharp
Summary(s =>
{
    s.ExampleRequest = new()
    {
        Description = "first",
        Name = "name one",
    };
    
    s.ExampleRequest = new()
    {
        Description = "second",
        Name = "name two",
    };
});
```

</details>

## Improvements 🚀

<details><summary>Prevent swallowing of STJ exceptions in edge cases</summary>

If STJ throws internally after it has started writing to the response stream, those exceptions will no longer be swallowed.
This can happen in rare cases such as when the DTO being serialized has an infinite recursion depth issue.

</details>

<details><summary>Ability to show error severity in ProblemDetails response</summary>

The `FluentValidation.Severity` can now be serialized to the `ProblemDetails` response by enabling it like so:

```csharp
app.UseFastEndpoints(
    c =>
    {
        ProblemDetails.Error.IndicateSeverity = true;
        c.Errors.UseProblemDetails();
    });
```

</details>

<details><summary>Misc. improvements</summary>

- Upgrade dependencies to latest

</details>

<!-- ## Fixes 🪲 -->


<!-- ## Minor Breaking Change ⚠️ -->