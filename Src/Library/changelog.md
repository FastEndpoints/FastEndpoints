---

## ❇️ Help Keep FastEndpoints Free & Open-Source ❇️

Due to the current [unfortunate state of FOSS](https://www.youtube.com/watch?v=H96Va36xbvo), please consider [becoming a sponsor](https://opencollective.com/fast-endpoints) and help us beat the odds to keep the project alive and free for everyone.

---

<!-- <details><summary>title text</summary></details> -->

## New 🎉

## Improvements 🚀

<details><summary>Ability to optionally save command type on job storage record</summary>

A new optional/addon interface `IHasCommandType` has been introduce if you need to persist the full type name of the command that is associated with the a job storage record.
Simply implement the new interface on your job storage record and the system will automatically populate the property value before being persisted.

</details>

<details><summary>RPC Event Hub startup sequence</summary>

The rpc event hub was using a thread sleep pattern during startup to restore subscriber IDs via the storage provider.
It has been refactored to use an `IHostedService` together with retry logic for proper async operation.

</details>

## Fixes 🪲

<details><summary>Job result is null when job execution failure occurs</summary>

The result property of job records that was passed into the `OnHandlerExecutionFailureAsync()` method of the storage provider was `null` due to an oversight, which has been corrected.

</details>

## Breaking Changes ⚠️