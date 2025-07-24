// SPDX-License-Identifier: MPL-2.0
namespace Remote.Portable;

/// <summary>Encapsulates a dictionary for fast lookup, and ordered array for sorted enumeration.</summary>
/// <param name="Dictionary">The dictionary.</param>
/// <param name="Array">The array.</param>
public readonly record struct FrozenSortedDictionary(
    FrozenDictionary<string, FrozenSortedSet> Dictionary,
    ImmutableArray<KeyValuePair<string, FrozenSortedSet>> Array
) : IEqualityOperators<FrozenSortedDictionary, FrozenSortedDictionary, bool>
{
    /// <summary>Gets the main comparer used.</summary>
    public static StringComparer Comparer { get; } =
#if NET10_0_OR_GREATER
        StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering);
#else
        StringComparer.Ordinal;
#endif
    /// <inheritdoc cref="ImmutableDictionary{TKey, TValue}.this"/>
    public FrozenSortedSet this[string key] => Dictionary[key];

    /// <inheritdoc cref="ImmutableArray{T}.Empty"/>
    public static FrozenSortedDictionary Empty => new(FrozenDictionary<string, FrozenSortedSet>.Empty, []);

    /// <summary>Constructs the <see cref="FrozenSortedDictionary"/> from the dictionary.</summary>
    /// <typeparam name="T">The type of value which is an enumerable.</typeparam>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>
    /// The constructed <see cref="FrozenSortedDictionary"/> based on
    /// the values in the parameter <paramref name="dictionary"/>.
    /// </returns>
    public static FrozenSortedDictionary From<T>(IReadOnlyDictionary<string, T> dictionary)
        where T : IEnumerable<string>
    {
        var frozen = dictionary.ToFrozenDictionary(x => x.Key, FrozenSortedSet.From, Comparer);
        return new(frozen, [..frozen.OrderBy(x => x.Key, Comparer)]);
    }

    /// <inheritdoc cref="ImmutableDictionary{TKey, TValue}.TryGetValue"/>
    public bool TryGetValue(ReadOnlyMemory<char> key, out FrozenSortedSet value) => TryGetValue(key.Span, out value);

    /// <inheritdoc cref="ImmutableDictionary{TKey, TValue}.TryGetValue"/>
    public bool TryGetValue(ReadOnlySpan<char> key, out FrozenSortedSet value) =>
        Dictionary.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(key, out value);

    /// <inheritdoc cref="ImmutableDictionary{TKey, TValue}.TryGetValue"/>
    public bool TryGetValue(string key, out FrozenSortedSet value) => Dictionary.TryGetValue(key, out value);

    /// <summary>Inverts the collection such that the keys become values and vice versa.</summary>
    /// <returns>The inverted collection.</returns>
    public FrozenSortedDictionary Invert()
    {
        Dictionary<string, HashSet<string>> ret = new(Comparer);

        foreach (var (key, (_, values)) in this)
            foreach (var value in values)
                (CollectionsMarshal.GetValueRefOrAddDefault(ret, value, out _) ??= new(Comparer)).Add(key);

        return From(ret);
    }

    /// <inheritdoc cref="ImmutableArray{T}.GetEnumerator"/>
    public ImmutableArray<KeyValuePair<string, FrozenSortedSet>>.Enumerator GetEnumerator() => Array.GetEnumerator();
}
