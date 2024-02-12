---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

## Improvements 🚀

<details><summary>Relax DTO type constraint on 'Validator&lt;TRequest&gt;' class</summary>

The type constraint on the `Validator<TRequest>` class has been relaxed to `notnull` so that struct type DTOs can be validated.

</details>

## Fixes 🪲

<details><summary>Swagger UI displaying random text for email fields</summary>

When a FluentValidator rule is attached to a property that's an email address, Swagger UI was displaying a random string of characters instead of showing an email address. This has been rectified.

</details>

<details><summary>Swagger generation issue with form content and empty request DTO</summary>

Endpoints configured like below, where the request dto type is `EmptyRequest` and the endpoint allows form content; was causing the swagger processor to throw an error, which has been rectified.

```csharp
sealed class MyEndpoint : EndpointWithoutRequest<MyResponse>
{
    public override void Configure()
    {
        ...
        AllowFileUploads(); 
    }
}
```

</details>

<details><summary>Swagger issue with reference type DTO props being marked as nullable</summary>

Given a DTO such as this:

```csharp
sealed class MyRequest
{
    public string PropOne { get; set; }
    public string? PropTwo { get; set; }
}
```

The following swagger spec was generated before:

```json
"parameters": [
    {
        "name": "propOne",
        "in": "query",
        "required": true,
        "schema": {
            "type": "string",
            "nullable": true //this is wrong as property is not marked nullable
        }
    },
    {
        "name": "propTwo",
        "in": "query",
        "schema": {
            "type": "string",
            "nullable": true
        }
    }
]
```

Non-nullable reference types are not correctly generated as non-nullable.

</details>

## Breaking Changes ⚠️