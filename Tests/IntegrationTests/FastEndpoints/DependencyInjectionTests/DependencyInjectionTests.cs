namespace DependencyInjection;

public class DiTests(Fixture f, ITestOutputHelper o) : TestClass<Fixture>(f, o) { }