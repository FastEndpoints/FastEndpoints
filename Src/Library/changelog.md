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

<details><summary>Support for generating STJ serializer contexts for DTOs in referenced projects</summary>

The serializer context will now generate `JsonSerializable` attributes for request and response DTOs from referenced source projects. Previously the generator was only capable of generating attributes for DTOs from the current project directory.

</details>

## Fixes 🪲

<details><summary>Serializer context generator was skipping collection DTO types</summary>

The serializer context generator tool was not creating `JsonSerializable` attributes for request and response DTO types if they were collection types such as `List<Request>`, `IEnumerable<Response>`, etc.

</details>

<details><summary>Stack overflow issue with .NET 8 and 9</summary>

A stack overflow exception was being thrown in .NET 8/9 due to cyclical calls in TypeInfoResolver, which .NET 10 has solved. We've added a workaround to prevent this from happening.

</details>

<details><summary>Job queue storage provider query translation issue</summary>

The job queue search predicate has been improved to allow EF Core (and potentially other ORMs) to translate the expression correctly when the storage provider is configured in non-distributed mode. Previously, an unmapped `DequeueAfter` property was included in the expression causing EF Core translation errors.

</details>

<details><summary>Regression in OData library</summary>

Due to recent changes in FastEndpoints v8, the OData library stopped producing results. This has now been fixed, and comprehensive tests added to prevent a reoccurrence.

</details>

## Improvements 🚀

<details><summary>Optimize serializer context generator</summary>

The serializer context generator has been improved to use less resources and do the generation in a more efficient and faster manner.

</details>

<details><summary>Custom value parser registration internals</summary>

v8 matches custom value parsers by the underlying type (due to native aot intricacies) and if a user would register the parser with the nullable type, it would not match. This has been solved by always registering the underlying type even if the user supplies a nullable type.

</details>

[//]: # (## Minor Breaking Changes ⚠️)