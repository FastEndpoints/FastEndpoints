namespace FastEndpoints;

/// <summary>
/// global security options
/// </summary>
public sealed class SecurityOptions
{
    /// <summary>
    /// specify a custom claim type used to identity the name of a user principal. defaults to `name`.
    /// <para>
    /// WARNING: do not change the default unless you fully comprehend what you're doing!!!
    /// </para>
    /// </summary>
    public string NameClaimType { internal get; set; }
        = "name";

    /// <summary>
    /// specify a custom claim type used to identify permissions of a user principal. defaults to <c>permissions</c>.
    /// <para>
    /// WARNING: do not change the default unless you fully comprehend what you're doing!!!
    /// </para>
    /// </summary>
    public string PermissionsClaimType { internal get; set; }
        = "permissions"; //should never change from "permissions" or third party auth providers such as Auth0 won't work.

    /// <summary>
    /// specify a custom claim type used to identify scopes. defaults to <c>scope</c>.
    /// <para>
    /// WARNING: do not change the default unless you fully comprehend what you're doing!!!
    /// </para>
    /// </summary>
    public string ScopeClaimType { internal get; set; }
        = "scope";

    /// <summary>
    /// a function for parsing the 'scope' claim value and producing a collection of scopes.
    /// the default function simply splits the input string using the space character.
    /// </summary>
    public Func<string, IEnumerable<string>> ScopeParser { internal get; set; }
        = value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// specify a custom claim type used to identify roles of a user principal. defaults to `role`.
    /// <para>
    /// WARNING: do not change the default unless you fully comprehend what you're doing!!!
    /// </para>
    /// </summary>
    public string RoleClaimType { internal get; set; }
        = "role";
}