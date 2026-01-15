---

## ⚠️ Sponsorship Level Critically Low ⚠️

Due to low financial backing by the community, FastEndpoints will soon be going into "Bugfix Only" mode until the situation improves. Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Response sending method 'NotModifiedAsync'</summary>

A new response sending method has been added for sending a 304 status code response.

```csharp
public override async Task HandleAsync(CancellationToken c)
{
    await Send.NotModifiedAsync();
}
```

</details>

## Fixes 🪲

<details><summary>Index out of range exception in routeless test helpers</summary>

The routeless integration test helpers such as `.GETAsync<>()` would throw an exception when testing an endpoint configured with the root URL `/`, which has now been fixed.

</details>

## Breaking Changes ⚠️