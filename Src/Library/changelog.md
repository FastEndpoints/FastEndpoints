---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## Breaking Changes ⚠️

> Support for .NET 6 & 7 has been dropped as those SDKs are no longer supported by Microsoft. In order to use this release of FastEndpoints, you need to be on at least .NET 8.0.4

## New 🎉

<details><summary>Support for .NET 10 preview</summary>

You can start targeting `net10.0` SDK in your FE projects now. Currently preview versions of the dependencies are used.

</details>

<details><summary>Generic Pre/Post Processor global registration</summary>

Open generic pre/post processors can now be registered globally using the endpoint configurator func like so:

```cs
app.UseFastEndpoints(c => c.Endpoints.Configurator = ep => ep.PreProcessors(Order.Before, typeof(MyPreProcessor<>)))
```

```cs
sealed class MyPreProcessor<TRequest> : IPreProcessor<TRequest>
{
    public Task PreProcessAsync(IPreProcessorContext<TRequest> ctx, CancellationToken c)
    {
        ...
    }
}
```

</details>

<details><summary>Middleware pipeline for Command Bus</summary>

By popular demand from people moving away from MediatR, a middleware pipeline similar to MediatRs [pipeline behaviors](https://github.com/jbogard/MediatR/wiki/Behaviors) has been added to FE's built-in command bus. You just need to write your pipeline/middleware
pieces by implementing the interface `ICommandMiddleware<TCommand,TResult>` and register those pieces to form a middleware pipeline as described in the [documentation]().

</details>

<details><summary>Support 'CONNECT' and 'TRACE' verbs</summary>

The `FastEndpoints.Http` enum and the endpoint base classes now have support for the HTTP `CONNECT` & `TRACE` verbs.

</details>

<details><summary>Verify event publishes when integration testing</summary>

When integration testing using the `AppFixture`, it is now possible to setup a `Test Event Receiver` as a collector of all the events that gets published from your code.
These received events can be used as verification that your code did actually publish the desired event. A full example of this new capability can be seen [here](https://gist.github.com/dj-nitehawk/ae85c63fefb1e8163fdd37ca6dcb7bfd).

</details>

<details><summary>Dynamic updating of JWT signing keys at runtime</summary>

Updating the signing keys used by JWT middleware at runtime is now made simple without having to restart the application.
[See here](https://gist.github.com/dj-nitehawk/65b78b08075fae3070e9d30e2a59f4c1) for a full example of how it is done.

</details>

## Improvements 🚀

<details><summary>Automatic addition of 'ProducesResponseTypeMetadata'</summary>

The library [automatically adds response type metadata](https://fast-endpoints.com/docs/swagger-support#describe-endpoints) for certain response types.
Sometimes, the automatically added responses need to be cleared by the user when it's not appropriate.
From now on, the automatic additions will only happen if the user hasn't already added it.

**Before:**

```cs
Description(x => x.ClearDefaultProduces(200) //had to clear the auto added 200
                  .Produces(201))
```

**Now:**

```cs
Description(x => x.Produces(201)) //nothing to clear as nothing was added due to 201 being present
```

</details>

<details><summary>Use source generated regex</summary>

Source generated regex is now used whereever possible. Source generated regex was not used before due to having to support older SDK versions.

</details>

<details><summary>Allow overriding the 'Verbs()' method of `Endpoint<>` class</summary>

The `Verbs()` method was sealed until now because it was doing some essential setup which was required for adding the default request/response swagger descriptions.
This logic has been moved out of the `Verbs()` method making it overrideable if needed.

</details>

<details><summary>Prevent configuration methods being called after startup</summary>

A meaningful exception will now be thrown if the user tries to call endpoint configuration methods such as `Verbs()/Routes()/etc.` outside of the endpoint `Configure()` method.

</details>

## Fixes 🪲

<details><summary>Contention issue in reflection source generator</summary>

The reflection source generator was using some static state which was causing issues in certain usage scenarios, which has now been fixed.

</details>

<details><summary>Type discriminator missing from polymorphic responses</summary>

The type discriminator was not being serialized by STJ when the response type was a base type, due to an oversight in the default response serialized func.

</details>

<details><summary>Source generated reflection for obsolete members</summary>

When source generation happens for obsolete members of classes, the generated file triggered a compiler warning, which has now been correctly handled.

</details>