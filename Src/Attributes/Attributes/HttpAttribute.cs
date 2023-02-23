namespace FastEndpoints;

/// <summary>
/// base http attribute class
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public abstract class HttpAttribute : Attribute
{
    internal Http Verb { get; set; }
    internal string[] Routes { get; set; }

    /// <summary>
    /// constructor
    /// </summary>
    /// <param name="verb">verb</param>
    /// <param name="route">route</param>
    protected HttpAttribute(Http verb, string route)
    {
        Verb = verb;
        Routes = new[] { route };
    }

    /// <summary>
    /// constructor
    /// </summary>
    /// <param name="verb">verb</param>
    /// <param name="routes">routes</param>
    protected HttpAttribute(Http verb, params string[] routes)
    {
        Verb = verb;
        Routes = routes;
    }
}
