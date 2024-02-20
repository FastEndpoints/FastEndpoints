using Bogus;

namespace FastEndpoints.Testing;

/// <summary>
/// marker interface for <see cref="AppFixture{TProgram}" /> implementations
/// </summary>
public interface IFixture
{
    /// <summary>
    /// bogus data generator
    /// </summary>
    Faker Fake { get; }
}