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

<details><summary>Support 'CONNECT' and 'TRACE' verbs</summary>

The `FastEndpoints.Http` enum and the endpoint base classes now have support for the HTTP `CONNECT` & `TRACE` verbs.

</details>

<details><summary>Verify event publishes when integration testing</summary>

When integration testing using the `AppFixture`, it is now possible to setup a `Test Event Receiver` as a collector of all the events that gets published from your code.
These received events can be used as verification that your code did actually publish the desired event. A full example of this new capability can be seen [here](https://gist.github.com/dj-nitehawk/ae85c63fefb1e8163fdd37ca6dcb7bfd).

</details>

## Improvements 🚀

<details><summary>Use source generated regex</summary>

Source generated regex is now used whereever possible. Source generated regex was not used before due to having to support older SDK versions.

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