// SPDX-License-Identifier: MPL-2.0
namespace Remote;

using JsonSerializer = System.Text.Json.JsonSerializer;

/// <summary>Handles reading <c>.apworld</c>.</summary>
/// <param name="Game">The game.</param>
/// <param name="Categories">The categories.</param>
/// <param name="Items">The items.</param>
/// <param name="Locations">The locations.</param>
/// <param name="Options">The options.</param>
/// <param name="Regions">The regions.</param>
public sealed record ApWorldReader(
    JsonObject? Game,
    JsonObject? Categories,
    JsonArray? Items,
    JsonArray? Locations,
    JsonObject? Options,
    JsonObject? Regions
)
{
    /// <summary>Contains the goal index.</summary>
    /// <param name="Goal">The index.</param>
    [Serializable] // ReSharper disable once ClassNeverInstantiated.Local
    sealed record GoalData(int Goal);

    /// <summary>The default category name.</summary>
    public const string Uncategorized = "(No Category)";

    /// <summary>Contains the python script that runs <c>Data.py</c>.</summary>
    static readonly string? s_script = Script();

    /// <summary>Initializes a new instance of the <see cref="ApWorldReader"/> class.</summary>
    /// <param name="path">The path to read.</param>
    /// <param name="python">The path to the python binary to execute python.</param>
    /// <param name="ap">The path to the archipelago repository.</param>
    /// <param name="g">The logger.</param>
    public ApWorldReader(string path, string? python = "python", string? ap = null, Action<string>? g = null)
        : this(Read(path, python, ap, g, out var c, out var i, out var l, out var o, out var r), c, i, l, o, r) { }

    /// <summary>Extracts all categories.</summary>
    /// <param name="logger">The logger.</param>
    /// <returns>The categories.</returns>
    public (FrozenSet<string>, FrozenSortedDictionary) ExtractCategories(Action<string>? logger)
    {
        logger?.Invoke("Creating fast lookup tables for categories...");

        return Categories is not null
            ? (Categories.Where(IsHiddenTrue).Select(x => x.Key).ToFrozenSet(FrozenSortedDictionary.Comparer),
                FrozenSortedDictionary.From(
                    Categories.Select(GetOptions).ToDictionary(FrozenSortedDictionary.Comparer)
                ))
            : (FrozenSet<string>.Empty, FrozenSortedDictionary.Empty);
    }

    /// <summary>Extracts all locations.</summary>
    /// <param name="wrapper">The wrapper to get the goal data.</param>
    /// <param name="yaml">The yaml options.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The locations.</returns>
    [CLSCompliant(false)]
    public (FrozenDictionary<string, Logic>, FrozenSortedDictionary)
        ExtractLocations(IDataStorageWrapper wrapper, Yaml yaml, Action<string>? logger)
    {
        if (Locations is null)
            return default;

        if (GetGoal(wrapper) is { } goal)
            yaml.Goal = goal;

        Dictionary<string, Logic> locationsToLogic = new(FrozenSortedDictionary.Comparer);
        Dictionary<string, HashSet<string>> categoriesToLogic = new(FrozenSortedDictionary.Comparer);

        foreach (var location in Locations)
        {
            if (location is not JsonObject o || o["name"]?.ToString() is not { } name)
                continue;

            logger?.Invoke($"Processing location logic: {name}");

            if (o.TryGetPropertyValue("requires", out var requires) &&
                requires?.GetValueKind() is JsonValueKind.String &&
                Logic.TokenizeAndParse(requires.ToString()) is { } logic)
                locationsToLogic[name] = logic;

            if (Regions is not null &&
                o.TryGetPropertyValue("region", out var region) &&
                region?.GetValueKind() is JsonValueKind.String)
                CollectionsMarshal.GetValueRefOrAddDefault(locationsToLogic, name, out _) &=
                    Find(region.ToString(), Regions, new(FrozenSortedDictionary.Comparer), logger);

            if (o.TryGetPropertyValue("hidden", out var hidden) && hidden?.GetValueKind() is JsonValueKind.True)
                continue;

            if (GetCategory(o) is not { } categories)
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
    /// <returns>The items.</returns>
    public (FrozenSortedDictionary, FrozenDictionary<string, int>,
        FrozenDictionary<string, ImmutableArray<(string PhantomItem, int Count)>>) ExtractItems(Action<string>? logger)
    {
        if (Items is null)
        {
            logger?.Invoke("Unable to create fast lookups for items! Exiting...");
            return default;
        }

        logger?.Invoke("Allocating temporary dictionaries for items...");
        Dictionary<string, HashSet<string>> itemToCategories = new(FrozenSortedDictionary.Comparer);
        Dictionary<string, int> itemCount = new(FrozenSortedDictionary.Comparer);

        Dictionary<string, ImmutableArray<(string PhantomItem, int Count)>.Builder> itemValues =
            new(FrozenSortedDictionary.Comparer);

        foreach (var item in Items)
        {
            if (item is not JsonObject obj ||
                !obj.TryGetPropertyValue("name", out var name) ||
                name?.GetValueKind() is not JsonValueKind.String)
                continue;

            logger?.Invoke($"Processing item: {name}");
            var nameString = name.ToString();

            itemCount[nameString] = obj.TryGetPropertyValue("count", out var count) &&
                count?.GetValueKind() is JsonValueKind.Number
                    ? count.GetValue<int>()
                    : 1;

            if (GetCategory(obj) is { } categories)
                foreach (var c in categories)
                    if (c?.GetValueKind() is JsonValueKind.String && c.ToString() is var categoryString)
                        (CollectionsMarshal.GetValueRefOrAddDefault(itemToCategories, nameString, out _) ??=
                            new(FrozenSortedDictionary.Comparer)).Add(categoryString);

            if (!obj.TryGetPropertyValue("value", out var value) || value is not JsonObject phantomItems)
                continue;

            foreach (var (phantomItem, countNode) in phantomItems)
                if (countNode?.GetValueKind() is JsonValueKind.Number &&
                    countNode.GetValue<int>() is var c)
                    (CollectionsMarshal.GetValueRefOrAddDefault(itemValues, nameString, out _) ??=
                        ImmutableArray.CreateBuilder<(string PhantomItem, int Count)>()).Add((phantomItem, c));
        }

        // ReSharper disable once InvertIf
        if (Game?.TryGetPropertyValue("filler_item_name", out var fillerItemName) is true &&
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

    /// <summary>Whether it contains a <c>hidden</c> property that is true.</summary>
    /// <param name="kvp">The pair to check.</param>
    /// <returns>Whether to include this in <see cref="Evaluator.HiddenCategories"/>.</returns>
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

    /// <summary>Gets the goal location, if possible.</summary>
    /// <param name="wrapper">The slot data.</param>
    /// <returns>The goal location, or <see langword="null"/> if it cannot be determined.</returns>
    string? GetGoal(IDataStorageWrapper wrapper)
    {
        static bool IsVictory(JsonNode? x) =>
            x is JsonObject obj &&
            obj.TryGetPropertyValue("victory", out var node) &&
            node?.GetValueKind() is JsonValueKind.True;

        if (Locations is null)
            return null;

        IReadOnlyList<string?> victories = [..Locations.Where(IsVictory).Select(x => x?["name"]?.ToString())];

        if (!Go(x => x.GetSlotData<GoalData>(), wrapper, out _, out var ok) &&
            ok is { Goal: var index } &&
            (uint)index < (uint)victories.Count)
            return victories[index];

        return victories is [var single] ? single : null;
    }

    /// <summary>Gets the python script that runs <c>Data.py</c>.</summary>
    /// <returns>The python script that runs <c>Data.py</c>.</returns>
    static string? Script()
    {
        using var stream = typeof(AssemblyMarker).Assembly.GetManifestResourceStream(
            $"{nameof(Remote)}.{nameof(Resources)}.Resources.Values.Extractor.py"
        );

        if (stream is null)
            return null;

        using StreamReader sr = new(stream);
        return sr.ReadToEnd();
    }

    /// <summary>Attempts to get the <c>category</c> array field.</summary>
    /// <param name="obj">The node to extract.</param>
    /// <returns>The <c>category</c> array, if it exists.</returns>
    static JsonArray? GetCategory(JsonObject obj) =>
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
    static Logic? Find(string target, JsonObject regions, Dictionary<string, Logic> regionToLogic, Action<string>? g)
    {
        Logic? path = null;

        foreach (var (name, value) in regions)
        {
            if (!IsStarting(value))
                continue;

            g?.Invoke($"Constructing region logic! Processing if {target} is accessible from {name}...");

            var visited = regions
               .Where(x => !FrozenSortedDictionary.Equals(x.Key, name) && IsStarting(x.Value))
               .Select(x => x.Key)
               .ToSet(FrozenSortedDictionary.Comparer);

            var foundTarget = false;

            var branch = Find(name, target, regions, regionToLogic, visited, ref foundTarget);

            if (foundTarget)
                path = path is null ? branch : path | branch;
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

        if (logic is null && obj.TryGetPropertyValue("requires", out var requires))
            logic = TokenizeAndParse(requires);

        if (FrozenSortedDictionary.Equals(current, target))
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

                var innerLogic =
                    Find(connectionString, target, regions, regionToLogic, visited.ToSet(), ref innerFoundTarget);

                if (!innerFoundTarget)
                    continue;

                foundTarget = true;
                var and = innerLogic & TokenizeAndParse(exitRequires?[connectionString]);
                path = path is null ? and : path | and;
            }

        return foundTarget ? logic & path : null;
    }

    /// <summary>Tokenizes and parses the <see cref="JsonNode"/>.</summary>
    /// <param name="requires">The requires string to parse.</param>
    /// <returns>The <see cref="Logic"/> of the parameter <paramref name="requires"/>.</returns>
    static Logic? TokenizeAndParse(JsonNode? requires) =>
        requires?.GetValueKind() is JsonValueKind.String ? Logic.TokenizeAndParse(requires.ToString()) : null;

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

    /// <summary>Gets the categories alongside its yaml options.</summary>
    /// <param name="kvp">The pair to deconstruct.</param>
    /// <returns>The category with its yaml options.</returns>
    static (string, IReadOnlySet<string>) GetOptions(KeyValuePair<string, JsonNode?> kvp) =>
        kvp.Value is JsonObject obj &&
        obj.TryGetPropertyValue("yaml_option", out var yamlOption) &&
        yamlOption is JsonArray array
            ? (kvp.Key, JsonArrayToHashSet(array))
            : (kvp.Key, FrozenSet<string>.Empty);

    /// <summary>Fail case. Sets all parameters to <see langword="null"/> and returns <see langword="null"/>.</summary>
    /// <param name="categories">The categories that will be set to <see langword="null"/>.</param>
    /// <param name="options">The options that will be set to <see langword="null"/>.</param>
    /// <param name="regions">The regions that will be set to <see langword="null"/>.</param>
    /// <returns>The value <see langword="null"/>.</returns>
    static JsonObject? Fail(out JsonObject? categories, out JsonObject? options, out JsonObject? regions)
    {
        (categories, options, regions) = (null, null, null);
        return null;
    }

    /// <summary>Attempts to get the world data from executing python.</summary>
    /// <param name="path">The path to the <c>.apworld</c>.</param>
    /// <param name="python">The path to the python binary to execute python.</param>
    /// <param name="ap">The path to the archipelago repository.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The world data, or <see langword="null"/> if it was unable to execute the script.</returns>
    static JsonObject? GetWorldDataFromPython(string path, string? python, string? ap, Action<string>? logger)
    {
        if (string.IsNullOrWhiteSpace(python) || string.IsNullOrWhiteSpace(ap))
        {
            logger?.Invoke("The .apworld uses Manual Hooks but the Archipelago repository does not exist! Exiting...");
            return null;
        }

        logger?.Invoke("Starting python...");

        using var process = Process.Start(
            new ProcessStartInfo(python)
            {
                CreateNoWindow = true,
                Environment = { ["APWORLD_PATH"] = path, ["ARCHIPELAGO_REPO_PATH"] = ap },
                ErrorDialog = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            }
        );

        if (process is null)
        {
            logger?.Invoke("Unable to start process! Exiting...");
            return null;
        }

        logger?.Invoke("Writing to standard input...");
        process.StandardInput.Write(s_script);
        logger?.Invoke("Closing the standard input...");
        process.StandardInput.Close();
        logger?.Invoke("Waiting for python...");
        _ = process.WaitForExit(30000);
        logger?.Invoke("Deserializing the output from python...");

        return JsonSerializer.Deserialize<JsonNode>(
            process.StandardOutput.ReadToEnd(),
            RemoteJsonSerializerContext.Default.JsonNode
        ) as JsonObject;
    }

    /// <summary>Reads the <c>.apworld</c>.</summary>
    /// <param name="path"></param>
    /// <param name="python">The path to the python binary to execute python.</param>
    /// <param name="ap">The path to the archipelago repository.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="categories">The categories.</param>
    /// <param name="items">The items.</param>
    /// <param name="locations">The locations.</param>
    /// <param name="options">The options.</param>
    /// <param name="regions">The regions.</param>
    /// <returns>The game.</returns>
    static JsonObject? Read(
        string path,
        string? python,
        string? ap,
        Action<string>? logger,
        out JsonObject? categories,
        out JsonArray? items,
        out JsonArray? locations,
        out JsonObject? options,
        out JsonObject? regions
    )
    {
        logger?.Invoke("Opening the zip file...");
        using ZipArchive zip = new(File.OpenRead(path));
        items = Extract<JsonArray>(zip, "/data/items.json", logger);
        locations = Extract<JsonArray>(zip, "/data/locations.json", logger);

        if (items?.Count > 0 || locations?.Count > 0)
        {
            categories = Extract<JsonObject>(zip, "/data/categories.json", logger);
            options = Extract<JsonObject>(zip, "/data/options.json", logger);
            regions = Extract<JsonObject>(zip, "/data/regions.json", logger);
            return Extract<JsonObject>(zip, "/data/game.json", logger);
        }

        if (GetWorldDataFromPython(path, python, ap, logger) is not { } obj)
            return Fail(out categories, out options, out regions);

        logger?.Invoke("Extracting values from the json object...");
        items = Index<JsonArray>(obj, "items.json");
        locations = Index<JsonArray>(obj, "locations.json");
        categories = Index<JsonObject>(obj, "categories.json");
        options = Index<JsonObject>(obj, "options.json");
        regions = Index<JsonObject>(obj, "regions.json");
        return Index<JsonObject>(obj, "game.json");
    }

    /// <summary>Attempts to find the entry to deserialize.</summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="zip">The zip archive.</param>
    /// <param name="file">The file to find.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The <typeparamref name="T"/>, or <see langword="null"/> if the entry could not be found.</returns>
    static T? Extract<T>(ZipArchive zip, string file, Action<string>? logger)
        where T : JsonNode
    {
        logger?.Invoke($"Locating {file}...");

        if (zip.Entries.FirstOrDefault(x => x.FullName.EndsWith(file)) is not { } entry)
            return null;

        logger?.Invoke($"Opening {file}...");
        using var stream = entry.Open();
        logger?.Invoke($"Deserializing {file}...");
        return JsonSerializer.Deserialize<JsonNode>(stream, RemoteJsonSerializerContext.Default.JsonNode) as T;
    }

    /// <summary>Attempts to extract the value from the <see cref="GetWorldDataFromPython"/>.</summary>
    /// <typeparam name="T">The type to extract.</typeparam>
    /// <param name="obj">The object to extract.</param>
    /// <param name="name">The index.</param>
    /// <returns>
    /// The value from the parameter <paramref name="obj"/> when indexing with the parameter
    /// <paramref name="name"/> casted as <typeparamref name="T"/>, or <see langword="null"/>.
    /// </returns>
    static T? Index<T>(JsonObject obj, string name)
        where T : JsonNode =>
        obj.TryGetPropertyValue(name, out var node) ? node as T : null;
}
