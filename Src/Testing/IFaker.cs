using Bogus;

namespace FastEndpoints.Testing;

/// <summary>
/// provides a bogus fake data generator
/// </summary>
public interface IFaker
{
    /// <summary>
    /// bogus data generator
    /// </summary>
    Faker Fake { get; }
}