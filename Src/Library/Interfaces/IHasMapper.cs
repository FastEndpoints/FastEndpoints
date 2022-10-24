namespace FastEndpoints;

/// <summary>
/// marker interface for endpoints that has a mapper
/// </summary>
public interface IHasMapper { }

/// <summary>
/// marker/constraint for endpoints that have a mapper generic argument
/// </summary>
/// <typeparam name="TMapper">the type of the mapper</typeparam>
public interface IHasMapper<TMapper> : IHasMapper where TMapper : notnull, IMapper
{
    /// <summary>
    /// the mapper property
    /// </summary>
    TMapper Map { get; set; }
}
