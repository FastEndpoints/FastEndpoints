namespace FastEndpoints;

/// <summary>
/// marker/constraint for endpoints that have a mapper generic argument
/// </summary>
/// <typeparam name="TMapper"></typeparam>
public interface IHasMapper<TMapper> where TMapper : notnull, IMapper
{
    TMapper Map { get; }
}
