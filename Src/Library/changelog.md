---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Multipart Form Data binding support for deeply nested complex DTOs</summary>

todo: write docs + write description here

</details>

<details><summary>Ability to disable FluentValidation+Swagger integration per rule</summary>

The built-in FV+Swagger integration can be disabled per property rule with the newly added `.SwaggerIgnore()` extension method as shown below.

```csharp
sealed class MyValidator : Validator<MyRequest>
{
    public MyValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .SwaggerIgnore();
    }
}
```

</details>

## Improvements 🚀

<details><summary>Route template syntax highlighting for VS and Rider</summary>

Route template items such as the following will now be correctly syntax highlighted in Rider and Visual Studio:

```csharp
Get("api/invoice/{id}/print")
```

</details>

## Fixes 🪲

<details><summary>Global 'TypeInfoResolver' not working</summary>

As reported by #783, there was an oversight in the way the built-in modifiers were checking the existence of custom attributes which lead to DTO properties being marked as "should not serialize".

</details>

## Minor Breaking Changes ⚠️