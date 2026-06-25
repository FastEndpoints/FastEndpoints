namespace FastEndpoints;

/// <summary>
/// implemented by command rules that need explicit evaluation order.
/// </summary>
public interface IOrderedCommandRule
{
    /// <summary>
    /// rule ordering value. lower values are evaluated first.
    /// </summary>
    int Order { get; }
}
