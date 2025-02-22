---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

<details><summary>Ability to bypass endpoint caching for integration tests</summary>

You can now easily test endpoints that have caching enabled, by using a client configured to automatically bypass caching like so:

```cs
var antiCacheClient = App.CreateClient(new() { BypassCaching = true });
```

</details>

## Improvements 🚀

<details><summary>Ability to optionally save command type on job storage record</summary>

A new optional/addon interface `IHasCommandType` has been introduce if you need to persist the full type name of the command that is associated with the a job storage record.
Simply implement the new interface on your job storage record and the system will automatically populate the property value before being persisted.

</details>

<details><summary>RPC Event Hub startup sequence</summary>

The rpc event hub was using a thread sleep pattern during startup to restore subscriber IDs via the storage provider, resulting in a sequential initialization.
It has been refactored to use an IHostedService together with retry logic for a proper async and parallel initialization, resulting in decreased startup time.

</details>

<details><summary>Workaround for NSwag quirk with byte array responses</summary>

NSwag has a quirk that it will render an incorrect schema if the user does something like the following:

```cs
b => b.Produces<byte[]>(200, "image/png");
```

In order to get the correct schema generated, we've had to do the following:

```cs
b => b.Produces<IFormFile>(200, "image/png");
```

You now have the ability to do either of the above and it will now generate the correct schema.

</details>

## Fixes 🪲

<details><summary>Job result is null when job execution failure occurs</summary>

The result property of job records that was passed into the `OnHandlerExecutionFailureAsync()` method of the storage provider was `null` due to an oversight, which has been corrected.

</details>

<details><summary>Parallel 'VersionSet' creation issue</summary>

If `VersionSet`s are created by multiple SUTs at the same time when doing integration testing, a non-concurrent dictionary modification exception was being thrown.
The internal dictionary used to keep track of the version sets has been changed to a concurrent dictionary which solves the issue.

</details>

## Breaking Changes ⚠️