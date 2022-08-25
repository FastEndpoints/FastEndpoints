namespace FastEndpoints;

/// <summary>
/// request binding options
/// </summary>
public class BindingOptions
{
    /// <summary>
    /// an optional action to be run after the endpoint level request binding has occured.
    /// it is intended as a way to perform common model binding logic that applies to all endpoints/requests.
    /// the action is passed in the following arguments:
    /// <para><see cref="object"/>: the request dto instance</para>
    /// <para><see cref="Type"/>: the type of the request dto</para>
    /// <para><see cref="BinderContext"/>: the request binding context</para>
    /// <para><see cref="CancellationToken"/>: a cancellation token</para>
    /// <para>WARNING: be mindful of the performance cost of using reflection to modify the request dto object</para>
    /// </summary>
    public Action<object, Type, BinderContext, CancellationToken>? Modifier;
}