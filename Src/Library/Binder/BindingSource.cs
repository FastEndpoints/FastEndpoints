namespace FastEndpoints;

/// <summary>
/// enum for choosing which binding sources the default request binder should use
/// </summary>
[Flags]
public enum BindingSource
{
    JsonBody = 1 << 0,
    FormFields = 1 << 1,
    RouteValues = 1 << 2,
    QueryParams = 1 << 3,
    UserClaims = 1 << 4,
    Headers = 1 << 5,
    Permissions = 1 << 6
}
