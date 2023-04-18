using Microsoft.CodeAnalysis;

namespace FastEndpoints.Generator;

internal sealed class TypeDescription : IEquatable<TypeDescription>
{
    public TypeDescription(INamedTypeSymbol symbol)
    {
        Namespace = symbol.ContainingNamespace.Name;
        Name = symbol.Name;
    }

    public TypeDescription(string typeName)
    {
        var segments = typeName.Split('.');

        Namespace = segments[0];
        Name = segments[1];
    }

    public string Namespace { get; }

    public string Name { get; }

    public bool Equals(TypeDescription? other)
    {
        return other is not null && (ReferenceEquals(this, other) ||
               (string.Equals(Namespace, other.Namespace, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)));
    }

    public override bool Equals(object? obj)
        => obj is not null && (ReferenceEquals(this, obj) || (obj.GetType() == GetType() && Equals((TypeDescription)obj)));

    public override int GetHashCode()
    {
        unchecked
        {
            return (StringComparer.OrdinalIgnoreCase.GetHashCode(Namespace) * 397) ^
                    StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
        }
    }

    public static bool operator ==(TypeDescription? left, TypeDescription? right) => Equals(left, right);

    public static bool operator !=(TypeDescription? left, TypeDescription? right) => !Equals(left, right);
}