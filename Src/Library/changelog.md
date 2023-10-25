---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Model binding collections of 'IFormFile' from incoming form data</summary>

The following types of properties can now be automatically model bound from `file` form data fields.

```csharp
class Request
{
    public IEnumerable<IFormFile> Cars { get; set; }
    public List<IFormFile> Boats { get; set; }    
    public IFormFileCollection Jets { get; set; }
}
```

When submitting collections of form files, the incoming field names can be one of the following 3 formats:

|     | Format One | Format Two | Format Three |
|-----|------------|------------|--------------|
| # 1 | Cars       | Boats[1]   | Jets[]       |
| # 2 | Cars       | Boats[2]   | Jets[]       |

</details>

<details><summary>Multiple request examples for Swagger</summary>

Multiple examples for the request DTO can be specified by either setting the `ExampleRequest` property of the Summary class multiple times or adding to
the `RequestExamples` collection like so:

```csharp
Summary(s =>
{
    s.ExampleRequest = new MyRequest {...};  
    s.ExampleRequest = new MyRequest {...};
    s.RequestExamples.Add(new MyRequest {...});
});
```

</details>

<details><summary>Support for simple validation with 'DataAnnotations'</summary>

```csharp
sealed class Request
{
    [Required, StringLength(10, MinimumLength = 2)]
    public string Name { get; set; }
}

//can be used together with `FluentValidations` rules if need be

sealed class MyValidator : Validator<Request>
{
    public MyValidator()
    {
        RuleFor(x => x.Id).InclusiveBetween(10, 100);
    }
}
```

Note: there's no swagger integration for data annotations.

Thank you Wàn Yǎhǔ for the [contribution](https://github.com/FastEndpoints/FastEndpoints/pull/500).

</details>

<details><summary>Anti-forgery token validation middleware</summary>

Please see the [documentation page](https://fast-endpoints.com/docs/security#csrf-protection-for-form-submissions-antiforgery-tokens) for details of this feature.

Thank you Wàn Yǎhǔ for the [contribution](https://github.com/FastEndpoints/FastEndpoints/pull/509).

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

## Fixes 🪲

<details><summary>Response DTO property description not being detected</summary>

When the response DTO property description was provided by a lambda expression and the respective DTO property is also decorated with `[JsonPropertyName]` attribute,
the Swagger operation processor was not correctly setting the property description in generated Swagger spec. See #511 for more details.

</details>

[//]: # (## Minor Breaking Change ⚠️)