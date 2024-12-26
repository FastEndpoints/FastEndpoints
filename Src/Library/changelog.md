---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

<details><summary>Migrate to xUnit v3 ⚠️</summary>

If you're using the `FastEndpoints.Testing` package in your test projects, take the following steps to migrate your projects:

1. Update all "FastEndpoints" package references in all of your projects to "5.33.0".
2. In your test project's `.csproj` file:
    1. Remove the package reference to the "xunit" v2 package.
    2. Add a package reference to the new "xunit.v3" library with version "1.0.0"
    3. Change the version of "xunit.runner.visualstudio" to "3.0.0"
3. Build the solution.
4. There might be compilation errors related to the return type of your derived `AppFixture<TProgram>` classes overridden methods such as `SetupAsync` and `TearDownAsync` methods. Simply change them from `Task` to `ValueTask` and the project should compile successfully.
5. If there are any compilation errors related to `XUnit.Abstractions` namespace not being found, simply delete those "using statements" as that namespace has been removed in xUnit v3.

After doing the above, it should pretty much be smooth sailing, unless your project is affected by the removal of previously deprecated classes as mentioned in the "Breaking Changes" section below.

</details>

<details><summary>Eliminate the need for [BindFrom(...)] attribute ⚠️</summary>

Until now, when binding from sources other than JSON body, you had to annotate request DTO properties with the `[BindFrom("my_field")]` attribute when the incoming field name is different to the DTO property name.
A new setting has now been introduced which allows you to use the same Json naming policy from the serializer for matching incoming request parameters without having to use the attribute.

```cs
app.UseFastEndpoints(c => c.Binding.UsePropertyNamingPolicy = true)
```

The setting is now enabled by default. Set it to `false` to go back to the previous behavior. If you'd like to be more explicit, you can still use the [BindFrom(...)] attribute which will take precedence even when the setting is enabled.

</details>


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

<details><summary>Deeply nested complex model binding from query parameters</summary>

Binding deeply nested complex DTOs from incoming query parameters is now supported. Please refer to the documentation [here](https://fast-endpoints.com/docs/model-binding#complex-query-binding).

</details>

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

<!-- ## Improvements 🚀 -->

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

<details><summary>Complex form data binding issue</summary>

When binding deeply nested form data with the `[FromForm]` attribute, if a certain deeply nested objects didn't have at least one primitive type property,
it would not get bound correctly. This has been fixed as well as the binding logic being improved.

</details>

## Breaking Changes ⚠️

<details><summary>Incoming field names to DTO property matching behavior</summary>

Starting with this update, the `Binding.UsePropertyNamingPolicy` setting is **enabled by default**.
This means that the same JSON naming policy from the serializer will now be used to match incoming request parameters to DTO properties,
eliminating the need for the `[BindFrom(...)]` attribute in most cases.

- **Previous Behavior:** When the incoming field name differed from the DTO property name, you had to explicitly use the `[BindFrom("my_field")]` attribute.
- **New Behavior:** The default binding now uses the JSON naming policy, matching field names without requiring the `[BindFrom(...)]` attribute.

#### Restore Previous Behavior

To revert to the previous behavior where property names are matched exactly without using the naming policy, set the following configuration:

```csharp
app.UseFastEndpoints(c => c.Binding.UsePropertyNamingPolicy = false)
```

#### Additional Notes

- If you prefer explicit binding, the `[BindFrom(...)]` attribute is still supported and takes precedence over the naming policy.
- Applications relying on the old behavior without configuring this setting may experience unexpected binding results and need adjustments.

</details>


<details><summary>Removal of deprecated classes (Testing related)</summary>

After following the xUnit v3 upgrade instructions above, you may be affected by the removal of the following previously deprecated classes:

- `TestFixture<TProgram>`: Use the `AppFixture<TProgram>` class instead.
- `TestClass<TFixture>`: Use the `TestBase<TFixture>` class instead.

</details>

<details><summary>Removal of constructor overloads from 'AppFixture&lt;TProgram&gt;'</summary>

Due to the migration to xUnit v3, the `AppFixture<TProgram>` base class no longer accepts `IMessageSink` and `ITestOutputHelper` arguments and only has a parameterless constructor.

</details>

<details><summary>Removal of undocumented [FromQueryParams] attribute</summary>

`[FromQueryParams]` was an undocumented feature that was put in place to help people migrating from old MVC projects to make the transition easier.
It was not documented due to its extremely poor performance and we wanted to discourage people from using query parameters as a means to submit complex data structures.

The newly introduced `[FromQuery]` attribute can be used now if you really must send complex query parameters. However, it is not a one-to-one replacement
as the query naming convention is quite strict and simplified.

</details>