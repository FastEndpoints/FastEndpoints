namespace FastEndpoints;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public abstract class HttpAttribute : Attribute
{
    internal Http Verb { get; set; }
    internal string[] Routes { get; set; }

    protected HttpAttribute(Http verb, string route)
    {
        Verb = verb;
        Routes = new[] { route };
    }

    protected HttpAttribute(Http verb, params string[] routes)
    {
        Verb = verb;
        Routes = routes;
    }
}
