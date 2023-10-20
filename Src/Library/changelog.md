---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

<details><summary>Model binding collections of 'IFormFile' from incoming form data</summary>

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

<details><summary>Multiple request examples for Swagger</summary>

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

<details><summary>Ability to show error severity in 'ProblemDetails' response</summary>

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

## Improvements 🚀

<details><summary>Prevent swallowing of STJ exceptions in edge cases</summary>

If STJ throws internally after it has started writing to the response stream, those exceptions will no longer be swallowed.
This can happen in rare cases such as when the DTO being serialized has an infinite recursion depth issue.

</details>

<details><summary>Deep nested collection property name serialization support with 'AddError(expression, ...)' method</summary>

When doing a manual add error call like this:

```csharp
AddError(r => r.ObjectArray[i].Test, "Some error message");
```

Previous output was:

![](https://github.com/FastEndpoints/FastEndpoints/assets/10120072/99b866ff-30bb-4ec7-bf19-7957ecc1b882)

New output:

![](https://github.com/FastEndpoints/FastEndpoints/assets/10120072/b4d14887-bb99-4654-9e75-6fa31741f27e)

Thank you Mattis Bratland for the [contribution](https://github.com/FastEndpoints/FastEndpoints/pull/506)

</details>

<details><summary>Misc. improvements</summary>

- Upgrade dependencies to latest

</details>

<!-- ## Fixes 🪲 -->


<!-- ## Minor Breaking Change ⚠️ -->