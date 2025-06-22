// SPDX-License-Identifier: MPL-2.0
namespace Remote;

using JsonSerializer = System.Text.Json.JsonSerializer;

/// <summary>The record for processing <see cref="Logic"/> and whether something is in logic.</summary>
/// <param name="Helper">The list of items received.</param>
/// <param name="HiddenCategories">The set of categories that shouldn't be visible to the user.</param>
/// <param name="LocationsToLogic">The conversion from locations to its <see cref="Logic"/> instances.</param>
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
    /// <param name="locationsToLogic">The conversion from locations to its <see cref="Logic"/> instances.</param>
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

    /// <summary>Attempts to process the manual <c>.apworld</c>.</summary>
    /// <param name="helper">The list of items received.</param>
    /// <param name="yaml">The yaml options.</param>
    /// <param name="preferences">
    /// The user preferences, containing the directory of the Archipelago installation.
    /// </param>
    /// <returns>
    /// The <see cref="Evaluator"/> to evaluate <see cref="Logic"/>, or <see langword="null"/> if not a manual world,
    /// parsing failed, or the <c>.apworld</c> doesn't exist.
    /// </returns>
    public static Evaluator? Read(IReceivedItemsHelper helper, Yaml yaml, Preferences preferences) =>
        FindApWorld(yaml, preferences) is not { } path || Go(ReadZip, helper, yaml, path, out _, out var ok)
            ? null
            : ok;

    /// <summary>Whether it contains a <c>hidden</c> property that is true.</summary>
    /// <param name="kvp">The pair to check.</param>
    /// <returns>Whether to include this in <see cref="HiddenCategories"/>.</returns>
    static bool IsHiddenTrue(KeyValuePair<string, JsonNode?> kvp) =>
        kvp.Value is JsonObject obj &&
        obj.TryGetPropertyValue("hidden", out var hidden) &&
        hidden?.GetValueKind() is JsonValueKind.True;

    /// <summary>Whether it contains a <c>starting</c> property that is true.</summary>
    /// <param name="value">The value to check.</param>
    /// <returns>Whether this is a starting region.</returns>
    static bool IsStarting(JsonNode? value) =>
        value is JsonObject obj &&
        obj.TryGetPropertyValue("starting", out var starting) &&
        starting?.GetValueKind() is JsonValueKind.True;

    /// <summary>Attempts to find the <c>.apworld</c>.</summary>
    /// <param name="yaml">The yaml options.</param>
    /// <param name="preferences">The user preferences.</param>
    /// <returns>The path to the <c>.apworld</c>, or <see langword="null"/> if none found.</returns>
    static string? FindApWorld(Yaml yaml, Preferences preferences) =>
        $"{yaml.Game}.apworld" is var apworld &&
        Path.Join(preferences.Directory, "worlds", apworld) is var first &&
        File.Exists(first) ? first :
            Path.Join(preferences.Directory, "custom_worlds", $"{yaml.Game}.apworld") is var second &&
            File.Exists(second) ?
                second : null;

    /// <summary>Reads the path as a zip file. Can throw.</summary>
    /// <param name="helper">The list of items received.</param>
    /// <param name="yaml">The yaml options.</param>
    /// <param name="path">The path to the zip file.</param>
    /// <returns>
    /// The <see cref="Evaluator"/> to evaluate <see cref="Logic"/>, or <see langword="null"/> if not a manual world,
    /// parsing failed, or the <c>.apworld</c> doesn't exist.
    /// </returns>
    static Evaluator? ReadZip(IReceivedItemsHelper helper, Yaml yaml, string path)
    {
        using ZipArchive zip = new(File.OpenRead(path));

        return ExtractCategories(zip) is ({ } hiddenCategories, var categoryToYaml) &&
            ExtractItems(zip) is (var itemToCategories, { } itemCount, { } itemValues) &&
            ExtractLocations(zip) is ({ } locationsToLogic, var categoryToLocations)
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

    /// <summary>Extracts all categories.</summary>
    /// <param name="zip">The zip archive.</param>
    /// <returns>The categories.</returns>
    static (FrozenSet<string>, FrozenSortedDictionary) ExtractCategories(ZipArchive zip) =>
        Extract<JsonObject>(zip, "/data/categories.json") is { } categories
            ? (categories.Where(IsHiddenTrue).Select(x => x.Key).ToFrozenSet(FrozenSortedDictionary.Comparer),
                FrozenSortedDictionary.From(categories.Select(Options).ToDictionary(FrozenSortedDictionary.Comparer)))
            : (FrozenSet<string>.Empty, FrozenSortedDictionary.Empty);

    /// <summary>Converts the <see cref="JsonArray"/> into the <see cref="HashSet{T}"/>.</summary>
    /// <param name="array">The array to convert.</param>
    /// <returns>The converted <see cref="HashSet{T}"/> from the parameter <paramref name="array"/>.</returns>
    static HashSet<string> JsonArrayToHashSet(JsonArray array)
    {
        static string ConfidentlyToString(JsonNode? node)
        {
            Debug.Assert(node is not null);
            return node.ToString();
        }

        return array.Where(x => x?.GetValueKind() is JsonValueKind.String)
           .Select(ConfidentlyToString)
           .ToHashSet(FrozenSortedDictionary.Comparer);
    }

    /// <summary>Attempts to get the <c>category</c> array field.</summary>
    /// <param name="obj">The node to extract.</param>
    /// <returns>The <c>category</c> array, if it exists.</returns>
    static JsonArray? Categories(JsonObject obj) =>
        obj.TryGetPropertyValue("category", out var categoriesJson)
            ? categoriesJson as JsonArray
            : new(Uncategorized);

    /// <summary>Constructs the logic based on getting to a specific region from any starting region.</summary>
    /// <remarks><para>NOTE: Currently does not consider <c>entrance_requires</c>.</para></remarks>
    /// <param name="target">The region to find.</param>
    /// <param name="regions">The root of the <c>regions.json</c> node.</param>
    /// <param name="regionToLogic">
    /// Maps the region name to the already parsed <see cref="Logic"/> for caching purposes.
    /// </param>
    /// <returns>The resulting <see cref="Logic"/> to get to the parameter <paramref name="target"/>.</returns>
    static Logic? Find(string target, JsonObject regions, Dictionary<string, Logic> regionToLogic)
    {
        Logic? path = null;

        foreach (var (name, value) in regions)
        {
            if (!IsStarting(value))
                continue;

            var visited = regions
               .Where(x => !x.Key.Equals(name, StringComparison.Ordinal) && IsStarting(x.Value))
               .Select(x => x.Key)
               .ToSet(FrozenSortedDictionary.Comparer);

            var foundTarget = false;
            var branch = Find(name, target, regions, regionToLogic, visited, ref foundTarget);

            if (foundTarget)
                path |= branch;
        }

        return path;
    }

    /// <summary>Constructs the logic based on getting to a specific region.</summary>
    /// <param name="current">The current region.</param>
    /// <param name="target">The target region.</param>
    /// <param name="regions">The root of the <c>regions.json</c> node.</param>
    /// <param name="regionToLogic">
    /// Maps the region name to the already parsed <see cref="Logic"/> for caching purposes.
    /// </param>
    /// <param name="visited">The regions that have already been visited, so as to remove unnecessary branching.</param>
    /// <param name="foundTarget">Whether the target has been found successfully.</param>
    /// <returns>The resulting <see cref="Logic"/> to get to the parameter <paramref name="target"/>.</returns>
    static Logic? Find(
        string current,
        string target,
        JsonNode regions,
        Dictionary<string, Logic> regionToLogic, // ReSharper disable once SuggestBaseTypeForParameter
        HashSet<string> visited,
        ref bool foundTarget
    )
    {
        if (regions[current] is not JsonObject obj || !visited.Add(current))
            return null;

        ref var logic = ref CollectionsMarshal.GetValueRefOrAddDefault(regionToLogic, current, out _);

        logic ??= obj.TryGetPropertyValue("requires", out var requires) &&
            requires?.GetValueKind() is JsonValueKind.String
                ? Logic.TokenizeAndParse(requires.ToString().AsMemory())
                : null;

        if (current.Equals(target, StringComparison.Ordinal))
        {
            foundTarget = true;
            return logic;
        }

        if (!obj.TryGetPropertyValue("connects_to", out var connectsTo) || connectsTo is not JsonArray array)
            return null;

        Logic? path = null;
        var exitRequires = obj.TryGetPropertyValue("exit_requires", out var e) && e is JsonObject o ? o : null;

        foreach (var connection in array)
            if (connection?.GetValueKind() is JsonValueKind.String &&
                connection.ToString() is var connectionString)
            {
                var innerFoundTarget = false;
                var l = Find(connectionString, target, regions, regionToLogic, visited.ToSet(), ref innerFoundTarget);

                if (!innerFoundTarget)
                    continue;

                foundTarget = true;

                path |= l &
                    (exitRequires?[connectionString] is var exit && exit?.GetValueKind() is JsonValueKind.String
                        ? Logic.TokenizeAndParse(exit.ToString().AsMemory())
                        : null);
            }

        return foundTarget ? logic & path : null;
    }

    /// <summary>Gets the categories alongside its yaml options.</summary>
    /// <param name="kvp">The pair to deconstruct.</param>
    /// <returns>The category with its yaml options.</returns>
    static (string, IReadOnlySet<string>) Options(KeyValuePair<string, JsonNode?> kvp) =>
        kvp.Value is JsonObject obj &&
        obj.TryGetPropertyValue("yaml_option", out var yamlOption) &&
        yamlOption is JsonArray array
            ? (kvp.Key, JsonArrayToHashSet(array))
            : (kvp.Key, FrozenSet<string>.Empty);

    /// <summary>Extracts all locations.</summary>
    /// <param name="zip">The zip archive.</param>
    /// <returns>The locations.</returns>
    static (FrozenDictionary<string, Logic>, FrozenSortedDictionary) ExtractLocations(
        ZipArchive zip
    )
    {
        if (Extract<JsonArray>(zip, "/data/locations.json") is not { } locations)
            return default;

        var regions = Extract<JsonObject>(zip, "/data/regions.json");
        Dictionary<string, Logic> locationsToLogic = new(FrozenSortedDictionary.Comparer);
        Dictionary<string, HashSet<string>> categoriesToLogic = new(FrozenSortedDictionary.Comparer);

        foreach (var location in locations)
        {
            if (location is not JsonObject o || o["name"]?.ToString() is not { } name)
                continue;

            if (o.TryGetPropertyValue("requires", out var requires) &&
                requires?.GetValueKind() is JsonValueKind.String &&
                Logic.TokenizeAndParse(requires.ToString().AsMemory()) is { } logic)
                locationsToLogic[name] = logic;

            if (regions is not null &&
                o.TryGetPropertyValue("region", out var region) &&
                region?.GetValueKind() is JsonValueKind.String)
                CollectionsMarshal.GetValueRefOrAddDefault(locationsToLogic, name, out _) &=
                    Find(region.ToString(), regions, new(FrozenSortedDictionary.Comparer));

            if (o.TryGetPropertyValue("hidden", out var hidden) && hidden?.GetValueKind() is JsonValueKind.True)
                continue;

            if (Categories(o) is not { } categories)
                continue;

            // ReSharper disable once LoopCanBePartlyConvertedToQuery
            foreach (var category in categories)
                if (category is not null)
                    (CollectionsMarshal.GetValueRefOrAddDefault(categoriesToLogic, category.ToString(), out _) ??=
                        new(FrozenSortedDictionary.Comparer)).Add(name);
        }

        return (locationsToLogic.ToFrozenDictionary(x => x.Key, x => x.Value, FrozenSortedDictionary.Comparer),
            FrozenSortedDictionary.From(categoriesToLogic));
    }

    /// <summary>Extracts all items.</summary>
    /// <param name="zip">The zip archive.</param>
    /// <returns>The items.</returns>
    static (FrozenSortedDictionary, FrozenDictionary<string, int>,
        FrozenDictionary<string, ImmutableArray<(string PhantomItem, int Count)>>) ExtractItems(ZipArchive zip)
    {
        if (Extract<JsonArray>(zip, "/data/items.json") is not { } items)
            return default;

        Dictionary<string, HashSet<string>> itemToCategories = new(FrozenSortedDictionary.Comparer);
        Dictionary<string, int> itemCount = new(FrozenSortedDictionary.Comparer);

        Dictionary<string, ImmutableArray<(string PhantomItem, int Count)>.Builder> itemValues =
            new(FrozenSortedDictionary.Comparer);

        foreach (var item in items)
        {
            if (item is not JsonObject obj ||
                !obj.TryGetPropertyValue("name", out var name) ||
                name?.GetValueKind() is not JsonValueKind.String)
                continue;

            var nameString = name.ToString();

            itemCount[nameString] = obj.TryGetPropertyValue("count", out var count) &&
                count?.GetValueKind() is JsonValueKind.Number
                    ? count.GetValue<int>()
                    : 1;

            if (Categories(obj) is { } categories)
                foreach (var c in categories)
                    if (c?.GetValueKind() is JsonValueKind.String && c.ToString() is var categoryString)
                        (CollectionsMarshal.GetValueRefOrAddDefault(itemToCategories, nameString, out _) ??=
                            new(FrozenSortedDictionary.Comparer)).Add(categoryString);

            if (!obj.TryGetPropertyValue("value", out var value) || value is not JsonObject phantomItems)
                continue;

            foreach (var (phantomItem, countNode) in phantomItems)
                if (countNode?.GetValueKind() is JsonValueKind.Number &&
                    countNode.GetValue<int>() is var c)
                    (CollectionsMarshal.GetValueRefOrAddDefault(itemValues, phantomItem, out _) ??=
                        ImmutableArray.CreateBuilder<(string PhantomItem, int Count)>()).Add((phantomItem, c));
        }

        // ReSharper disable once InvertIf
        if (Extract<JsonObject>(zip, "/data/game.json") is { } game &&
            game.TryGetPropertyValue("filler_item_name", out var fillerItemName) &&
            fillerItemName?.GetValueKind() is JsonValueKind.String &&
            fillerItemName.ToString() is var filler)
        {
            itemToCategories[filler] = [Uncategorized];
            itemCount[filler] = 1;
        }

        return (FrozenSortedDictionary.From(itemToCategories),
            itemCount.ToFrozenDictionary(x => x.Key, x => x.Value, FrozenSortedDictionary.Comparer),
            itemValues.ToFrozenDictionary(x => x.Key, x => x.Value.DrainToImmutable(), FrozenSortedDictionary.Comparer));
    }

    /// <summary>Attempts to find the entry to deserialize.</summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="zip">The zip archive.</param>
    /// <param name="file">The file to find.</param>
    /// <returns>The <typeparamref name="T"/>, or <see langword="null"/> if the entry could not be found.</returns>
    static T? Extract<T>(ZipArchive zip, string file)
        where T : JsonNode
    {
        if (zip.Entries.FirstOrDefault(x => x.FullName.EndsWith(file)) is not { } entry)
            return null;

        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream);
    }
}
