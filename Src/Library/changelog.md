---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Jwt token revocation middleware</summary>

Jwt token revocation can be easily implemented with the newly provided abstract class like so:

```csharp
public class JwtBlacklistChecker(RequestDelegate next) : JwtRevocationMiddleware(next)
{
    protected override Task<bool> JwtTokenIsValidAsync(string jwtToken, CancellationToken ct)
    { 
        //return true if the supplied token is still valid
    }
}
```

Simply register it before any auth related middleware like so:

```csharp
app.UseJwtRevocation<JwtBlacklistChecker>()
   .UseAuthentication()
   .UseAuthorization()
```

</details>

<details><summary>Ability to override JWT Token creation options per request for Refresh Tokens</summary>

A couple of new [optional hooks](https://github.com/FastEndpoints/FastEndpoints/blob/5afe7db3628e08fc4515af17701410b4a35f182b/Src/Security/RefreshTokens/RefreshTokenService.cs#L55-L91) have been added that can be tapped in to if you'd like to modify Jwt token creation parameters per request, and also modify the token response per request before it's sent to the client. Per request token creation parameter modification may be useful when allowing the client to decide the validity of tokens.

</details>

<details><summary>Ability to subscribe to gRPC Events from Blazor Wasm projects</summary>

Until now, only gRPC Command initiations were possible from within Blazor Wasm projects. Support has been added to the `FastEndpoints.Messaging.Remote.Core` project which is capable of running in the browser to be able to act as a subscriber for Event broadcasts from a gRPC server. [See here](https://github.com/FastEndpoints/Blazor-Wasm-Remote-Messaging-Demo) for a sample project showcasing both.

</details>

## Improvements 🚀

<details><summary>Get rid of Swagger middleware ordering requirements</summary>

Swagger middleware ordering is no longer important. You can now place the `.SwaggerDocument()` and `.UseSwaggerGen()` calls wherever you prefer.

</details>

<details><summary>Swagger OneOf support for polymorphic request/response DTOs</summary>

Correctly annotated polymorphic base types can now be used as request/response DTOs. The possible list of derived types will be shown in Swagger UI under a `OneOf` field. To enable, decorate the base type with the correct set of attributes like so:

```csharp
public class Apple : Fruit
{
    public string Foo { get; set; }
}

public class Orange : Fruit
{
    public string Bar { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "_t")]
[JsonDerivedType(typeof(Apple), "a")]
[JsonDerivedType(typeof(Orange), "o")]
public class Fruit
{
    public string Baz { get; set; }
}
```

And set the following setting to `true` when defining the Swagger document:

```csharp
builder.Services.SwaggerDocument(c => c.UseOneOfForPolymorphism = true)
```

</details>

## Fixes 🪲

<details><summary>Swagger generator issue with [FromBody] properties</summary>

The referenced schema was generated as a `OneOf` instead of a direct `$ref` when a request DTO property was being annotated with the `[FromBody]` attribute.

</details>

<details><summary>Swagger route parameter detection issue</summary>

The Nswag operation processor did not correctly recognize route parameters in the following form:

```csharp
api/a:{id1}:{id2}
```

Which has now been corrected thanks to PR #735

</details>

<details><summary>Kiota client generation issue with 'clean output' setting</summary>

If the setting for cleaning the output folder was enabled, Kiota client generation was throwing an error that it can't find the input swagger json file, because Kiota deletes everything in the output folder when that setting is enabled. From now on, if the setting is enabled, the swagger json file will be created in the system temp folder instead of the output folder.

</details>

<details><summary>Graceful shutdown issue of gRPC streaming</summary>

Server & Client Streaming was not listening for application shutdown which made app shutdown to wait until the streaming was finished. It has been fixed to be able to gracefully terminate the streams if the application is shutting down.

</details>

[//]: # (## Minor Breaking Changes ⚠️)