# request dto binding
the endpoint handlers are supplied with fully populated request dtos. the dto property values are automatically bound from the incoming request, from the following sources in the exact order:

1. json body
2. form data
3. route parameters
4. query parameters
5. user claims (if property has [FromClaim] attribute)

consider the following request dto and http request:

**dto**
```csharp
public class GetUserRequest
{
    public int UserID { get; set; }
}
```

**http request**
```
pattern : /api/user/{UserID}
url     : /api/user/54321

json    : { "UserID": 12345 }
```

when the handler receives the request dto, the value of `UserID` will be `54321` because route parameters have higher priority than json body.

likewise, if you decorate the `UserID` property with `[FromClaim]` attribute like so:
```csharp
public class GetUserRequest
{
    [FromClaim]
    public int UserID { get; set; }
}
```
the value of `UserID` will be whatever claim value the user has for the claim type `UserID` in their claims. by default if the user does not have a claim type called `UserID`, then a validation error will be sent automatically to the client. you can make the claim optional by using the following overload of the attribute:
```java
[FromClaim(IsRequired = false)]
```
doing so will allow the endpoint handler to execute even if the current user doesn't have the specified claim and model binding will take the value from the highest priority source of the other binding sources mentioned above (if a matching field/route param is present). an example can be seen [here](https://github.com/dj-nitehawk/FastEndpoints/blob/main/Web/%5BFeatures%5D/Customers/Update/Endpoint.cs).

# route parameters
route parameters can be bound to primitive types on the dto using route templates like you'd typically do.

**request dto**

```csharp
public class MyRequest
{
    public string MyString { get; set; }
    public bool MyBool { get; set; }
    public int MyInt { get; set; }
    public long MyLong { get; set; }
    public double MyDouble { get; set; }
    public decimal MyDecimal { get; set; }
}
```

**endpoint**
```csharp
public class MyEndpoint : Endpoint<MyRequest>
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/api/{MyString}/{MyBool}/{MyInt}/{MyLong}/{MyDouble}/{MyDecimal}");
    }
}
```

if a `GET` request is made to the url `/api/hello world/true/123/12345678/123.45/123.4567` the request dto would have the following property values:

```
MyString  - "hello world"
MyBool    - true
MyInt     - 123
MyLong    - 12345678
MyDouble  - 123.45
MyDecimal - 123.4567
```

# query parameters
in order to bind from query string params, simply use a url that has the same param names as your request dto such as:
```java
/api/hello-world/?Message=hello+from+query+string
```
if your request dto has a property called `Message` it would then have `hello from query string` as it's value.

# complex model binding

complex model binding is only supported from the json body. for example, the following request dto will be automatically populated from the below json request body.

**request dto**
```csharp
public class UpdateAddressRequest
{
    public int UserID { get; set; }
    public Address UserAddress { get; set; }

    public class Address
    {
        public string Street { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
    }
}
```

**json request**
```
{
    "UserID": 111,
    "Address": {
        "Street": "123 road",
        "City": "new york",
        "Country": "usa"
    }
}
```

# json serialization casing
by default the serializer uses **camel casing** for serializing/deserializing. you can change the casing in the app startup by configuring `JsonOptions` like so:
```java
builder.Services.Configure<JsonOptions>(o => 
    o.SerializerOptions.PropertyNamingPolicy = null);
```
the above will set the casing to **pascal casing** for the serializer.

you can set other options for the serializer with the above as well.