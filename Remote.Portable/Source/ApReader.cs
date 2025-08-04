// SPDX-License-Identifier: MPL-2.0
namespace Remote.Portable;

/// <summary>Handles reading <c>.apworld</c>.</summary>
/// <param name="Game">The game.</param>
/// <param name="Categories">The categories.</param>
/// <param name="Items">The items.</param>
/// <param name="Locations">The locations.</param>
/// <param name="Options">The options.</param>
/// <param name="Regions">The regions.</param>
public sealed record ApReader(
    JsonObject? Game,
    JsonObject? Categories,
    JsonArray? Items,
    JsonArray? Locations,
    JsonObject? Options,
    JsonObject? Regions
) : IEqualityOperators<ApReader, ApReader, bool>
{
    /// <summary>The default category name.</summary>
    public const string Uncategorized = "(No Category)";

    /// <summary>Contains the python script that runs <c>Data.py</c>.</summary>
    static readonly string? s_script = Script();

    /// <summary>Initializes a new instance of the <see cref="ApReader"/> class.</summary>
    /// <param name="path">The path to read.</param>
    /// <param name="ap">The path to the archipelago repository.</param>
    /// <param name="python">The path to the python binary to execute python.</param>
    /// <param name="logger">The logger.</param>
    public ApReader(string path, string? ap = null, string? python = "python", Action<string>? logger = null)
        : this(Read(path, ap, python, logger, out var c, out var i, out var l, out var o, out var r), c, i, l, o, r) { }

    /// <summary>Contains the SHA512 hash of the unmodified <c>Data.py</c>.</summary>
    static ReadOnlySpan<byte> Hash =>
    [
        208, 117, 59, 232, 186, 95, 105, 76, 159, 101, 237, 184, 62, 111, 15, 130, 61, 131, 37, 110, 4, 3,
        189, 140, 185, 222, 175, 11, 140, 162, 189, 167, 62, 71, 25, 134, 137, 116, 44, 236, 243, 168, 59,
        55, 197, 193, 149, 216, 107, 234, 24, 94, 114, 206, 30, 123, 71, 207, 37, 84, 94, 94, 38, 193,
    ];

    /// <summary>Attempts to find the <c>.apworld</c>.</summary>
    /// <param name="game">The game to search for.</param>
    /// <param name="directory">The directory to search in.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The path to the <c>.apworld</c>, or <see langword="null"/> if none found.</returns>
    public static string? Find(string game, string directory, Action<string>? logger = null)
    {
        var world = $"{game}.apworld";

        static bool ReadZip(string x, string game, Action<string>? logger)
        {
            using ZipArchive zip = new(File.OpenRead(x), ZipArchiveMode.Read);

            if (Path.GetFileName(x.AsSpan()) is var fileName &&
                Extract<JsonObject>(zip, "/data/game.json", logger) is not { } obj)
            {
                logger?.Invoke($"{fileName} does not have a game.json.");
                return false;
            }

            if (!obj.TryGetPropertyValue("game", out var g) || g?.GetValueKind() is not JsonValueKind.String)
            {
                logger?.Invoke($"{fileName} does not have a string on the 'game' field.");
                return false;
            }

            if (!obj.TryGetPropertyValue("creator", out var p) && !obj.TryGetPropertyValue("player", out p) ||
                p?.GetValueKind() is not JsonValueKind.String)
            {
                logger?.Invoke($"{fileName} does not have a string on the 'player' field.");
                return false;
            }

            if ($"Manual_{g}_{p}" is var gameName && FrozenSortedDictionary.Comparer.Equals(game, gameName))
                return true;

            logger?.Invoke($"{game} is not {gameName}.");
            return false;
        }

        bool ByFileName(string x) => world.Equals(Path.GetFileName(x.AsSpan()), StringComparison.OrdinalIgnoreCase);

        bool ByGameJson(string x)
        {
            if (!Go(ReadZip, x, game, logger, out _, out var result))
                return result;

            logger?.Invoke($"Unable to read {Path.GetFileName(x.AsSpan())}.");
            return false;
        }

        string? First(string root, string? relative, Func<string, bool> f)
        {
            logger?.Invoke($"Locating {game} within {relative ?? "root"} by {f.Method.Name}...");

            return Path.Join(root, relative) is var d && Directory.Exists(d)
                ? Directory.EnumerateFiles(d).Where(x => Path.GetExtension(x.AsSpan()) is ".apworld").FirstOrDefault(f)
                : null;
        }

        return First(directory, "worlds", ByFileName) ??
            First(directory, "custom_worlds", ByFileName) ??
            First(directory, null, ByFileName) ??
            First(directory, "worlds", ByGameJson) ??
            First(directory, "custom_worlds", ByGameJson) ??
            First(directory, null, ByGameJson);
    }

    /// <summary>Extracts all categories.</summary>
    /// <param name="yaml">The yaml options.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The categories.</returns>
    public (FrozenSet<string>, FrozenSet<string>, FrozenSortedDictionary)
        ExtractCategories(ApYaml yaml, Action<string>? logger)
    {
        logger?.Invoke("Creating fast lookup tables for categories...");

        // ReSharper disable once InlineTemporaryVariable
        return Categories is { } c
            ? (c.Omit(yaml.IsEnabled).Select(x => x.Key).ToFrozenSet(FrozenSortedDictionary.Comparer),
                c.Where(IsHiddenTrue).Select(x => x.Key).ToFrozenSet(FrozenSortedDictionary.Comparer),
                FrozenSortedDictionary.From(c.Select(GetOptions).ToDictionary(FrozenSortedDictionary.Comparer)))
            : (FrozenSet<string>.Empty, FrozenSet<string>.Empty, FrozenSortedDictionary.Empty);
    }

    /// <summary>Extracts all locations.</summary>
    /// <param name="yaml">The yaml options.</param>
    /// <param name="goalGetter">The wrapper to get the goal data.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The locations.</returns>
    [CLSCompliant(false)]
    public (FrozenDictionary<string, ApLogic>, FrozenSortedDictionary)
        ExtractLocations(ApYaml yaml, Func<GoalData?>? goalGetter, Action<string>? logger)
    {
        if (Locations is null)
            return default;

        if (GetGoal(goalGetter) is { } goal)
            yaml.Goal = goal;

        Dictionary<string, ApLogic> locationsToLogic = new(FrozenSortedDictionary.Comparer);
        Dictionary<string, HashSet<string>> categoriesToLogic = new(FrozenSortedDictionary.Comparer);

        foreach (var location in Locations)
        {
            if (location is not JsonObject o || o["name"]?.ToString() is not { } name)
                continue;

            logger?.Invoke($"Processing location logic: {name}");

            if (o.TryGetPropertyValue("requires", out var requires) &&
                requires?.GetValueKind() is JsonValueKind.String &&
                ApLogic.TokenizeAndParse(requires.ToString()) is { } logic)
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

            itemCount[nameString] = Count(obj);

            if (GetCategory(obj) is { } categories)
                foreach (var c in categories)
                    if (c?.GetValueKind() is JsonValueKind.String && c.ToString() is var categoryString)
                        (CollectionsMarshal.GetValueRefOrAddDefault(itemToCategories, nameString, out _) ??=
                            new(FrozenSortedDictionary.Comparer)).Add(categoryString);

            if (!obj.TryGetPropertyValue("value", out var value) || value is not JsonObject phantomItems)
                continue;

            foreach (var (phantomItem, countNode) in phantomItems)
                if (countNode?.GetValueKind() is JsonValueKind.Number &&
                    (int)countNode.GetValue<double>() is var c)
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
    /// <returns>Whether to include this in <see cref="ApEvaluator.HiddenCategories"/>.</returns>
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

    /// <summary>Gets the count.</summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    static int Count(JsonObject obj) =>
        obj.TryGetPropertyValue("count", out var count) && count is not null
            ? count.GetValueKind() switch
            {
                JsonValueKind.String when int.TryParse(count.GetValue<string>(), out var i) => i,
                JsonValueKind.Number => (int)count.GetValue<double>(),
                _ => 1,
            }
            : 1;

    /// <summary>Gets the python script that runs <c>Data.py</c>.</summary>
    /// <returns>The python script that runs <c>Data.py</c>.</returns>
    static string? Script()
    {
        const string Extractor = $"{nameof(Remote)}.{nameof(Portable)}.Resources.Values.Extractor.py";
        using var stream = typeof(ApReader).Assembly.GetManifestResourceStream(Extractor);

        if (stream is null)
            return null;

        using StreamReader sr = new(stream);
        return sr.ReadToEnd();
    }

    /// <summary>Attempts to get the <c>category</c> array field.</summary>
    /// <param name="obj">The node to extract.</param>
    /// <returns>The <c>category</c> array, if it exists.</returns>
    static JsonArray GetCategory(JsonObject obj) =>
        obj.TryGetPropertyValue("category", out var categories)
            ? categories as JsonArray ??
            (categories?.GetValueKind() is JsonValueKind.String ? new(categories.ToString()) : new(Uncategorized))
            : new(Uncategorized);

    /// <summary>Constructs the logic based on getting to a specific region from any starting region.</summary>
    /// <remarks><para>NOTE: Currently does not consider <c>entrance_requires</c>.</para></remarks>
    /// <param name="target">The region to find.</param>
    /// <param name="regions">The root of the <c>regions.json</c> node.</param>
    /// <param name="regionToLogic">
    /// Maps the region name to the already parsed <see cref="ApLogic"/> for caching purposes.
    /// </param>
    /// <param name="g">The logger.</param>
    /// <returns>The resulting <see cref="ApLogic"/> to get to the parameter <paramref name="target"/>.</returns>
    static ApLogic? Find(string target, JsonObject regions, Dictionary<string, ApLogic> regionToLogic, Action<string>? g)
    {
        ApLogic? path = null;

        foreach (var (name, value) in regions)
        {
            if (!IsStarting(value))
                continue;

            g?.Invoke($"Constructing region logic! Processing if {target} is accessible from {name}...");

            var visited = regions
               .Where(x => !FrozenSortedDictionary.Comparer.Equals(x.Key, name) && IsStarting(x.Value))
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
    /// Maps the region name to the already parsed <see cref="ApLogic"/> for caching purposes.
    /// </param>
    /// <param name="visited">The regions that have already been visited, so as to remove unnecessary branching.</param>
    /// <param name="foundTarget">Whether the target has been found successfully.</param>
    /// <returns>The resulting <see cref="ApLogic"/> to get to the parameter <paramref name="target"/>.</returns>
    static ApLogic? Find(
        string current,
        string target,
        JsonNode regions,
        Dictionary<string, ApLogic> regionToLogic, // ReSharper disable once SuggestBaseTypeForParameter
        HashSet<string> visited,
        ref bool foundTarget
    )
    {
        if (regions[current] is not JsonObject obj || !visited.Add(current))
            return null;

        ref var logic = ref CollectionsMarshal.GetValueRefOrAddDefault(regionToLogic, current, out _);

        if (logic is null && obj.TryGetPropertyValue("requires", out var requires))
            logic = TokenizeAndParse(requires);

        if (FrozenSortedDictionary.Comparer.Equals(current, target))
        {
            foundTarget = true;
            return logic;
        }

        if (!obj.TryGetPropertyValue("connects_to", out var connectsTo) || connectsTo is not JsonArray array)
            return null;

        ApLogic? path = null;
        var exitRequires = obj.TryGetPropertyValue("exit_requires", out var ex) && ex is JsonObject ox ? ox : null;

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

                var entranceRequires =
                    regions[target] is JsonObject rt &&
                    rt.TryGetPropertyValue("entrance_requires", out var en) &&
                    en is JsonObject on
                        ? on
                        : null;

                var and = innerLogic &
                    TokenizeAndParse(exitRequires?[connectionString]) &
                    TokenizeAndParse(entranceRequires?[connectionString]);

                path = path is null ? and : path | and;
            }

        return foundTarget ? logic & path : null;
    }

    /// <summary>Tokenizes and parses the <see cref="JsonNode"/>.</summary>
    /// <param name="requires">The requires string to parse.</param>
    /// <returns>The <see cref="ApLogic"/> of the parameter <paramref name="requires"/>.</returns>
    static ApLogic? TokenizeAndParse(JsonNode? requires) =>
        requires?.GetValueKind() is JsonValueKind.String ? ApLogic.TokenizeAndParse(requires.ToString()) : null;

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

    /// <summary>Attempts to get the world data from executing python.</summary>
    /// <param name="zip">The zip archive.</param>
    /// <param name="path">The path to the <c>.apworld</c>.</param>
    /// <param name="ap">The path to the archipelago repository.</param>
    /// <param name="python">The path to the python binary to execute python.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The world data, or <see langword="null"/> if it was unable to execute the script.</returns>
    static JsonObject? GetWorldDataFromPython(
        ZipArchive zip,
        string path,
        string? ap,
        string? python,
        Action<string>? logger
    )
    {
        logger?.Invoke("Locating Data.py...");

        if (Extract(zip, "/hooks/Data.py") is not { } data)
            return null;

        logger?.Invoke("Opening Data.py...");
        using var stream = data.Open();
        logger?.Invoke("Computing hash for Data.py...");
        Span<byte> span = stackalloc byte[64];
        SHA512.HashData(stream, span);

        if (span.SequenceEqual(Hash))
        {
            logger?.Invoke("Data.py has been left unmodified, extracting from json files directly...");
            return null;
        }

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
        var str = process.StandardOutput.ReadToEnd();
        logger?.Invoke("Deserializing the output from python...");
        return JsonSerializer.Deserialize<JsonNode>(str, RemoteJsonSerializerContext.Default.JsonNode) as JsonObject;
    }

    /// <summary>Reads the <c>.apworld</c>.</summary>
    /// <param name="path">The path to read.</param>
    /// <param name="ap">The path to the archipelago repository.</param>
    /// <param name="python">The path to the python binary to execute python.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="categories">The categories.</param>
    /// <param name="items">The items.</param>
    /// <param name="locations">The locations.</param>
    /// <param name="options">The options.</param>
    /// <param name="regions">The regions.</param>
    /// <returns>The game.</returns>
    static JsonObject? Read(
        string path,
        string? ap,
        string? python,
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

        if (GetWorldDataFromPython(zip, path, ap, python, logger) is not { } obj)
        {
            items = Extract<JsonArray>(zip, "/data/items.json", logger);
            locations = Extract<JsonArray>(zip, "/data/locations.json", logger);
            categories = Extract<JsonObject>(zip, "/data/categories.json", logger);
            options = Extract<JsonObject>(zip, "/data/options.json", logger);
            regions = Extract<JsonObject>(zip, "/data/regions.json", logger);
            return Extract<JsonObject>(zip, "/data/game.json", logger);
        }

        logger?.Invoke("Extracting values from the json object...");
        items = Index<JsonArray>(obj, "items.json");
        locations = Index<JsonArray>(obj, "locations.json");
        categories = Index<JsonObject>(obj, "categories.json");
        options = Index<JsonObject>(obj, "options.json");
        regions = Index<JsonObject>(obj, "regions.json");
        return Index<JsonObject>(obj, "game.json");
    }

    /// <summary>Finds the <see cref="ZipArchiveEntry"/> that ends with the specified path.</summary>
    /// <param name="zip">The zip archive.</param>
    /// <param name="endsWith">The string to match.</param>
    /// <returns>
    /// The <see cref="ZipArchiveEntry"/> whose path ends with the parameter <paramref name="endsWith"/>.
    /// </returns>
    static ZipArchiveEntry? Extract(ZipArchive zip, string endsWith) =>
        zip.Entries.FirstOrDefault(x => x.FullName.EndsWith(endsWith));

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

        if (Extract(zip, file) is not { } entry)
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

    /// <summary>Gets the goal location, if possible.</summary>
    /// <param name="goalGetter">The goal.</param>
    /// <returns>The goal location, or <see langword="null"/> if it cannot be determined.</returns>
    string? GetGoal(Func<GoalData?>? goalGetter)
    {
        static bool IsVictory(JsonNode? x) =>
            x is JsonObject obj &&
            obj.TryGetPropertyValue("victory", out var node) &&
            node?.GetValueKind() is JsonValueKind.True;

        if (Locations is null)
            return null;

        IReadOnlyList<string?> victories = [..Locations.Where(IsVictory).Select(x => x?["name"]?.ToString())];

        return goalGetter is null ||
            Go(goalGetter, out _, out var ok) ||
            ok is not { Goal: var goal } ||
            (uint)goal >= (uint)victories.Count
                ? victories is [var single] ? single : null
                : victories[goal];
    }
}
