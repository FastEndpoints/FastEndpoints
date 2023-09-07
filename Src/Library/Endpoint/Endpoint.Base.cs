using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FastEndpoints;

/// <summary>
/// the base class all fast endpoints inherit from
/// </summary>
public abstract class BaseEndpoint : IEndpoint
{
    private List<ValidationFailure> _failures;

    internal abstract Task ExecAsync(CancellationToken ct);

    ///<inheritdoc/>
    [DontInject]
    public EndpointDefinition Definition { get; internal set; }

    ///<inheritdoc/>
    [DontInject]
    public HttpContext HttpContext { get; internal set; }

    ///<inheritdoc/>
    public List<ValidationFailure> ValidationFailures => _failures ??= new();

    /// <summary>
    /// use this method to configure how the endpoint should be listening to incoming requests.
    /// <para>HINT: it is only called once during endpoint auto registration during app startup.</para>
    /// </summary>
    [NotImplemented]
    public virtual void Configure() => throw new NotImplementedException();

    public virtual void Verbs(params string[] methods) => throw new NotImplementedException();

    //this is here just so the derived endpoint class can seal it.
    protected virtual void Group<TEndpointGroup>() where TEndpointGroup : notnull, Group, new() => throw new NotImplementedException();

    private static readonly Regex regex = new("[^a-zA-Z0-9]+", RegexOptions.Compiled);
    internal static string GetAclHash(string input)
    {
        //NOTE: if modifying this algo, update FastEndpoints.Generator.AccessControlGenerator.Permission.GetAclHash() method also!
        using var sha256 = SHA256.Create();
        var base64Hash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(Sanitize(input).ToUpperInvariant())));
        return new(base64Hash.Where(char.IsLetterOrDigit).Take(3).Select(char.ToUpper).ToArray());

        static string Sanitize(string input)
           => regex.Replace(input, "_");
    }
}