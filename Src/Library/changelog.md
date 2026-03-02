---

## ⚠️ Sponsorship Level Critically Low ⚠️

Due to low financial backing by the community, FastEndpoints will soon be going into "Bugfix Only" mode until the situation improves. Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## New 🎉

<details><summary>Dual mode testing support for 'AppFixture'</summary>

You can now use the same app fixture (without any conditional code in your tests) to run WAF based tests during regular development, and run smoke tests against a native aot build during a CI/CD pipeline run by simply doing `dotnet test MyTestProject.csproj -p:NativeAotTestMode=true` in the pipeline. This way you are able to have a faster feedback loop during development and also verify that everything works the same once the app is build with native aot by running the same set of tests against the aot build without any special handling in your code.

TODO: update template project and add info to the aot doc page

</details>

## Fixes 🪲

<details><summary>Regression in OData library</summary>

Due to recent changes in FastEndpoints v8, the OData library stopped producing results. This has now been fixed and the OData base endpoint class has been made leaner by removing the need for an intermediate endpoint filter.

</details>

<details><summary>Serializer context generator was skipping collection DTO types</summary>

The serializer context generator tool was not creating `JsonSerializable` attributes for request and response DTO types if they were collection types such a `List<Request>`, `IEnumerable<Response>`, etc.

</details>

## Improvements 🚀

<details><summary>Fluent generics support for serializer context generator</summary>

The STJ serializer context generator now supports endpoints defined with [fluent generics](https://fast-endpoints.com/docs/get-started#fluent-generics).

</details>

<details><summary>Optimize serializer context generator</summary>

The serializer context generator has been improved to use less resources and do the generation in a more efficient and faster manner.

</details>

<details><summary>Custom value parser registration internals</summary>

v8 matches custom value parsers by the underlying type (due to native aot intricacies) and if a user would register the parser with the nullable type, it would not match. This has been solved by always registering the underlying type even if the user supplies a nullable type.

</details>

## Minor Breaking Changes ⚠️