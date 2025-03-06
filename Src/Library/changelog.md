---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

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

## Breaking Changes ⚠️