# endpoint options
in addition to the convenient methods you can use in the endpoint configuration to setup your endpoints (mentioned in previous pages), you can use the `Options()` method to customize aspects of endpoint registration/setup like so:
```csharp
Options(b => b.RequireCors(x => x.AllowAnyOrigin())
              .RequireHost("domain.com")
              .ProducesProblem(404));
```

# shorthand route configuration

instead of the `Verbs() and Routes()` combo, you can use the shorthand versions that combines them with `Get(), Post(), Put(), Patch(), Delete()` when configuring your endpoints like so:
```csharp
public override void Configure( )
{
    Get("/api/customer/{CustomerID}");
}
```
the above is equivalent to using both `Verbs() and Routes()`. do note that you can't configure multiple verbs with the shorthand version. you can however setup multiple route patterns with the shorthand methods.

# endpoint properties
the following properties are available to all endpoint classes.

#### BaseURL (string)
the base url of the current request in the form of `https://hostname:port/` (includes trailing slash). if your server is behind a proxy/gateway, use the [forwarded headers middleware](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer) to get the correct address.

#### Config (IConfiguration)
gives access to current configuration of the web app

#### Env (IWebHostEnvironment)
gives access to the current web hosting environment

#### Files (IFormFileCollection)
exposes the uploaded file collection in case of `multipart/form-data` uploads.

#### Form (IFormCollection)
exposes the form data in case of `application/x-www-form-urlencoded` or `multipart/form-data` uploads.

#### HttpContext (HttpContext)
gives access to the current http context of the request.

#### HttpMethod (Http enum value)
the http method of the current request as an enum value.

#### Logger (ILogger)
the default logger for the current endpoint type

#### Response (TResponse)
exposes a blank response dto for the current endpoint before the endpoint handler is executed. or represents the populated response dto after a response has been sent to the client.

#### User (ClaimsPrincipal)
the current claims principal associated with the current request.

#### ValidationFailed (bool)
indicates the current validation status

#### ValidationFailures (List\<ValidationFailure\>)
the list of validation failures for the current execution context.

# send methods
the following **[response sending methods](xref:FastEndpoints.Endpoint`2.SendAsync(`1,System.Int32,CancellationToken))** are available for use from within endpoint handlers:

#### SendAsync()
sends a given response dto or any object that can be serialized as json down to the requesting client.

#### SendCreatedAtAsync()
sends a 201 created response with a `Location` header containing where the resource can be retrieved from. **[see note](Swagger-Support.md#custom-endpoint-names)** about using with custom endpoint names.

#### SendStringAsync()
sends a given string to the client in the response body

#### SendOkAsync()
sends a 200 ok response without any body.

#### SendNoContentAsync()
sends a 204 no content response

#### SendRedirectAsync()
sends a 30X moved response with a location header containing the url to redirect to.

#### SendErrorsAsync()
sends a 400 error response with the current list of validation errors describing the validation failures.

#### SendNotFoundAsync()
sends a 404 not found response

#### SendUnauthorizedAsync()
sends a 401 unauthorized response

#### SendForbiddenAsync()
sends a 403 unauthorized response

#### SendBytesAsync()
sends a byte array to the client

#### SendFileAsync()
sends a file to the client

#### SendStreamAsync()
sends the contents of a stream to the client

#### SendEventStreamAsync()
sends a "server-sent-events" data stream to the client

# hook methods
the following 4 hook methods allow you to do something before and after dto validation as well as handler execution.

#### OnBeforeValidate()
override this method if you'd like to do something to the request dto before it gets validated.

#### OnAfterValidate()
override this method if you'd like to do something to the request dto after it gets validated.

#### OnValidationFailed()
override this method if you'd like to do something when validation fails. 

#### OnBeforeHandle()
override this method if you'd like to do something to the request dto before the handler is executed.

#### OnAfterHandle()
override this method if you'd like to do something after the handler is executed.