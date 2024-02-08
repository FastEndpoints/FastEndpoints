---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

## Improvements 🚀

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

## Breaking Changes ⚠️