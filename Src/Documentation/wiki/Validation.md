# request dto validation
request validation is done using [FluentValidation](https://fluentvalidation.net/) rules. please refer to the FluentValidation website if you haven't used it before. just make sure to import it first *(or add a `global using FluentValidation;` in program.cs)* before writing any validators. you don't need to install the FluentValidation package as it's automatically brought in by FastEndpoints.

simply write your validators by inheriting the `Validator<TRequest>` base class like below. you don't need to register your validators with the DI container. that is automatically taken care of by FastEndpoints. 

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
the format of the error response is customizable as described in the [configuration settings](Configuration-Settings.md) page.

## disable automatic failure response
in cases where you need more control of the validations, you can turn off the default behavior by calling the `DontThrowIfValidationFails()` method in the endpoint configuration like so:
```csharp
public class CreateUserEndpoint : Endpoint<CreateUserRequest>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/api/user/create");
        DontThrowIfValidationFails();
    }
}
```
doing so will not send an automatic error response to the client and your handler will be executed. you can check the validation status by looking at the `ValidationFailures` property of the handler like so:

```csharp
public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
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
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/api/user/create");
    }

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        bool userExists = await userRepo.UserAlreadyExists(req.EmailAddress);
        if (userExists)
            AddError(r => r.EmailAddress, "this email is already in use!");

        var maxAge = await userRepo.GetMaxAllowedAge();
        if (req.Age >= maxAge)
            AddError(r => r.Age, "you are not eligible for insurance!");

        ThrowIfAnyErrors(); //because if there are errors, we shouldn't go beyond this point

        var userID = await userRepo.CreateNew(req);
        if (userID is null)
            ThrowError("creating a user did not go so well!"); //error response thrown here

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

> [!NOTE]
> by default, validators are used as singletons for [performance reasons](Benchmarks.md). i.e. there will only ever be one instance of a validator. all requests to an endpoint will use that single instance for validating the incoming request. so, you should not maintain state in your validators. if you must have state in the validator or would like to do constructor dependency injection by paying the performance penalty, you may do so as shown [here](Dependency-Injection.md#dependency-resolving-for-validators).