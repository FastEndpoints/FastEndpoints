---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

<details><summary>API change of response sending methods ⚠️</summary>

Response sending methods such as `SendOkAsync()` have been ripped out of the endpoint base class for a better intellisense experience and extensibility.

Going forward, the response sending methods are accessed via the `Send` property of the endpoint as follows:

```cs
public override async Task HandleAsync(CancellationToken c)
{
    await Send.OkAsync("hello world!");
}
```

In order to add your own custom response sending methods, simply target the `IResponseSender` interface and write extension methods like so:

```cs
static class SendExtensions
{
    public static Task HelloResponse(this IResponseSender sender)
        => sender.HttpContext.Response.SendOkAsync("hello!");
}
```

This is obviously is a wide-reaching breaking change which can be easily remedied with a quick regex based find & replace. Please see the breaking changes section below for step-by-step instructions on how to migrate. Takes less than a minute.

</details>

<details><summary>Send multiple Server-Sent-Event models in a single stream</summary>

It is now possible to send different types of data in a single SSE stream with the use of a wrapper type called **StreamItem** like so:

```cs
public override async Task HandleAsync(CancellationToken ct)
{
    await Send.EventStreamAsync(GetMultiDataStream(ct), ct);

    async IAsyncEnumerable<StreamItem> GetMultiDataStream([EnumeratorCancellation] CancellationToken ct)
    {
        long id = 0;

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(1000, ct);

            id++;

            if (DateTime.Now.Second % 2 == 1)
                yield return new StreamItem(id.ToString(), "odd-second", Guid.NewGuid()); //guide data
            else
                yield return new StreamItem(id.ToString(), "even-second", "hello!"); //string data
        }
    }
}
```

By default, the `StreamItem` will be serialized as a JSON object, but you can change this by inheriting from it and overriding the `GetDataString` method to return a different format such as XML or plain text.

</details>

<details><summary>Ability to customize param names for strongly typed route params</summary>

It is now possible to customize the route param names when using the [strongly typed route params](https://fast-endpoints.com/docs/misc-conveniences#strongly-typed-route-parameters) feature by simply decorating the target dto property with a `[BindFrom("customName"))]` attribute. If a `BindFrom` attribute annotation is not present on the property, the actual name of the property itself will end up being the route param name.

</details>

<details><summary>Support for malformed JSON array string binding</summary>

When submitting requests via SwaggerUI where a complex object collection is to be bound to a collection property of a DTO, SwaggerUI sends in a malformed string of JSON objects without properly enclosing them in the JSON array notation `[...]` such as the following:

```json
{"something":"one"},{"something":"two"}
```

whereas it should be a proper JSON array such as this:

```json
[{"something":"one"},{"something":"two"}]
```

Since we have no control over how SwaggerUI behaves, support has been added to the default request binder to support parsing and binding the malformed comma separateed JSON objects that SwaggerUI sends at the expense of a minor performance hit.

</details>

<details><summary>Auto infer query parameters for routeless integration tests</summary>

If you annotate request DTO properties with `[RouteParam]` attribute, the helper extensions such as `.GETAsync()` will now automatically populate
the request query string with values from the supplied dto instance when sending integration tests.

```cs
sealed class MyRequest
{
    [RouteParam]
    public string FirstName { get; set; }

    public string LastName { get; set; }
}

[Fact]
public async Task Query_Param_Test()
{
    var request = new MyRequest
    {
        FirstName = "John", //will turn into a query parameter
        LastName = "Gallow" //will be in json body content
    };
    var result = await App.Client.GETAsync<MyEndpoint, MyRequest, string>(request);
}
```

</details>

<!-- ## Improvements 🚀 -->

## Fixes 🪲

<details><summary>Header example value not picked up from swagger example request</summary>

If a request DTO specifies a custom header name that is different from the property name such as the following:

```cs
sealed class GetItemRequest
{
    [FromHeader("x-correlation-id")]
    public Guid CorrelationId { get; init; }
}
```

and a summary example request is provided such as the following:

```cs
Summary(s => s.ExampleRequest = new GetItemRequest()
{
    CorrelationId = "54321"
});
```

the example value from the summary example property was not being picked up due to an oversight.

</details>

<details><summary>Xml comments not picked up by swagger for request schema</summary>

There was a regression in the code path that was picking up `Summary` xml comments from DTO properties in certain scenarios, which has now been fixed.

</details>

<details><summary>Incorrect swagger example generation when '[FromBody] or [FromForm]' was used</summary>

If a request DTO was defined like this:

```cs
sealed class MyRequest
{
    [FromBody]
    public Something Body { get; set; }
}
```

and an example request is provided via the Summary like this:

```cs
Summary(x=>x.ExampleRequest = new MyRequest()
{
    Body = new Something()
    {
        ...
    }
});
```

swagger generated the incorrect request example value which included the property name, which it shouldn't have.

</details>

<details><summary>Default exception handler setting incorrect mime type</summary>

Due to an oversight, the [default exception handler](https://fast-endpoints.com/docs/exception-handler) was not correctly setting the intended content-type value of `application/problem+json`. Instead, it was being overwritten with `application/json` due to not using the correct overload of `WriteAsJsonAsync()` method internally.

</details>

## Minor Breaking Changes ⚠️

<details><summary>API change of endpoint response sending methods</summary>

The response sending methods are no longer located on the endpoint class itself and are now accessed via the `Send` property of the endpoint.
This is a breaking change which you can easily fix by doing a quick find+replace using a text editor such as VSCode. Please follow the following steps in order to update your files:

1. Open the top level folder of where your endpoint classes exist in the project in a text editor like VSCode.
2. Click `Edit > Replace In Files` and enable `Regex Matching`
3. Use `(?<!\.)\bSend(?=[A-Z][A-Za-z0-9_]*Async\b)` as the regex to find matches to target for editing.
4. Enter `Send.` in the replacement field and hit `Replace All`
5. Then use `(?<!\.)\bSendAsync\b` as the regex.
6. Enter `Send.OkAsync` as the replacement and hit `Replace All` again.
7. Build the project and profit!

**Note:** In case some `Send.OkAsync()` calls won't compile, it's most likely you were using the `SendAsync()` overload that allowed to set a custom status code, and all you have to do to fix it is to use the `Send.ResponseAsync()` method instead of `Send.OkAsync()` as `OkAsync()` doesn't allow custom status codes.

Here's a complete [walkthrough](https://imgur.com/j0OVrKp) of the above process.

</details>

<details><summary>Small change in the Server-Sent-Event response stream</summary>

Previously the Server-Sent-Event response was written as:

``` plain
id:12345
event: my-event
data: hello world!


```

Notice the inconsistency in the spacing between the `id`, `event` and `data` fields. This has now been fixed to be consistent with the following format:

``` plain
id: 12345
event: my-event
data: hello world!


```

</details>