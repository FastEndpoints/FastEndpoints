# request dto validation
request validation is done using [FluentValidation](https://fluentvalidation.net/) rules. `fastendpoints` uses a customized version of the fluentvalidations library but the api for the end user is the same. please refer to the fluentvalidations website if you haven't used it before. the built-in validation is provided by the `FastEndpoints.Validation` package. make sure to import it first before writing any validators.

**request dto**
```csharp
public class CreateUserRequest
{
    public string FullName { get; set; }
    public int Age { get; set; }
}
```

**validator**
```csharp
using FastEndpoints.Validation;

public class MyValidator : Validator<CreateUserRequest>
{
    public MyValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("your name is required!")
            .MinimumLength(5).WithMessage("your name is too short!");

        RuleFor(x => x.Age)
            .NotEmpty().WithMessage("we need your age!")
            .GreaterThan(18).WithMessage("you are not legal yet!");
    }
}
```
if a request is received that doesn't meet the above model validation criteria, a `400 bad request` response will be sent to the client automatically with the following json body describing the error details:
```
{
    "StatusCode": 400,
    "Message": "One or more errors occured!",
    "Errors": {
        "FullName": [
            "your name is required!",
            "your name is too short!"
        ],
        "Age": [
            "we need your age!",
            "you are not legal yet!"
        ]
    }
}
```

## disable automatic failure response
in cases where you need more control of the validations, you can turn off the default behavior by calling the `DontThrowIfValidationFails()` method in the endpoint ctor like so:
```csharp
public class CreateUserEndpoint : Endpoint<CreateUserRequest>
{
    public CreateUserEndpoint()
    {
        Verbs(Http.POST);
        Routes("/api/user/create");
        DontThrowIfValidationFails();
    }
}
```
doing so will not send an automatic error response to the client and your handler will be executed. you can check the validation status in your handler by looking at the `ValidationFailures` property of the handler like so:

```csharp
protected override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
{
    if (ValidationFailed)
    {
        foreach (ValidationFailure failure in ValidationFailures)
        {
            var propertyName = failure.PropertyName;
            var errorMessage = failure.ErrorMessage;
        }
    }
    await SendOkAsync();
}
```

# application logic validation
in cases where there are app/business logic validation failures during the processing of a request in the handler, you can send an error response to the client like so:
```csharp
public class CreateUserEndpoint : Endpoint<CreateUserRequest, CreateUserResponse>
{
    public CreateUserEndpoint()
    {
        Verbs(Http.POST);
        Routes("/api/user/create");
    }

    protected override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        bool userExists = await userRepo.UserAlreadyExists(req.UserID);
        if (userExists)
            AddError(r => r.UserID, "this user id already exists!");

        var maxAge = await userRepo.GetMaxAllowedAge();
        if (req.Age >= maxAge)
            AddError(r => r.Age, "you are not eligible for insurance!");

        ThrowIfAnyErrors();

        var userID = await userRepo.CreateNew(req);
        if (userID is null)
            ThrowError("creating a user did not go so well!");

        await SendAsync(new CreateUserResponse
        {
            UserID = userID,
            FullName = req.FullName
        });
    }
}
```

**AddError()** - this method adds a validation failure to the `ValidationFailures` property of the handler.

**ThrowIfAnyErrors()** - this method will cause the handler execution to be halted when called and an error response will be sent to the client **if** there are any validation failures in the `ValidationFailures` list. if there's none, execution will proceed to the next line.

**ThrowError()** - this method will abort execution immediately and send an error response to the client.