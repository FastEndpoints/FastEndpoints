namespace FastEndpoints;

/// <summary>
/// When using the 'FastEndpoints.Generator' package, any concrete class can be decorated with this attribute to source generate extension methods
/// in the form of <c>.RegisterServicesFrom{assembly-name}()</c> which can be used to automatically register services with a single call per assembly.
/// instead of multiple calls per each service you need registered in DI.
/// <para>
/// specify the service type with the <typeparamref name="TService" /> generic attribute argument. the service type would typically be an interface type.
/// </para>
/// </summary>
/// <typeparam name="TService">the type of the service you are registering. typically an interface type.</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RegisterServiceAttribute<TService> : Attribute where TService : class
{
    readonly LifeTime _lifetime;

    /// <summary>
    /// mark a class for registration in DI using the 'FastEndpoints.Generator' package by specifying the service lifetime.
    /// </summary>
    /// <param name="serviceLifetime">the service lifetime to use when registering in DI</param>
    public RegisterServiceAttribute(LifeTime serviceLifetime)
    {
        _lifetime = serviceLifetime;
    }
}

/// <summary>
/// enum for selecting the DI service lifetime
/// </summary>
public enum LifeTime
{
    /// <summary>
    /// scoped service lifetime
    /// </summary>
    Scoped = 0,

    /// <summary>
    /// transient service lifetime
    /// </summary>
    Transient = 1,

    /// <summary>
    /// singleton service lifetime
    /// </summary>
    Singleton = 2
}