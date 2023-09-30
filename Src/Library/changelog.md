
---

## ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

<!-- <details><summary>title text</summary></details> -->

# New 🎉

<details><summary>Source generated DI registrations</summary>

Please see the [documentation](https://fast-endpoints.com/docs/dependency-injection#source-generated-service-registrations) for details of this feature.

</details>

<details><summary>Source generated access control lists</summary>

Todo: update doc page and link from here.

</details>

<details><summary>Ability to show deprecated endpoint versions in Swagger</summary>

By default, deprecated endpoint versions are not included in swagger docs. Now you have the choice of including/displaying them in the doc so they'll be displayed greyed out like this:

![image](https://user-images.githubusercontent.com/7043768/267551669-25eb2c56-fb55-4dfb-b3a2-847e1c55b2c7.png)

Please see [this usage example](https://gist.github.com/dj-nitehawk/c32e7f887389460c661b955d233b650d) on how to enable it.

</details>

<details><summary>Round-Robin Event delivery with gRPC</summary>

It is now possible to deliver an event to only just one of the connected remote subscribers in a round-robin fashion. Comes in handy when you need to distribute the workload among a pool of subscribers/workers and ensure that a single event is only processed by a single remote subscriber. See the [documentation](https://fast-endpoints.com/docs/remote-procedure-calls#round-robin-mode) for more info.

</details>

<details><summary>Allow customizing serialization/deserialization of Event/Command objects in Job/Event Queue storage</summary>

Todo: update doc page and link from here.
Ref: https://github.com/FastEndpoints/FastEndpoints/issues/480

</details>

# Improvements 🚀

<details><summary>Ability to get rid of null-forgiving operator '!' from test code</summary>

The `TestResult<TResponse>.Result` property is no longer a nullable property. This change enables us to get rid of the null-forgiving operator `!` from our integration test code.
Existing test code wouldn't have to change. You just don't need to use the `!` to hide the compiler warnings anymore. If/when the value of the property is actually `null`, the tests will 
just fail with a NRE, which is fine in the context of test code.

</details>

<details><summary>Optimize source generator performance</summary>

The type discovery generator is now highly efficient and only generates the source when any of the target types changes or new ones are added.

</details>

<details><summary>Optimize startup routine</summary>

Authorization policy building is moved to the `MapFastEndpoints` stage avoiding the need to iterate the discovered endpoint collection twice. This also avoids any potential race conditions due to different middleware pipeline config/ordering edge cases.

</details>

# Fixes 🪲

<details><summary>Startup issue due to 'IAuthorizationService' injection</summary>

v5.16 had introduced a bug of not being able to inject `IAuthorizationService` into endpoint classes, which has now been fixed.

</details>

<details><summary>Startup type discovery issue with exclusion list</summary>

Since you can override the exclusion list by doing:

```cs
.AddFastEndpoints(o.Assemblies = new[] { typeof(SomeClass).Assembly });
```

This was not working if the assembly name didn't have a dot (.) in the namespace.  

</details>

<details><summary>Empty swagger parameter example generation</summary>

The swagger operation processor was creating an example field with an empty string when there's no example provided by the user like the following:

```json
"parameters": [
    {
    ...
    "example": ""
    }
```

</details>

<details><summary>Swagger parameter example generation with default values</summary>

The swagger operation processor was creating an example field with the default value when there's no example or `DefaultValue` provided by the user like the following:

```json
"parameters": [
    {
    ...
    "example": 0
    }
```

</details>

## Minor Breaking Changes ⚠️

<details><summary>'AddFastEndpoints()' no longer calls 'AddAuthorization()'</summary>

Due to the startup optimization mentioned above, you will now be greeted with the following exception if your app is using authorization middleware:

```yaml
Unhandled exception. System.InvalidOperationException: Unable to find the required services. Please add all the required services by calling 'IServiceCollection.AddAuthorization' in the application startup code.
```

It's because `AddFastEndpoints()` used to do the `AddAuthorization()` call internally which it no longer does. Simply add this call yourself to the middleware pipeline.

</details>