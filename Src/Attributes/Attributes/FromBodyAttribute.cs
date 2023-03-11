namespace FastEndpoints;

/// <summary>
/// properties decorated with this attribute will have their values auto bound from the incoming request's json body.
/// <para>HINT: no other binding sources will be used for binding that property.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class FromBodyAttribute : Attribute { }
