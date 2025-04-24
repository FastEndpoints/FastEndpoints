namespace FastEndpoints;

/// <summary>
/// marker/constraint for endpoints that have a mapper generic argument
/// </summary>
/// <typeparam name="TMapper">the type of the mapper</typeparam>
public interface IHasMapper<TMapper> where TMapper : IMapper
{
    /// <summary>
    /// the mapper property
    /// </summary>
    TMapper Map { get; set; }
}