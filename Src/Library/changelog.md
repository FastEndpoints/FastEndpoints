---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>.NET 9.0 SDK Support</summary>

Migration to .NET 9 has been completed. We're currently referencing the GA build of the SDK. Once RTM comes out, the references will be updated in the following FastEndpoints release. The GA build seems to be quite stable and suitable for production use.

</details>

<details><summary>Source generator for avoiding reflection cost</summary>

todo: write docs + write description here

#### Automatic fallback for:

- All properties of Records
- Init only properties of any class

</details>

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

The `ProblemDetails` Title and Type values will now be automatically determined/transformed according to the `Status` code of the instance. The [default behavior](https://github.com/FastEndpoints/FastEndpoints/blob/0ff9555cd6a99ca19bcfe4ad7c458d5e2d2e04ff/Src/Library/Config/ErrorOptions.cs#L112-L120) can be changed by setting your own `TypeTransformer` and `TitleTransformer` functions like so:

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

<details><summary>Enhance Swagger UI search bar behavior</summary>

The Swagger UI search bar is only capable of searching/filtering operations by tag values. The search bar has been enhanced via a custom injected JS plugin to be able to search the following sources:

- Operation paths
- Summary text
- Description text
- Operation parameters
- Request schema
- Response schema

</details>

<details><summary>Extension method to display Swagger operation IDs in Swagger UI</summary>

Calling the following extension method will cause the operation ids to show up in the swagger ui.

```csharp
app.UseSwaggerGen(uiConfig: u => u.ShowOperationIDs());
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

<details><summary>Better handling of JsonIgnore attribute condition</summary>

The `[JsonIgnore]` attribute on request/response DTO properties will now be taken into consideration only if it's declared in either the following two ways:

```csharp
[JsonIgnore] //without specifying a condition (defaults to JsonIgnoreCondition.Always)

[JsonIgnore(Condition = JsonIgnoreCondition.Always)]
```

This change only applies to Swagger generation and Non-STJ model binding behavior.

</details>

## Fixes 🪲

<details><summary>Global 'TypeInfoResolver' not working</summary>

As reported by #783, there was an oversight in the way the built-in modifiers were checking the existence of custom attributes which lead to DTO properties being marked as "should not serialize".

</details>

<details><summary>Incorrect property name resolution of fluent validators with deeply nested DTOs</summary>

When json property naming policy is applied to fluentvalidation property chains, it was not correctly resolving the property chains for deeply nested request DTO properties.

</details>

<details><summary>'[FromBody]' attribute overriding media-type in Swagger</summary>

The usage of `[FromBody]` attribute was incorrectly overriding the user specified media-type value to `application/json`. Info: #800

</details>

<details><summary>Workaround for OpenApi bug in .NET 9.0 SDK</summary>

There's a bug in .NET 9.0 where any response DTOs implementing the `IResult` interface will be filtered out from swagger documentation. A workaround has been put in place since a fix will not be available until .NET 9.1 or later according the ASPNET team. More info: #803

</details>

## Minor Breaking Changes ⚠️