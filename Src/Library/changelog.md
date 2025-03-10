---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## Breaking Changes ⚠️

> Support for .NET 6 & 7 has been dropped as those SDKs are no longer supported by Microsoft. In order to use this release of FastEndpoints, you need to be on at least .NET 8.

## New 🎉

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

## Improvements 🚀

## Fixes 🪲

<details><summary>Contention issue in reflection source generator</summary>

The reflection source generator was using some static state which was causing issues in certain usage scenarios, which has now been fixed.

</details>