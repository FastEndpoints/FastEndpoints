---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Multi-level test-collection ordering</summary>

Tests can now be ordered by prioritizing test-collections, test-classes in those collections as well as tests within the classes for fully controlling the order of test execution when test-collections are involved. [See here](https://fast-endpoints.com/docs/integration-unit-testing#ordering-tests-in-collections) for a usage example.

</details>

<details><summary>Customize character encoding of JSON responses</summary>

A new config setting has been added to be able to customize the charset of JSON responses. `utf-8` is used by default. can be set to `null` for disabling the automatic appending of the charset to the `Content-Type` header of responses.

```csharp
app.UseFastEndpoints(c => c.Serializer.CharacterEncoding = "utf-8")
```

</details>

<details><summary>Setting for allowing [JsonIgnore] attribute on 'required' DTO properties</summary>

STJ typically [does not allow](https://github.com/dotnet/runtime/issues/82879) `required` properties to be annotated with `[JsonIgnore]` attribute. The following doesn't work out of the box:

```csharp
public class MyRequest
{
    [JsonIgnore]
    public required string Id { get; init; }
}
```

The following setting is now enabled by default allowing you to annotate `required` properties with `[JsonIgnore]`:

```csharp
app.UseFastEndpoints(c => c.Serializer.EnableJsonIgnoreAttributeOnRequiredProperties = true)
```

It's necessary to decorate `required` properties with `[JsonIgnore]` in situations where the same property is bound from multiple sources.

</details>

<details><summary>Read form-field values together with form-file sections</summary>

A new method has been added to conveniently read regular form-field values when they come in with multi-part form data requests while [buffering is turned off](https://fast-endpoints.com/docs/file-handling#handling-large-file-uploads).

```csharp
await foreach (var sec in FormMultipartSectionsAsync(ct))
{
    //reading the value of a form field
    if (sec.IsFormSection && sec.FormSection.Name == "formFieldName")
    {
        var formFieldValue = await sec.FormSection.GetValueAsync(ct);
    }

    //obtaining the stream of a file
    if (sec.IsFileSection && sec.FileSection.Name == "fileFieldName")
    {
        var fileStream = sec.FileSection.FileStream;
    }
}
```

</details>

## Improvements 🚀

<details><summary>Remove dependency on 'Xunit.Priority' package</summary>

The 'Xunit.Priority' package is no longer necessary as we've implemented our own test-case-orderer. If you've been using test ordering with the `[Priority(n)]` attribute, all you need to do is get rid of any `using` statements that refer to `XUnit.Priority`.

</details>

## Fixes 🪲

<details><summary>Issue with FluentValidation+Swagger integration</summary>

When a child validator has no parameterless constructor, the FV+Swagger integration was not able to construct an instance of the child validator causing it to be ignored when generating the Swagger spec. Child validators will now be instantiated via FE's service resolver in the validation schema processor to mitigate this issue.

</details>

## Minor Breaking Changes ⚠️