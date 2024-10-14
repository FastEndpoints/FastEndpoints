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

<details><summary>Automatic transformation of 'ProblemDetails.Title' & 'ProblemDetails.Type' values according to 'StatusCode'</summary>

The `ProblemDetails` Title and Type values will now be automatically determined/transformed according to the `Status` code of the instance. The default behavior can be changed by setting your own `TypeTransformer` and `TitleTransformer` functions like so:

```csharp
app.UseFastEndpoints(
       cfg => cfg.Errors.UseProblemDetails(
           pCfg =>
           {
               pCfg.TypeTransformer
                   = pd =>
                     {
                         switch (pd.Status)
                         {
                             case 404:
                                 return "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.4";
                             case 429:
                                 return "https://www.rfc-editor.org/rfc/rfc6585#section-4";
                             default:
                                 return "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.1";
                         }
                     };
           }))
```

</details>

## Improvements 🚀

<details><summary>Route template syntax highlighting for VS and Rider</summary>

Route template items such as the following will now be correctly syntax highlighted in Rider and Visual Studio:

```csharp
Get("api/invoice/{id}/print")
```

</details>

<details><summary>Allow route param values with curly braces</summary>

The default request binder did not bind incoming route parameter values with curly braces such as:

```http
http://localhost:5000/invoice/{123-456}
```

</details>

## Fixes 🪲

<details><summary>Global 'TypeInfoResolver' not working</summary>

As reported by #783, there was an oversight in the way the built-in modifiers were checking the existence of custom attributes which lead to DTO properties being marked as "should not serialize".

</details>

<details><summary>Incorrect property name resolution of fluent validators with deeply nested DTOs</summary>

When json property naming policy is applied to fluentvalidation property chains, it was not correctly resolving the property chains for deeply nested request DTO properties.

</details>

## Minor Breaking Changes ⚠️