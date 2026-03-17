---

## ⚠️ Sponsorship Level Critically Low ⚠️

Due to low financial backing by the community, FastEndpoints will soon be going into "Bugfix Only" mode until the situation improves. Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Dual mode testing support for 'AppFixture'</summary>

You can now use the same app fixture (without any conditional code in your tests) to run WAF based tests during regular development, and run smoke tests against a native aot build during a CI/CD pipeline run by simply doing `dotnet test MyTestProject.csproj -p:NativeAotTestMode=true` in the pipeline. This way you are able to have a faster feedback loop during development and also verify that everything works the same once the app is built with native aot by running the same set of tests against the aot build without any special handling in your code. See the documentation [here](https://fast-endpoints.com/docs/native-aot#testing-native-aot-builds).

</details>

<details><summary>Fluent generics support for serializer context generator</summary>

The STJ serializer context generator now supports endpoints defined with [fluent generics](https://fast-endpoints.com/docs/get-started#fluent-generics).

</details>

<details><summary>Referenced project + Nuget package support for the serializer context generator</summary>

The serializer context will now generate `JsonSerializable` attributes for request and response DTOs from referenced source projects as well as Nuget packages. Previously the generator was only capable of generating attributes for DTOs from the current project directory.

</details>

<details><summary>Ability to configure a pre-determined list of "known subscribers" for remote event queues</summary>

Remote event subscribers can now supply an explicit `subscriberID` instead of relying on the auto generated client identity, and event hubs can be configured with a known list of subscriber IDs to begin queuing events for them from app startup onward. Known subscriber pre-seeding does not affect round-robin mode, which still delivers only to currently connected subscribers.

</details>

## Fixes 🪲

<details><summary>Stack overflow issue with .NET 8 and 9</summary>

A stack overflow exception was being thrown in .NET 8/9 due to cyclical calls in TypeInfoResolver, which .NET 10 has solved. We've added a workaround to prevent this from happening.

</details>

<details><summary>Serializer context generator was skipping collection DTO types</summary>

The serializer context generator tool was not creating `JsonSerializable` attributes for request and response DTO types if they were collection types such as `List<Request>`, `IEnumerable<Response>`, etc.

</details>

<details><summary>Job queue storage provider query translation issue</summary>

The job queue search predicate has been improved to allow EF Core (and potentially other ORMs) to translate the expression correctly when the storage provider is configured in non-distributed mode. Previously, an unmapped `DequeueAfter` property was included in the expression causing EF Core translation errors.

</details>

<details><summary>Regression in OData library</summary>

Due to recent changes in FastEndpoints v8, the OData library stopped producing results. This has now been fixed, and comprehensive tests added to prevent a reoccurrence.

</details>

## Improvements 🚀

<details><summary>Prune stale remote event hub subscribers after 24 hours</summary>

Remote event hubs now stop creating event records for disconnected subscribers that have not been seen for 24 hours. This keeps temporarily disconnected subscribers eligible to receive queued events when they reconnect, while preventing dead subscribers from accumulating new records indefinitely.

</details>

<details><summary>Remote event queue persistence retry handling</summary>

The publisher event hub and remote event subscriber now retry only the actual event storage operations and perform their semaphore wake-up signaling separately. This avoids re-persisting already stored event records if the post-store notification step fails.

</details>

<details><summary>Optimize serializer context generator</summary>

The serializer context generator has been improved to use less resources and do the generation in a more efficient and faster manner.

</details>

<details><summary>Custom value parser registration internals</summary>

v8 matches custom value parsers by the underlying type (due to native aot intricacies) and if a user would register the parser with the nullable type, it would not match. This has been solved by always registering the underlying type even if the user supplies a nullable type.

</details>

<details><summary>Job queue executor refilling and shutdown behavior</summary>

Job queue executors now refill newly freed concurrency slots immediately instead of waiting for the whole fetched batch to finish. During shutdown, the executor also drains already running jobs before exiting, and distributed storage providers only claim as many jobs as there are currently available execution slots.

</details>

<details><summary>Remote event queue executor refilling with stable event record IDs</summary>

Remote event storage records now carry a library generated `TrackingID`, which allows event subscribers to refill newly freed execution slots immediately without re-scheduling the same in-flight durable record. This improves concurrency utilization for persistent remote event queues, especially when a slow handler would otherwise hold up the next batch.

</details>

## Breaking Changes ⚠️

<details><summary>Remote event storage records now require a TrackingID</summary>

The `IEventStorageRecord` contract now includes a `Guid TrackingID` property. If you maintain custom persistent event storage record types for remote event hubs or subscribers, you must add this property to your record models and map/persist it in storage. The library will automatically populate the value when creating new event records. This was a necessary addition in order to maximize concurrency utilization.

</details>