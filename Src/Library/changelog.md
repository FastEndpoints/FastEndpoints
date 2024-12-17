---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

<details><summary>Control binding sources per DTO property</summary>

The default binding order is designed to minimize attribute clutter on DTO models. In most cases, disabling binding sources is unnecessary. However, for rare scenarios where a binding source must be explicitly blocked, you can now do the following:

```cs
[DontBind(Source.QueryParam | Source.RouteParam)] 
public string UserID { get; set; } 
```

The opposite approach can be taken as well, by just specifying a single binding source for a property like so:

```cs
[FormField]
public string UserID { get; set; }

[QueryParam]
public string UserName { get; set; }

[RouteParam]
public string InvoiceID { get; set; }
```

</details>

## Improvements 🚀

<details><summary>Swagger descriptions for deeply nested DTO properties</summary>

Until now, if you wanted to provide text descriptions for deeply nested request DTO properties, the only option was to provide them via XML document summary tags.
You can now provide descriptions for deeply nested properties like so:

```cs
Summary(
    s =>
    {
        s.RequestParam(r => r.Nested.Name, "nested name description");
        s.RequestParam(r => r.Nested.Items[0].Id, "nested item id description");
    });
```

Descriptions for lists and arrays can be provided by using an index `0` to get at the actual property.
Note: only lists and arrays can be used for this.

</details>

## Fixes 🪲

<details><summary>Incorrect latest version detection with 'Release Versioning' strategy</summary>

The new release versioning strategy was not correctly detecting the latest version of an endpoint if there was multiple endpoints for the same route such as a GET & DELETE endpoint on the same route.

</details>

<details><summary>Issue with nullability context not being thread safe in Swagger processor</summary>

In rare occasions where swagger documents were being generated concurrently, an exception was being thrown due to `NullabilityInfoContext` not being thread safe.
This has been fixed by implementing a caching mechanism per property type.

</details>

<details><summary>Complex DTO handling in routeless testing extensions</summary>

If the request DTO is a complex structure, testing with routeless test extensions like the following did not work correctly:

```cs
[Fact]
public async Task FormDataTest()
{
    var book = new Book
    {
        BarCodes = [1, 2, 3],
        CoAuthors = [new Author { Name = "a1" }, new Author { Name = "a2" }],
        MainAuthor = new() { Name = "main" }
    };

    var (rsp, res) = await App.GuestClient.PUTAsync<MyEndpoint, Book, Book>(book, sendAsFormData: true);

    rsp.IsSuccessStatusCode.Should().BeTrue();
    res.Should().BeEquivalentTo(book);    
}
```

</details>

<details><summary>Incorrect detection of generic arguments of generic commands</summary>

There was a minor oversight in correctly detecting the number of generic arguments of generic commands if there was more than one. 
This has been fixed to correctly detect all generic arguments of generic commands.

</details>

## Minor Breaking Changes ⚠️