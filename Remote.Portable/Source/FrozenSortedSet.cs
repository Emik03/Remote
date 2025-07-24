// SPDX-License-Identifier: MPL-2.0
namespace Remote.Portable;

/// <summary>
/// Represents the value in each entry within <see cref="FrozenSortedDictionary"/>,
/// encapsulating a set for fast lookup, and ordered array for sorted enumeration.
/// </summary>
/// <param name="Set">The set.</param>
/// <param name="Array">The array.</param>
public readonly record struct FrozenSortedSet(FrozenSet<string> Set, ImmutableArray<string> Array)
    : IReadOnlySet<string>, IEqualityOperators<FrozenSortedSet, FrozenSortedSet, bool>
{
    /// <inheritdoc />
    int IReadOnlyCollection<string>.Count => Array.Length;

    /// <summary>Constructs the <see cref="FrozenSortedSet"/> from the key-value-pair.</summary>
    /// <typeparam name="T">The type of enumerable.</typeparam>
    /// <param name="kvp">The pair.</param>
    /// <returns>
    /// The constructed <see cref="FrozenSortedSet"/> based on the values in the parameter <paramref name="kvp"/>.
    /// </returns>
    public static FrozenSortedSet From<T>(KeyValuePair<string, T> kvp)
        where T : IEnumerable<string>
    {
        var frozen = kvp.Value.ToFrozenSet(FrozenSortedDictionary.Comparer);
        return new(frozen, [..frozen.Order(FrozenSortedDictionary.Comparer)]);
    }

    /// <inheritdoc cref="Enumerable.Any{T}(IEnumerable{T}, Func{T, bool})"/>
    public bool Any(Func<string, bool> func) => !Array.IsDefaultOrEmpty && Array.Any(func);

    /// <inheritdoc cref="FrozenSet{T}.Contains"/>
    public bool Contains(string item) => Array.IsDefaultOrEmpty || Set.Contains(item);

    /// <inheritdoc />
    public bool IsProperSubsetOf(IEnumerable<string> other) => Set.IsProperSubsetOf(other);

    /// <inheritdoc />
    public bool IsProperSupersetOf(IEnumerable<string> other) => Set.IsProperSupersetOf(other);

    /// <inheritdoc />
    public bool IsSubsetOf(IEnumerable<string> other) => Set.IsSubsetOf(other);

    /// <inheritdoc />
    public bool IsSupersetOf(IEnumerable<string> other) => Set.IsSupersetOf(other);

    /// <inheritdoc />
    public bool Overlaps(IEnumerable<string> other) => Set.Overlaps(other);

    /// <inheritdoc />
    public bool SetEquals(IEnumerable<string> other) => Set.SetEquals(other);

    /// <inheritdoc cref="Enumerable.Count{T}(IEnumerable{T}, Func{T, bool})"/>
    public int Count(Func<string, bool> func)
    {
        var count = 0;

        foreach (var element in Array.AsSpan())
            if (func(element))
                count++;

        return count;
    }

    /// <inheritdoc cref="ImmutableArray{T}.GetEnumerator"/>
    public ImmutableArray<string>.Enumerator GetEnumerator() => Array.GetEnumerator();

    /// <inheritdoc />
    IEnumerator<string> IEnumerable<string>.GetEnumerator() => Array.AsEnumerable().GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => Array.AsEnumerable().GetEnumerator();
}
