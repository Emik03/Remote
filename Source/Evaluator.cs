// SPDX-License-Identifier: MPL-2.0
namespace Remote;

/// <summary>The record for processing <see cref="OnAnd"/> and whether something is in logic.</summary>
/// <param name="Helper">The list of items received.</param>
/// <param name="HiddenCategories">The set of categories that shouldn't be visible to the user.</param>
/// <param name="LocationsToLogic">The conversion from locations to its <see cref="OnAnd"/> instances.</param>
/// <param name="CategoryToLocations">The conversion from categories to its set of locations.</param>
/// <param name="CategoryToYaml">The conversion for categories to the yaml options that it falls under.</param>
/// <param name="CategoryToItems">The conversion from categories to its set of items.</param>
/// <param name="ItemToCategories">The conversion from items to the set of categories it falls under.</param>
/// <param name="ItemCount">The conversion from items to the amount of that item.</param>
/// <param name="CategoryCount">The conversion from categories to the amount of that category.</param>
/// <param name="ItemValues">The conversion from items to its phantom items.</param>
/// <param name="Yaml">The yaml options.</param>
/// <param name="IsOptAll">Whether to clamp requirements based on <see cref="Yaml"/>.</param>
[CLSCompliant(false)]
public sealed partial record Evaluator(
    IReceivedItemsHelper Helper,
    FrozenSet<string> HiddenCategories,
    FrozenDictionary<string, Logic> LocationsToLogic,
    FrozenSortedDictionary CategoryToLocations,
    FrozenSortedDictionary CategoryToYaml,
    FrozenSortedDictionary CategoryToItems,
    FrozenSortedDictionary ItemToCategories,
    FrozenDictionary<string, int> ItemCount,
    FrozenDictionary<string, int> CategoryCount,
    FrozenDictionary<string, ImmutableArray<(string PhantomItem, int Count)>> ItemValues,
    FrozenDictionary<string, int> Yaml,
    bool IsOptAll
)
{
    /// <summary>Initializes a new instance of the <see cref="Evaluator"/> record.</summary>
    /// <param name="helper">The list of items received.</param>
    /// <param name="hiddenCategories">The set of categories that shouldn't be visible to the user.</param>
    /// <param name="locationsToLogic">The conversion from locations to its <see cref="OnAnd"/> instances.</param>
    /// <param name="categoryToLocations">The conversion from categories to its set of locations.</param>
    /// <param name="categoryToYaml">The conversion for categories to the yaml options that it falls under.</param>
    /// <param name="itemToCategories">The conversion from items to the set of categories it falls under.</param>
    /// <param name="itemCount">The conversion from items to the amount of that item.</param>
    /// <param name="itemValues">The conversion from items to its phantom items.</param>
    /// <param name="yaml">The yaml options.</param>
    public Evaluator(
        IReceivedItemsHelper helper,
        FrozenSet<string> hiddenCategories,
        FrozenDictionary<string, Logic> locationsToLogic,
        FrozenSortedDictionary categoryToLocations,
        FrozenSortedDictionary categoryToYaml,
        FrozenSortedDictionary itemToCategories,
        FrozenDictionary<string, int> itemCount,
        FrozenDictionary<string, ImmutableArray<(string PhantomItem, int Count)>> itemValues,
        FrozenDictionary<string, int> yaml
    )
        : this(
            helper,
            hiddenCategories,
            locationsToLogic,
            categoryToLocations,
            categoryToYaml,
            itemToCategories.Invert(),
            itemToCategories,
            itemCount,
            Infer(itemToCategories, itemCount),
            itemValues,
            yaml,
            false
        ) { }

    /// <summary>Attempts to find the <c>.apworld</c>.</summary>
    /// <param name="yaml">The yaml options.</param>
    /// <param name="preferences">The user preferences.</param>
    /// <returns>The path to the <c>.apworld</c>, or <see langword="null"/> if none found.</returns>
    public static string? FindApWorld(Yaml yaml, Preferences preferences)
    {
        var world = $"{yaml.Game}.apworld";

        string? Enumerate(string directory) =>
            Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory)
                   .FirstOrDefault(x => world.Equals(Path.GetFileName(x.AsSpan()), StringComparison.OrdinalIgnoreCase))
                : null;

        return Enumerate(Path.Join(preferences.Directory, "worlds")) ??
            Enumerate(Path.Join(preferences.Directory, "custom_worlds")) ??
            Enumerate(preferences.Directory);
    }

    /// <summary>Attempts to process the manual <c>.apworld</c>.</summary>
    /// <param name="w">The wrapper to get the goal data.</param>
    /// <param name="h">The list of items.</param>
    /// <param name="y">The yaml options.</param>
    /// <param name="preferences">
    /// The user preferences, containing the directory of the Archipelago installation.
    /// </param>
    /// <returns>
    /// The <see cref="Evaluator"/> to evaluate <see cref="OnAnd"/>, or <see langword="null"/> if not a manual world,
    /// parsing failed, or the <c>.apworld</c> doesn't exist.
    /// </returns>
    public static Evaluator? Read(IDataStorageWrapper w, IReceivedItemsHelper h, Yaml y, Preferences preferences) =>
        FindApWorld(y, preferences) is not { } path || Go(ReadZip, (w, h), y, preferences, path, out _, out var ok)
            ? null
            : ok;

    /// <summary>Reads the path as a zip file. Can throw.</summary>
    /// <param name="interfaces">The interfaces.</param>
    /// <param name="yaml">The yaml options.</param>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="path">The path to the zip file.</param>
    /// <returns>
    /// The <see cref="Evaluator"/> to evaluate <see cref="OnAnd"/>, or <see langword="null"/> if not a manual world,
    /// parsing failed, or the <c>.apworld</c> doesn't exist.
    /// </returns>
    static Evaluator? ReadZip(
        (IDataStorageWrapper Wrapper, IReceivedItemsHelper Helper) interfaces,
        Yaml yaml,
        Preferences preferences,
        string path
    )
    {
        var (wrapper, helper) = interfaces;
        ApWorldReader reader = new(path, preferences);
        yaml.CopyFrom(reader.Options);

        return reader.ExtractCategories() is ({ } hiddenCategories, var categoryToYaml) &&
            reader.ExtractItems() is (var itemToCategories, { } itemCount, { } itemValues) &&
            reader.ExtractLocations(wrapper, yaml) is ({ } locationsToLogic, var categoryToLocations)
                ? new Evaluator(
                    helper,
                    hiddenCategories,
                    locationsToLogic,
                    categoryToLocations,
                    categoryToYaml,
                    itemToCategories,
                    itemCount,
                    itemValues,
                    yaml.Options.ToFrozenDictionary()
                )
                : null;
    }

    /// <summary>Infers the category count.</summary>
    /// <param name="itemToCategories">The conversion from items to the set of categories it falls under.</param>
    /// <param name="itemCount">The conversion from items to the amount of that item.</param>
    /// <returns>The category count.</returns>
    static FrozenDictionary<string, int> Infer( // ReSharper disable once SuggestBaseTypeForParameter
        FrozenSortedDictionary itemToCategories,
        FrozenDictionary<string, int> itemCount
    )
    {
        Dictionary<string, int> ret = new(FrozenSortedDictionary.Comparer);

        foreach (var (item, count) in itemCount)
            if (itemToCategories.TryGetValue(item, out var categories))
                foreach (var category in categories)
                    CollectionsMarshal.GetValueRefOrAddDefault(ret, category, out _) += count;

        return ret.ToFrozenDictionary(FrozenSortedDictionary.Comparer);
    }
}
