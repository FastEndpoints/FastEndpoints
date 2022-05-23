# request dto binding
the endpoint handlers are supplied with fully populated request dtos. the dto property values are automatically bound from the incoming request, from the following sources in the exact order:

1. json body
2. form data
3. route parameters
4. query parameters
5. user claims (if property has *[FromClaim]* attribute)
6. http headers (if property has *[FromHeader]* attribute)
7. permissions (if `boolean` property has *[HasPermission]* attribute)

consider the following request dto and http request:

**dto**
```csharp
public class GetUserRequest
{
    public string UserID { get; set; }
}
```

**http request**
```
route : /api/user/{UserID}
url   : /api/user/54321
json  : { "UserID": "12345" }
```

when the handler receives the request dto, the value of `UserID` will be `54321` because route parameters have higher priority than json body.

likewise, if you decorate the `UserID` property with `[FromClaim]` attribute like so:
```csharp
public class GetUserRequest
{
    [FromClaim]
    public string UserID { get; set; }
}
```
the value of `UserID` will be whatever claim value the user has for the claim type `UserID` in their claims. by default if the user does not have a claim type called `UserID`, then a validation error will be sent automatically to the client. you can make the claim optional by using the following overload of the attribute:
```java
[FromClaim(IsRequired = false)]
```
doing so will allow the endpoint handler to execute even if the current user doesn't have the specified claim and model binding will take the value from the highest priority source of the other binding sources mentioned above (if a matching field/route param is present). an example can be seen [here](https://github.com/dj-nitehawk/FastEndpoints/blob/main/Web/%5BFeatures%5D/Customers/Update/Endpoint.cs).

it is also possible to model bind automatically from http headers like so:
```csharp
public class GetUserRequest
{
    [FromHeader]
    public string TenantID { get; set; }
}
```
`FromHeader` attribute will also by default send an error response if a http header (with the same name as the property being bound to) is not present in the incoming request. you can make the header optional and turn off the default behavior by doing `[FromHeader(IsRequired = false)]` just like with the FromClaim attribute. Both attributes have the same overloads and behaves similarly.

the `HasPermission` attribute can be used on `boolean` properties to check if the current user principal has a particular permission like so:
```csharp
public class UpdateArticleRequest
{
    [HasPermission("Article_Update")]
    public bool AllowedToUpdate { get; set; }
}
```
the property value will be set to `true` if the current principal has the `Article_Update` permission. as with the above attributes, an automatic validation error will be sent in case the principal does not have the specified permission. you can disable the automatic validation error by doing the following:
```java
[HasPermission("Article_Update", IsRequired = false)]
```

# route parameters
route parameters can be bound to properties on the dto using route templates like you'd typically do.

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
        Get("/api/{MyString}/{MyBool}/{MyInt}/{MyLong}/{MyDouble}/{MyDecimal}");
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

complex models are bound automatically from the incoming http request body that has a `content-type` header value of `application/json` if the body has valid json such as the following:

**json request body**
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

which would be bound to a complex type such as this:

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

json arrays in the request body can be bound to models by specifying the request dto type of the endpoint as `List<T>` like so:
```csharp
public class MyEndpoint : Endpoint<List<Address>>
{
  ...
}
```

# mismatched property names
you can bind to dto properties when the incoming parameter name doesn't match with the name of the property being bound to, depending on the type of the parameter source like so:

### json body
```csharp
[JsonPropertyName("address")]
public Address UserAddress { get; set; }
```

### form fields, route params & query params:
```csharp
[BindFrom("customerId")]
public string CustomerID { get; set; }
```

### headers & claims:
```csharp
[FromHeader("tenant-id")]
public string TenantID { get; set; }

[FromClaim("user-id")]
public string UserID { get; set; }
```

# supported dto property types
#### from json body:
any complex type can be bound as long as the `System.Text.Json` serializer can handle it. if it's not supported out of the box, please see the [STJ documentation](https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-converters-how-to?pivots=dotnet-6-0) on how to implement custom converters for your types.

you can register your custom converters in startup like this:
```csharp
app.UseFastEndpoints(c =>
{
    c.SerializerOptions = o =>
    {
        o.SerializerOptions.Converters.Add(new CustomConverter());
    };
});
```

#### from form fields/route/query/claims/headers:
simple strings (scalar values) can be bound automatically to any of the primitive/clr **non-collection types** such as the following that has a static `TryParse()` method:
- bool
- double
- decimal
- DateTime
- Enum
- Guid
- int
- long
- string
- TimeSpan
- Uri
- Version

in order to support binding your custom types from route/query/claims/header/form fields, simply add a static `TryParse()` method to your type like the example below:
```csharp
public class Point
{
    public double X { get; set; }
    public double Y { get; set; }

    public static bool TryParse(string? input, out Point? output) //adhere to this signature
    {
        output = null;

        if (string.IsNullOrEmpty(input))
        {
            return false;
        }

        var parts = input.Split(',');

        if (!double.TryParse(parts[0], out var x) ||
            !double.TryParse(parts[1], out var y))
        {
            return false;
        }

        output = new Point
        {
            X = x,
            Y = y
        };

        return true;
    }
}
```

json array strings can be bound to collection type properties such as `IEnumerable<T>` and `T[]` as long as the incoming string is valid json. the incoming json string is deserialized using `System.Text.Json` serializer. consider the following example:

**request url:**
```
/my-endpoint?items=[{id="1"},{id="2"}]&codes=[1,2,3,4,5]
```

**request dto**
```csharp
public class MyRequest
{
    public IEnumerable<Item> Items { get; set; }
    public int[] Codes { get; set; }
}
```

the `Items` property will have 2 objects as it's values and the `Codes` will have 5 integers as it's values when the handler receives the dto.

to reiterate, if you want to automatically bind incoming values to collection type properties from query params, form fields, header values or claim values, those values must be valid json arrays. if the input is invalid json, an exception will be thrown by STJ.

## route/query binding when there's no request dto
if your endpoint doesn't have/need a request dto, you can easily read route & query parameters using the `Route<T>()` and `Query<T>()` methods.
```csharp
public class GetArticle : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/article/{ArticleID}");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        //http://localhost:5000/article/123
        int routeParam = Route<int>("ArticleID");
        
        //http://localhost:5000/article/123?OtherID=8635ffb2-6589-4629-85bc-29f2cce5a12d
        Guid queryParam = Query<Guid>("OtherID");
    }
}
```
**note:** `Route<T>()` & `Query<T>()` methods are also only able to handle types that have a static `TryParse()` method and/or valid json arrays as mentioned above. if there's no static `TryParse()` method or if parsing fails, an automatic validation failure response is sent to the client. this behavior can be turned off with the following overload:
```csharp
Route<Point>("ArticleID", isRequired: false);
Query<Guid>("OtherID", isRequired: false);
```

## binding to raw request content
if you need to access the raw request content as a string, you can achieve that by implementing the interface `IPlainTextRequest` on your dto like so:
```csharp
public class Request : IPlainTextRequest
{
    public string Content { get; set; }
}
```
when your dto implements `IPlainTextRequest`, json model binding won't occur. instead, the `Content` property is populated with the content of the request body.
other properties can also be added to your dto in case you need to access some other values like route/query/form field/header/claim values.

# json serialization casing
by default the serializer uses **camel casing** for serializing/deserializing. you can change the casing as shown in the [configuration settings](Configuration-Settings.md#specify-json-serializer-options) section.

# json source generator support
the `System.Text.Json` source generator support can be easily enabled with a simple 2 step process:

**step 1:** create a serializer context
```csharp
[JsonSerializable(typeof(RequestModel))]
[JsonSerializable(typeof(ResponseModel))]
public partial class UpdateAddressCtx : JsonSerializerContext { }
```

**step 2:** specify the serializer context for the endpoint
```csharp
public class UpdateAddress : Endpoint<RequestModel, ResponseModel>
{
    public override void Configure()
    {
        Post("user/address");
        SerializerContext(UpdateAddressCtx.Default);
    }
}
```