---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Support for cancellation of queued jobs</summary>

Queuing a command as a job now returns a **Tracking Id** with which you can request cancellation of a queued job from anywhere/anytime like so:

```csharp
var trackingId = await new LongRunningCommand().QueueJobAsync();

await JobTracker<LongRunningCommand>.CancelJobAsync(trackingId);
```

Use either use the **JobTracker&lt;TCommand&gt;** generic class or inject a **IJobTracker&lt;TCommand&gt;** instance from the DI Container to access the **CancelJobAsync()** method.

**NOTE:** This feature warrants a minor breaking change. See how to upgrade below.

</details>

<details><summary>Check if app is being run in Swagger Json export mode and/or Api Client Generation mode</summary>

You can now use the following new extension methods for conditionally configuring your middleware pipeline depending on the mode the app is running in:

#### WebApplicationBuilder Extensions

```csharp
bld.IsNotGenerationMode(); //returns true if running normally
bld.IsApiClientGenerationMode(); //returns true if running in client gen mode
bld.IsSwaggerJsonExportMode(); //returns true if running in swagger export mode
```

#### WebApplication Extensions

```csharp
app.IsNotGenerationMode(); //returns true if running normally
app.IsApiClientGenerationMode(); //returns true if running in client gen mode
app.IsSwaggerJsonExportMode(); //returns true if running in swagger export mode
```

</details>

<details><summary>[AllowFileUploads] attribute for endpoint class decoration</summary>

When using attribute based configuration of endpoints you can now enable file upload support for endpoints like so:

```csharp
[HttpPost("form"), AllowFileUploads]
sealed class MyEndpoint : Endpoint<MyRequest>
{
    
}
```

</details>

<details><summary>Blazor Wasm support for Remote messaging client</summary>

A new package has been added `FastEndpoints.Messaging.Remote.Core` which contains only the core functionality along with a client that's capable of running in the web browser with Blazor Wasm.

</details>

<details><summary>Bypass endpoint throttle limits with integration tests</summary>

Integration tests can now bypass the throttle limits enforced by endpoints if they need to like so:

```csharp
[Fact]
public async Task Throttle_Limit_Bypassing_Works()
{
    var client = App.CreateClient(new()
    {
        ThrottleBypassHeaderName = "X-Forwarded-For" //must match with what the endpoint is looking for
    });

    for (var i = 0; i < 100; i++)
    {
        var (rsp, _) = await client.GETAsync<ThrottledEndpoint, Response>();
        rsp.IsSuccessStatusCode.Should().BeTrue();
    }
}
```

Each request made through that client would then automatically contain a `X-Forwarded-For` header with a unique value per request allowing the test code to bypass the throttle limits set by the endpoint.

</details>

## Improvements 🚀

<details><summary>Change default redirection behavior of cookie authentication middleware</summary>

The default behavior of the ASP.NET cookie auth middleware is to automatically return a redirect response when current user is either not authenticated or unauthorized. This default behavior is not appropriate for REST APIs because there's typically no login UI page as part of the backend server to redirect to, which results in a `404 - Not Found` error which confuses people that's not familiar with the cookie auth middleware. The default behavior has now been overridden to correctly return a `401 - Unauthorized` & `403 - Forbidden` as necessary without any effort from the developer.

</details>

<details><summary>Change 'RoleClaimType' of cookie auth to non-soap value</summary>

Until now, the `CookieAuth.SignInAsync()` method was using the long soap version of 'Role Claim Type' value `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` which is not in line with what FE uses for JWT tokens. Now both JWT & Cookie auth uses the same value from the global config which is set like below or it's default value `role`:

```csharp
app.UseFastEndpoints(c=>c.Security.RoleClaimType = "role");
```

</details>

<details><summary>Workaround for CookieAuth middleware 'IsPersistent' misbehavior</summary>

By default, in ASP.NET Cookie middleware, if you specify an `Expiry` or `Max-Age` at the global/middleware level, setting `IsPersitent = false` will have no effect when signing in the user, as the middleware sets `Expiry/Max-Age` on the generated cookie anyway, making it a persistent cookie. A workaround has been implemented to fix this behavior.

</details>

## Fixes 🪲

<details><summary>[HideFromDocs] attribute missing issue with the source generator</summary>

If the consuming project didn't have a `global using FastEndpoints;` statement, the generated classes would complain about not being able to located the said attribute, which has now been rectified.

</details>

<details><summary>Service registration source generator issue with third party source generators</summary>

The service registration source generator was encountering a compatibility issue with partial classes generated by other source generators such as Mapster, which has no been fixed.

</details>

<details><summary>Swagger UI issue with path param names that are substrings</summary>

If a route contains multiple path parameters where one is a substring of another, the generated swagger spec would cause Swagger UI to not match the path param correctly. An example of this would be a route such as the following:

```
/api/parents/{ParentId}/children/{Id}
```

Path segment matching has been changed to include the parenthesis as well in order to prevent substring matching.

</details>

<details><summary>Default values for Enum properties not displayed in Swagger UI</summary>

Enum property default values were not being displayed in Swagger UI due to incorrectly generated Swagger Spec due to a bug in NSwag. A workaround has been implemented to generate the correct spec.

</details>

<details><summary>Incorrect property name detection for STJ deserialization exceptions</summary>

When deserializing Enum arrays from query parameters, if one of the values is invalid, the following error response was generated with an incorrect property name:

```json
{
  "statusCode": 400,
  "message": "One or more errors occurred!",
  "errors": {
    "0]": [
      "The JSON value could not be converted to MyEnum. Path: $[0] | LineNumber: 0 | BytePositionInLine: 9."
    ]
  }
}
```

Which has been now corrected to provide a better error message and the correct property name as follows:

```json
{
    "statusCode": 400,
    "message": "One or more errors occurred!",
    "errors": {
        "myEnumValues": [
            "Value [Xyz] is not valid for a [MyEnum[]] property!"
        ]
    }
}
```

</details>

<details><summary>Default response/produces metadata was ignored for 'IResult' response types</summary>

There was an oversight in adding default response metadata to endpoints that were returning 'IResult' types, which has now been rectified.

</details>

## Minor Breaking Changes ⚠️

<details><summary>Job storage record &amp; storage provider update</summary>

In order to support the new job cancellation feature, the following steps must be taken for a smooth migration experience:

1. Purge all existing queued jobs from storage or allow them to complete before deploying new version.
2. Add a `Guid` property called `TrackingID` to the [job storage record entity](https://github.com/FastEndpoints/Job-Queue-Demo/blob/main/src/Storage/JobRecord.cs#L8) and run a migration if using EF Core.
3. Implement `CancelJobAsync()` method on the [job storage provider](https://github.com/FastEndpoints/Job-Queue-Demo/blob/main/src/Storage/JobProvider.cs#L23-L28)

</details>