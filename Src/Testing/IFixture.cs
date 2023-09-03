using Bogus;

namespace FastEndpoints.Testing;

/// <summary>
/// marker interface for <see cref="TestFixture{TProgram}"/> implementations
/// </summary>
public interface IFixture
{
    /// <summary>
    /// bogus data generator
    /// </summary>
    Faker Fake { get; }
}
