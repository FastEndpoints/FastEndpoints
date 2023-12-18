---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Api Client generation using Kiota</summary>

Todo: update doc page and link here.

ref: https://discord.com/channels/933662816458645504/1114736030109683782/1182711087901184021

</details>

<details><summary>Ability to specify/obtain descriptions with ACL generation</summary>

todo: update docs and link here.
ref: https://github.com/FastEndpoints/FastEndpoints/issues/562

</details>

<details><summary>[HideFromDocs] attribute for removing properties from Swagger schema</summary>

```csharp
sealed class MyRequest
{
    [HideFromDocs]
    public int Internal { get; set; } //this will not appear in swagger schema

    public string Name { get; set; }
}
```

</details>

## Improvements 🚀

<details><summary>Treat validation rules with conditions attached as optional properties in Swagger spec.</summary>

If a validation rule is conditional, like in the example below, that particular DTO property will be considered optional and will not be marked as required in the
Swagger Schema.

```csharp
RuleFor(x => x.Id) //this property will be a required property in the swagger spec
    .NotEmpty();   //because there's no 'When(...)' condition attached to it.

RuleFor(x => x.Age) //this will be an optional property in swagger spec because
    .NotEmpty()     //'NotEmpty()' is conditional.
    .When(SomeCondition);
```

For this to work, the rules have to be written separately as above. I.e. the `.When(...)` condition must proceed immediately after the `.NotEmpty()` or `.NotNull()` rule.

</details>

<details><summary>Support for 'UrlSegmentApiVersionReader' of 'Asp.Versioning.Http'</summary>

Only the `HeaderApiVersionReader` was previously supported. Support for doing versioning based on URL segments using the `Asp.Versioning.Http` package is now working
correctly.

</details>

<details><summary>Micro optimization with 'Concurrent Dictionary' usage</summary>

Concurrent dictionary `GetOrAdd()` overload with lambda parameter seems to perform a bit better in .NET 8. All locations that were using the other overload was
changed to use the overload with the lambda.

</details>

## Fixes 🪲

<details><summary>'JsonNamingPolicy.SnakeCaseLower' was causing incorrect Swagger Schema properties</summary>

Snake case policy did not exist before .NET 8, so it's usage was not accounted for in the Swagger operation processor, which has now been corrected.

</details>

[//]: # (## Breaking Changes ⚠️)