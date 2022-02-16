# endpoint rate limiting
it is possible to rate limit individual endpoints based on the presence of an http header in the incoming request like below:
```csharp
public override void Configure()
{
    Post("/order/create");
    Throttle(
        hitLimit: 120,
        durationSeconds: 60,
        headerName: "X-Client-Id"); //this is optional
}
```

### hit limit & window duration

the above for example will only allow 120 requests from each unique client (identified by the header value) within a 60 second window. if 121 requests are made by a client within 60 seconds, a `429 too many requests` response will be automatically sent for the 121st request. the counter is reset every 60 seconds and the client is able to make another 120 requests in the next 60 seconds, and so on.

### header name
the header name can be set to anything you prefer. if it's not specified, the library will try to read the value of `X-Forwarded-For` header from the incoming request. if that's unsuccessful, it will try to read the `HttpContext.Connection.RemoteIpAddress` in order to uniquely identify the client making the request. if all attempts are unsuccessful, a `403 forbidden` response will be sent.

### header reliability
both `X-Forwarded-For` and `HttpContext.Connection.RemoteIpAddress` could be unreliable for uniquely identifying clients if they are behind a NAT, reverse proxy, or anonymizing vpn/proxy etc. therefore, the recommended strategy is to generate a unique identifier such as a GUID in your client application and use that as the header value in each request for the entirety of the session/app cycle.

### limitations & warnings
- should not be used for security or ddos protection. a malicious client can easily set a unique header value per request in order to circumvent the throttling.
- should be aware of the slight performance degradation due to resource allocation and amount of work being done.
- only per endpoint limits can be set. no global limits can be enforced. this won't ever be added due to performance reasons.
- consider a rate limiting solution that is out of process/ at the gateway level for better performance/security. 