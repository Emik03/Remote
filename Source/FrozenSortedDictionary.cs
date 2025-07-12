// SPDX-License-Identifier: MPL-2.0
namespace Remote;

/// <summary>Encapsulates a dictionary for fast lookup, and ordered array for sorted enumeration.</summary>
/// <param name="Dictionary">The dictionary.</param>
/// <param name="Array">The array.</param>
public readonly record struct FrozenSortedDictionary(
    FrozenDictionary<string, FrozenSortedDictionary.Element> Dictionary,
    ImmutableArray<KeyValuePair<string, FrozenSortedDictionary.Element>> Array
)
{
    /// <summary>
    /// Represents the value in each entry within <see cref="FrozenSortedDictionary"/>,
    /// encapsulating a set for fast lookup, and ordered array for sorted enumeration.
    /// </summary>
    /// <param name="Set">The set.</param>
    /// <param name="Array">The array.</param>
    public readonly record struct Element(FrozenSet<string> Set, ImmutableArray<string> Array) : IReadOnlySet<string>
    {
        /// <inheritdoc />
        int IReadOnlyCollection<string>.Count => Array.Length;

        /// <summary>Constructs the <see cref="Element"/> from the key-value-pair.</summary>
        /// <typeparam name="T">The type of enumerable.</typeparam>
        /// <param name="kvp">The pair.</param>
        /// <returns>
        /// The constructed <see cref="Element"/> based on the values in the parameter <paramref name="kvp"/>.
        /// </returns>
        public static Element From<T>(KeyValuePair<string, T> kvp)
            where T : IEnumerable<string>
        {
            var frozen = kvp.Value.ToFrozenSet(Comparer);
            return new(frozen, [..frozen.Order(Comparer)]);
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

    /// <summary>Gets the main comparer used.</summary>
    public static StringComparer Comparer { get; } =
#if NET10_0_OR_GREATER
        StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering);
#else
        StringComparer.Ordinal;
#endif
    /// <inheritdoc cref="ImmutableDictionary{TKey, TValue}.this"/>
    public Element this[string key] => Dictionary[key];

    /// <inheritdoc cref="ImmutableArray{T}.Empty"/>
    public static FrozenSortedDictionary Empty =>
        new(FrozenDictionary<string, Element>.Empty, ImmutableArray<KeyValuePair<string, Element>>.Empty);

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
        var frozen = dictionary.ToFrozenDictionary(x => x.Key, Element.From, Comparer);
        return new(frozen, [..frozen.OrderBy(x => x.Key, Comparer)]);
    }

    /// <inheritdoc cref="ImmutableDictionary{TKey, TValue}.TryGetValue"/>
    public bool TryGetValue(ReadOnlyMemory<char> key, out Element value) => TryGetValue(key.Span, out value);

    /// <inheritdoc cref="ImmutableDictionary{TKey, TValue}.TryGetValue"/>
    public bool TryGetValue(ReadOnlySpan<char> key, out Element value) =>
        Dictionary.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(key, out value);

    /// <inheritdoc cref="ImmutableDictionary{TKey, TValue}.TryGetValue"/>
    public bool TryGetValue(string key, out Element value) => Dictionary.TryGetValue(key, out value);

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
    public ImmutableArray<KeyValuePair<string, Element>>.Enumerator GetEnumerator() => Array.GetEnumerator();
}
