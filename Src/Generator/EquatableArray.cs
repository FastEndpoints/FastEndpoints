using System.Collections;

namespace FastEndpoints.Generator;

/// <summary>
/// an array with value equality so that incremental pipeline models holding collections can be cached by roslyn.
/// </summary>
readonly struct EquatableArray<T>(T[] items) : IEquatable<EquatableArray<T>>, IEnumerable<T> where T : IEquatable<T>
{
    readonly T[]? _items = items;

    T[] Items => _items ?? [];

    public int Length => Items.Length;

    public T this[int index] => Items[index];

    public bool Equals(EquatableArray<T> other)
        => Items.SequenceEqual(other.Items);

    public override bool Equals(object? obj)
        => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;

        foreach (var item in Items)
            hash = unchecked(hash * 31 + item.GetHashCode());

        return hash;
    }

    public IEnumerator<T> GetEnumerator()
        => ((IEnumerable<T>)Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
