---

## ⚠️ Goal Sponsorship Level Not Yet Met ⚠️

Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>'FastEndpoints.CommandRules' package for rule-based command dispatch</summary>

A new `FastEndpoints.CommandRules` package is now available for turning arbitrary input into one or more commands using small, ordered rules.

It's useful when an application event, webhook payload, request DTO, or domain object needs to fan out into different command-bus actions without putting branching logic in endpoints or handlers. Rules evaluate the input, build a command plan, and the dispatcher executes the selected commands immediately or queues them as jobs.

```csharp
// map input model to a rule
bld.Services.AddCommandRules(o => o.Register<OrderPlaced, OrderPlacedRule>());

// define the rule to specify which commands should ececute
sealed class OrderPlacedRule : CommandRule<OrderPlaced>
{
    public override bool CanHandle(OrderPlaced input)
        => input.SendReceipt;

    public override IEnumerable<PlannedCommand> Build(OrderPlaced input)
    {
        yield return PlannedCommand.Create(new ReserveStock(input.OrderId));

        yield return new PlannedCommand(new SendReceipt(input.OrderId))
        {
            Mode = CommandDispatchMode.QueueAsJob
        };
    }
}

// inject ICommandDispatcher<OrderPlaced> where the event/input is handled
await dispatcher.DispatchAsync(orderPlaced, ct);
```

</details>

<details><summary>Cached 'AppFixture' WAF disposal hook</summary>

`AppFixture<TProgram>` can now run final teardown once when its cached `WebApplicationFactory<TProgram>` is disposed at the end of a test assembly.

Override `OnCachedWafDisposedAsync()` in your fixture to clean up resources tied to the shared WAF instance. The hook runs after all fixture users are done, and requires cached WAF mode with `[assembly: EnableAdvancedTesting]`.

```csharp
sealed class App : AppFixture<Program>
{
    protected override async ValueTask OnCachedWafDisposedAsync()
    {
        await ResetExternalResourceAsync();
    }
}
```

</details>

## Fixes 🪲

<details><summary>Nullable OpenAPI schemas with composition now emit valid null branches</summary>

`FastEndpoints.OpenApi` now emits valid OpenAPI 3.1 schemas for nullable arrays and nullable object references when composition keywords such as `oneOf` are involved.

Nullable arrays now inline the referenced array schema instead of combining `type: ["null", "array"]` with a non-null `oneOf`, and nullable object references now preserve null validity with an explicit null branch.

</details>

<details><summary>GET/HEAD root collection request bodies are optional in OpenAPI</summary>

`FastEndpoints.OpenApi` and `FastEndpoints.Swagger` now mark root collection request bodies (`List<T>` and `T[]`) as optional for `GET` and `HEAD` endpoints while preserving the generated array schema.

This matches runtime binding behavior where omitted `GET`/`HEAD` request bodies bind as empty collections, while non-collection request DTOs and other HTTP methods remain unchanged.

</details>

## Improvements 🚀

<details><summary>Relaxed agent name validation</summary>

A2A skill ids and MCP tool names now allow dots and forward slashes, so path/version-style identifiers such as `users/read.v1` can be published without renaming.

Some external MCP adapters may still apply OpenAI-style function-name validation and reject dots or slashes.

</details>

<details><summary>Refresh token service support for union-type returning endpoints</summary>

A new `CreateTokenWith<TService, TTokenResponse>()` overload lets endpoints that return a union-type result (e.g. `Results<Ok<TokenResponse>, UnauthorizedHttpResult>`) create access/refresh token pairs, by decoupling the token response type from the endpoint's response type.

</details>

[//]: # (## Breaking Changes ⚠️)