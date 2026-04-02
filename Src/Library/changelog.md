---

## ⚠️ Sponsorship Level Critically Low ⚠️

Due to low financial backing by the community, FastEndpoints will soon be going into "Bugfix Only" mode until the situation improves. Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Generate a hydrated test URL from an endpoint type and request DTO</summary>

Testing extensions now expose `GetTestUrlFor<TEndpoint>(object request)` publicly. This lets you resolve the final routeless test URL for an endpoint without actually sending the request. Route parameters are populated from the supplied DTO instance, query string values are appended automatically, and the method also works in Aspire-style black-box tests by loading the endpoint URL cache over HTTP when necessary.

This is useful when you want to inspect or assert the exact URL that a test request would use before calling `GETAsync()`, `POSTAsync()`, etc.

```csharp
var request = new UpdateInvoiceRequest
{
    InvoiceId = 123,
    IncludeLines = true
};

var url = client.GetTestUrlFor<UpdateInvoiceEndpoint>(request);

url.ShouldBe("api/invoices/123?IncludeLines=true");
```

</details>

## Fixes 🪲

## Improvements 🚀

## Breaking Changes ⚠️