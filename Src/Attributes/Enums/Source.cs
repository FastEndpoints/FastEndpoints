namespace FastEndpoints;

#pragma warning disable CS1591

/// <summary>
/// enum for choosing which binding sources to disable for a given property using the <see cref="DontBindAttribute" />
/// </summary>
[Flags]
public enum Source
{
    FormField = 1 << 0,
    RouteParam = 1 << 1,
    QueryParam = 1 << 2,
    All = FormField | RouteParam | QueryParam,
}
