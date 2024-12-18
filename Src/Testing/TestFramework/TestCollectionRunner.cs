// using Xunit.Sdk;
// using Xunit.v3;
//
// namespace FastEndpoints.Testing;
//
// class TestCollectionRunner : XunitTestCollectionRunner
// {
//     readonly IMessageSink _diagnosticMessageSink = default!; //diagnosticMessageSink;
//
//     protected override Task<RunSummary> RunTestClassAsync(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases)
//     {
//         foreach (var fixtureType in @class.Type.GetTypeInfo().ImplementedInterfaces
//                                           .Where(i => i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == typeof(IAssemblyFixture<>))
//                                           .Select(i => i.GetTypeInfo().GenericTypeArguments.Single())
//                                           .Where(i => !assemblyFixtureMappings.ContainsKey(i)))
//         {
//             lock (assemblyFixtureMappings)
//             {
//                 if (!assemblyFixtureMappings.ContainsKey(fixtureType))
//                     Aggregator.Run(() => assemblyFixtureMappings.Add(fixtureType, CreateAssemblyFixtureInstance(fixtureType)));
//             }
//         }
//
//         var combinedFixtures = new Dictionary<Type, object>(assemblyFixtureMappings);
//         foreach (var kvp in CollectionFixtureMappings)
//             combinedFixtures[kvp.Key] = kvp.Value;
//
//         return new XunitTestClassRunner(
//             testClass,
//             @class,
//             testCases,
//             _diagnosticMessageSink,
//             MessageBus,
//             TestCaseOrderer,
//             new(Aggregator),
//             CancellationTokenSource,
//             combinedFixtures).RunAsync();
//     }
//
//     object CreateAssemblyFixtureInstance(Type fixtureType)
//     {
//         var constructors = fixtureType.GetConstructors();
//
//         if (constructors.Length > 1)
//             throw new($"The type ${fixtureType.FullName} can only contain one constructor.");
//
//         return constructors[0].GetParameters().Length == 0
//                    ? Activator.CreateInstance(fixtureType)!
//                    : Activator.CreateInstance(fixtureType, _diagnosticMessageSink)!;
//     }
// }