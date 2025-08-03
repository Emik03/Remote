// SPDX-License-Identifier: MPL-2.0
namespace Remote.Portable;

/// <summary>The record for processing <see cref="OnAnd"/> and whether something is in logic.</summary>
/// <param name="CurrentItems">The list of items received.</param>
/// <param name="DisabledCategories">The set of categories that are disabled.</param>
/// <param name="DisabledLocations">The set of locations that are disabled.</param>
/// <param name="DisabledItems">The set of items that are disabled.</param>
/// <param name="HiddenCategories">The set of categories that shouldn't be visible to the user.</param>
/// <param name="LocationsToLogic">The conversion from locations to its <see cref="OnAnd"/> instances.</param>
/// <param name="CategoryToLocations">The conversion from categories to its set of locations.</param>
/// <param name="CategoryToYaml">The conversion for categories to the yaml options that it falls under.</param>
/// <param name="CategoryToItems">The conversion from categories to its set of items.</param>
/// <param name="ItemToCategories">The conversion from items to the set of categories it falls under.</param>
/// <param name="ItemCount">The conversion from items to the amount of that item.</param>
/// <param name="CategoryCount">The conversion from categories to the amount of that category.</param>
/// <param name="ItemToPhantoms">The conversion from items to its phantom items.</param>
/// <param name="PhantomToItems">The conversion from phantom items to its items.</param>
/// <param name="Yaml">The yaml options.</param>
/// <param name="IsOpt">Whether to clamp requirements based on <see cref="Yaml"/>.</param>
[CLSCompliant(false)]
public sealed partial record ApEvaluator(
    IReadOnlyCollection<string> CurrentItems,
    FrozenSet<string> DisabledCategories,
    FrozenSet<string> DisabledLocations,
    FrozenSet<string> DisabledItems,
    FrozenSet<string> HiddenCategories,
    FrozenDictionary<string, ApLogic> LocationsToLogic,
    FrozenSortedDictionary CategoryToLocations,
    FrozenSortedDictionary CategoryToYaml,
    FrozenSortedDictionary CategoryToItems,
    FrozenSortedDictionary ItemToCategories,
    FrozenDictionary<string, int> ItemCount,
    FrozenDictionary<string, int> CategoryCount,
    FrozenDictionary<string, ImmutableArray<(string PhantomItem, int Count)>> ItemToPhantoms,
    FrozenDictionary<string, ImmutableArray<(string Item, int Count)>> PhantomToItems,
    FrozenDictionary<string, int> Yaml,
    bool IsOpt
) : IEqualityOperators<ApEvaluator, ApEvaluator, bool>
{
    /// <summary>Initializes a new instance of the <see cref="ApEvaluator"/> record.</summary>
    /// <param name="currentItems">The list of items received.</param>
    /// <param name="disabledCategories">The set of categories that are disabled.</param>
    /// <param name="hiddenCategories">The set of categories that shouldn't be visible to the user.</param>
    /// <param name="locationsToLogic">The conversion from locations to its <see cref="OnAnd"/> instances.</param>
    /// <param name="categoryToLocations">The conversion from categories to its set of locations.</param>
    /// <param name="categoryToYaml">The conversion for categories to the yaml options that it falls under.</param>
    /// <param name="itemToCategories">The conversion from items to the set of categories it falls under.</param>
    /// <param name="itemCount">The conversion from items to the amount of that item.</param>
    /// <param name="itemToPhantoms">The conversion from items to its phantom items.</param>
    /// <param name="yaml">The yaml options.</param>
    public ApEvaluator(
        IReadOnlyCollection<string> currentItems,
        FrozenSet<string> disabledCategories,
        FrozenSet<string> hiddenCategories,
        FrozenDictionary<string, ApLogic> locationsToLogic,
        FrozenSortedDictionary categoryToLocations,
        FrozenSortedDictionary categoryToYaml,
        FrozenSortedDictionary itemToCategories,
        FrozenDictionary<string, int> itemCount,
        FrozenDictionary<string, ImmutableArray<(string PhantomItem, int Count)>> itemToPhantoms,
        FrozenDictionary<string, int> yaml
    )
        : this(
            currentItems,
            disabledCategories,
            Infer(disabledCategories, categoryToLocations, true),
            Infer(disabledCategories, itemToCategories, false),
            hiddenCategories,
            locationsToLogic,
            categoryToLocations,
            categoryToYaml,
            itemToCategories.Invert(),
            itemToCategories,
            itemCount,
            Infer(itemToCategories, itemCount),
            itemToPhantoms,
            Infer(itemToPhantoms),
            yaml,
            false
        ) { }

    /// <summary>Attempts to process the manual <c>.apworld</c>.</summary>
    /// <param name="currentItems">The list of items.</param>
    /// <param name="yaml">The yaml options.</param>
    /// <param name="directory">The directory containing all yaml files.</param>
    /// <param name="ap">The path to the archipelago repository.</param>
    /// <param name="python">The path to the python binary to execute python.</param>
    /// <param name="goalGetter">The wrapper to get the goal data.</param>
    /// <param name="logger">The current status.</param>
    /// <returns>
    /// The <see cref="ApEvaluator"/> to evaluate <see cref="OnAnd"/>, or <see langword="null"/> if not a manual world,
    /// parsing failed, or the <c>.apworld</c> doesn't exist.
    /// </returns>
    public static ApEvaluator? Read(
        IReadOnlyCollection<string> currentItems,
        ApYaml yaml,
        string directory,
        string? ap = null,
        string? python = "python",
        Func<GoalData?>? goalGetter = null,
        Action<string>? logger = null
    )
    {
        if (ApReader.Find(yaml.Game, directory, logger) is not { } path)
            return null;

        try
        {
            return ReadZip(currentItems, yaml, path, ap, python, goalGetter, logger);
        }
#pragma warning disable CA1031
        catch (Exception e)
#pragma warning restore CA1031
        {
            logger?.Invoke(e.Message);
            return null;
        }
    }

    /// <summary>Reads the path as a zip file. Can throw.</summary>
    /// <param name="currentItems">The list of items.</param>
    /// <param name="yaml">The yaml options.</param>
    /// <param name="path">The path to the zip file.</param>
    /// <param name="ap">The path to the archipelago repository.</param>
    /// <param name="python">The path to the python binary to execute python.</param>
    /// <param name="goalGetter">The wrapper to get the goal data.</param>
    /// <param name="logger">The current status.</param>
    /// <returns>
    /// The <see cref="ApEvaluator"/> to evaluate <see cref="OnAnd"/>, or <see langword="null"/> if not a manual world,
    /// parsing failed, or the <c>.apworld</c> doesn't exist.
    /// </returns>
    static ApEvaluator? ReadZip(
        IReadOnlyCollection<string> currentItems,
        ApYaml yaml,
        string path,
        string? ap,
        string? python,
        Func<GoalData?>? goalGetter,
        Action<string>? logger
    )
    {
        ApReader reader = new(path, ap, python, logger);
        logger?.Invoke("Copying yaml options found .apworld...");
        yaml.CopyFrom(reader.Options);

        if (reader.ExtractCategories(yaml, logger) is not ({ } disabled, { } hidden, var categoryToYaml) ||
            reader.ExtractItems(logger) is not (var itemToCategories, { } itemCount, { } itemValues) ||
            reader.ExtractLocations(yaml, goalGetter, logger) is not ({ } locationsToLogic, var categoryToLocations))
            return null;

        logger?.Invoke("Finalizing...");

        return new(
            currentItems,
            disabled,
            hidden,
            locationsToLogic,
            categoryToLocations,
            categoryToYaml,
            itemToCategories,
            itemCount,
            itemValues,
            yaml.Options.ToFrozenDictionary()
        );
    }

    /// <summary>Gets the disabled elements.</summary>
    /// <param name="disabledCategories">The set of disabled categories.</param>
    /// <param name="dictionary">The conversion.</param>
    /// <param name="isForward">
    /// Whether the conversion is forward, i.e. from the category to the set of elements, and not the other way around.
    /// </param>
    /// <returns>The disabled elements.</returns>
    // ReSharper disable ParameterTypeCanBeEnumerable.Local SuggestBaseTypeForParameter
    static FrozenSet<string> Infer(
        FrozenSet<string> disabledCategories,
        FrozenSortedDictionary dictionary,
        bool isForward
    ) =>
        isForward
            ? disabledCategories.SelectMany(x => dictionary[x]).ToFrozenSet(FrozenSortedDictionary.Comparer)
            : dictionary.Array.Where(x => x.Value.Any(disabledCategories.Contains))
               .Select(x => x.Key)
               .ToFrozenSet(FrozenSortedDictionary.Comparer);

    /// <summary>Infers the category count.</summary>
    /// <param name="itemToPhantoms">The conversion from items to the set of categories it falls under.</param>
    /// <returns>The category count.</returns>
    static FrozenDictionary<string, ImmutableArray<(string Item, int Count)>> Infer(
        FrozenDictionary<string, ImmutableArray<(string PhantomItem, int Count)>> itemToPhantoms
    )
    {
        Dictionary<string, ImmutableArray<(string Item, int Count)>.Builder> ret = new(FrozenSortedDictionary.Comparer);

        foreach (var (item, values) in itemToPhantoms)
            foreach (var (phantomItem, count) in values)
                (CollectionsMarshal.GetValueRefOrAddDefault(ret, phantomItem, out _) ??=
                    ImmutableArray.CreateBuilder<(string, int)>()).Add((item, count));

        return ret.ToFrozenDictionary(x => x.Key, x => x.Value.DrainToImmutable(), FrozenSortedDictionary.Comparer);
    }

    /// <summary>Infers the category count.</summary>
    /// <param name="itemToCategories">The conversion from items to the set of categories it falls under.</param>
    /// <param name="itemCount">The conversion from items to the amount of that item.</param>
    /// <returns>The category count.</returns>
    static FrozenDictionary<string, int> Infer(
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
