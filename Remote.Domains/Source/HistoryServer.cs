// SPDX-License-Identifier: MPL-2.0
namespace Remote.Domains;

/// <summary>Holds the previous connection infos.</summary>
public sealed class HistoryServer
{
    /// <summary>Contains the orderings for history.</summary>
    public enum Order
    {
        /// <summary>This value indicates to sort the history by date, newest to oldest.</summary>
        Date,

        /// <summary>This value indicates to sort the history alphabetically.</summary>
        Name,
    }

    /// <summary>The default host address that hosts Archipelago games.</summary>
    const string HistoryFile = "history.json";

    /// <summary>Contains the path to the preferences file to read and write from.</summary>
    public static string FilePath { get; } = PathTo(HistoryFile, "REMOTE_HISTORY_PATH");

    /// <summary>Gets or sets the display name of this server.</summary>
    [NotNull]
    public string? Alias
    {
        get => field ??= "";
        set;
    }

    /// <summary>Gets or sets teh password of the game.</summary>
    [NotNull]
    public string? Password
    {
        get => field ??= "";
        set;
    }

    /// <summary>Gets or sets the slots for this server.</summary>
    [NotNull]
    public OrderedDictionary<string, HistorySlot>? Slots
    {
        get => field ??= new(FrozenSortedDictionary.Comparer);
        init;
    }

    /// <summary>Writes this instance to disk.</summary>
    /// <param name="servers">The servers to save.</param>
    public static void Save(OrderedDictionary<string, HistoryServer> servers) =>
        _ = Go(
            x => File.WriteAllText(
                FilePath,
                JsonSerializer.Serialize(x, RemoteJsonSerializerContext.Default.OrderedDictionaryStringHistoryServer)
            ),
            servers,
            out _
        );

    /// <summary>Gets the full path to the file.</summary>
    /// <param name="file">The file path to get.</param>
    /// <param name="environment">The environment variable that allows users to override the return.</param>
    /// <returns>The full path to the parameter <paramref name="file"/>.</returns>
    public static string PathTo(string file, string environment) =>
        Environment.GetEnvironmentVariable(environment) is { } variable
            ? Directory.Exists(variable) ? Path.Join(variable, file) : variable
            : Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nameof(Remote), file);

    /// <summary>Orders the connections.</summary>
    /// <param name="history">The value to sort.</param>
    /// <param name="method">The method of sorting.</param>
    /// <returns>The ordered enumerable of the parameter <paramref name="history"/>.</returns>
    public static IEnumerable<KeyValuePair<string, HistoryServer>> OrderBy(
        OrderedDictionary<string, HistoryServer> history,
        Order method
    ) =>
        method switch
        {
            Order.Date => history.Reverse(),
            Order.Name => history.OrderBy(
                x => string.IsNullOrWhiteSpace(x.Value.Alias) ? x.Key : x.Value.Alias,
                FrozenSortedDictionary.Comparer
            ),
            _ => [],
        };

    /// <summary>Loads the history from disk.</summary>
    /// <returns>The preferences.</returns>
    public static OrderedDictionary<string, HistoryServer> Load()
    {
        if (File.Exists(FilePath) && !Go(Deserialize, out _, out var disk) && disk is not null)
            return new(disk, FrozenSortedDictionary.Comparer);

        if (Path.GetDirectoryName(FilePath) is { } directory)
            Directory.CreateDirectory(directory);

        OrderedDictionary<string, HistoryServer> memory = new([], FrozenSortedDictionary.Comparer);
        Save(memory);
        return memory;
    }

    /// <summary>Orders the connections.</summary>
    /// <param name="method">The connections.</param>
    /// <returns>The ordered enumerable of the parameter <paramref name="method"/>.</returns>
    public IEnumerable<KeyValuePair<string, HistorySlot>> OrderBy(Order method) =>
        method switch
        {
            Order.Date => Slots.Reverse(),
            Order.Name => Slots.OrderBy(x => x.Key, FrozenSortedDictionary.Comparer),
            _ => [],
        };

    /// <summary>Deserializes the dictionary from disk.</summary>
    /// <returns>The dictionary from disk.</returns>
    static OrderedDictionary<string, HistoryServer>? Deserialize() =>
        JsonSerializer.Deserialize<OrderedDictionary<string, HistoryServer>>(
            File.OpenRead(FilePath),
            RemoteJsonSerializerContext.Default.OrderedDictionaryStringHistoryServer
        );
}
