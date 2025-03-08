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

<details><summary>Roslyn compiler versioning issue</summary>

The last version of the compiler shipped with .NET SDK v6/7/8 is `4.12.0`. We started using `4.13.0` which is only shipped in .NET 9. This was causing problems for people still on older SDK versions. 
This has been solved by doing conditional package referencing.

</details>

<details><summary>Contention issue in reflection source generator</summary>

The reflection source generator was using some static state which was causing issues in certain usage scenarios, which has now been fixed.

</details>

## Breaking Changes ⚠️