using FluentValidation.Results;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FastEndpoints;

/// <summary>
/// the base class all fast endpoints inherit from
/// </summary>
public abstract partial class BaseEndpoint : IEndpoint
{
    List<ValidationFailure>? _failures;

    internal abstract Task ExecAsync(CancellationToken ct);

    [DontInject]
    public EndpointDefinition Definition { get; internal set; } = null!;

    [DontInject]
    public HttpContext HttpContext { get; internal set; } = null!;

    public List<ValidationFailure> ValidationFailures => _failures ??= []; //lazy init to prevent alloc at startup

    /// <summary>
    /// use this method to configure how the endpoint should be listening to incoming requests.
    /// <para>HINT: it is only called once during endpoint auto registration during app startup.</para>
    /// </summary>
    [NotImplemented]
    public virtual void Configure()
        => throw new NotImplementedException();

    public virtual void Verbs(params string[] methods)
        => throw new NotImplementedException();

    [UsedImplicitly]
    public virtual void Routes(params string[] patterns)
        => throw new NotImplementedException();

    //this is here just so the derived endpoint class can seal it.
    protected virtual void Group<TEndpointGroup>() where TEndpointGroup : Group, new()
        => throw new NotImplementedException();

    [GeneratedRegex("[^a-zA-Z0-9]+")]
    private static partial Regex LetterOrDigitRegex();

    protected static string GetAclHash(string input)
    {
        //NOTE: if modifying this algo, update FastEndpoints.Generator.AccessControlGenerator.Permission.GetAclHash() method also!
        var base64Hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Sanitize(input).ToUpperInvariant())));

        return new(base64Hash.Where(char.IsLetterOrDigit).Take(3).Select(char.ToUpper).ToArray());

        static string Sanitize(string input)
            => LetterOrDigitRegex().Replace(input, "_");
    }
}