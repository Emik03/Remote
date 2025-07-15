// SPDX-License-Identifier: MPL-2.0
namespace Remote;

using ConnectionGroup = IGrouping<(string? Alias, string? Host, ushort Port), HistoryEntry>;
using JsonSerializer = System.Text.Json.JsonSerializer;

/// <summary>Holds a previous connection info.</summary>
/// <param name="Name">The slot.</param>
/// <param name="Password">The password of the game.</param>
/// <param name="Host">The host.</param>
/// <param name="Port">The port of the host.</param>
/// <param name="Game">The game.</param>
/// <param name="Items">The used items.</param>
/// <param name="Locations">The checked locations.</param>
/// <param name="Tagged">The tagged locations.</param>
/// <param name="Alias">The alias when displaying in history.</param>
/// <param name="Color">The color for the tab or window.</param>
/// <param name="HasDeathLink">Whether death link is enabled.</param>
[CLSCompliant(false), StructLayout(LayoutKind.Auto)]
public readonly record struct HistoryEntry(
    string? Name,
    string? Password,
    string? Host,
    ushort Port,
    string? Game,
    ImmutableDictionary<string, int>? Items,
    ImmutableHashSet<string>? Locations,
    ImmutableHashSet<string>? Tagged,
    string? Alias,
    string? Color,
    bool HasDeathLink
)
{
    /// <summary>Contains the list of connections.</summary>
    /// <param name="History">The history.</param>
    // ReSharper disable MemberHidesStaticFromOuterClass
#pragma warning disable MA0016
    public readonly record struct List(List<HistoryEntry> History)
#pragma warning restore MA0016
    {
        /// <summary>The default host address that hosts Archipelago games.</summary>
        const string HistoryFile = "history.json";

        /// <summary>Contains the path to the preferences file to read and write from.</summary>
        public static string FilePath { get; } = Preferences.PathTo(HistoryFile, "REMOTE_HISTORY_PATH");

        /// <summary>Loads the history from disk.</summary>
        /// <returns>The preferences.</returns>
        public static List Load()
        {
            if (File.Exists(FilePath) && !Go(Deserialize, out _, out var fromDisk) && fromDisk is not null)
                return new(fromDisk);

            var directory = Path.GetDirectoryName(FilePath);
            Debug.Assert(directory is not null);
            Directory.CreateDirectory(directory);
            List fromMemory = new([]);
            fromMemory.Save();
            return fromMemory;
        }

        /// <summary>Writes this instance to disk.</summary>
        public void Save() =>
            File.WriteAllText(
                FilePath,
                JsonSerializer.Serialize(History, RemoteJsonSerializerContext.Default.ListHistoryEntry)
            );

        /// <summary>Finds the index that matches the key of the group.</summary>
        /// <param name="group">The group.</param>
        /// <returns>The index that matches the parameter <paramref name="group"/>.</returns>
        public int Find(ConnectionGroup group) =>
            History.FindIndex(
                x => group.Key.Port == x.Port && FrozenSortedDictionary.Comparer.Equals(group.Key.Host, x.Host)
            );

        static List<HistoryEntry>? Deserialize() =>
            JsonSerializer.Deserialize<List<HistoryEntry>>(
                File.OpenRead(FilePath),
                RemoteJsonSerializerContext.Default.ListHistoryEntry
            );
    }

    /// <summary>Initializes a new instance of the <see cref="HistoryEntry"/> struct.</summary>
    /// <param name="historyEntry">The connection to copy.</param>
    /// <param name="locations">The locations to inherit.</param>
    /// <param name="alias">The alias for the history header.</param>
    /// <param name="color">The color for the tab or window.</param>
    public HistoryEntry(
        HistoryEntry historyEntry,
        IEnumerable<string>? locations,
        string? alias = null,
        string? color = null
    )
        : this(
            historyEntry.Name,
            historyEntry.Password,
            historyEntry.Host,
            historyEntry.Port,
            historyEntry.Game,
            historyEntry.Items,
            historyEntry.GetLocationsOrEmpty().Union(locations ?? []),
            historyEntry.Tagged,
            string.IsNullOrWhiteSpace(historyEntry.Alias) ? alias : historyEntry.Alias,
            color ?? historyEntry.Color,
            historyEntry.HasDeathLink
        ) { }

    /// <summary>Initializes a new instance of the <see cref="HistoryEntry"/> struct.</summary>
    /// <param name="yaml">The yaml to deconstruct.</param>
    /// <param name="password">The password of the game.</param>
    /// <param name="host">The host.</param>
    /// <param name="port">The port of the host.</param>
    /// <param name="alias">The alias for the history header.</param>
    /// <param name="color">The color for the tab or window.</param>
    /// <param name="hasDeathLink">Whether death link is enabled.</param>
    public HistoryEntry(
        Yaml yaml,
        string? password,
        string? host,
        ushort port,
        string? alias,
        string? color,
        bool hasDeathLink
    )
        : this(yaml.Name, password, host, port, yaml.Game, [], [], [], alias, color, hasDeathLink) { }

    /// <summary>Determines whether this instance is invalid, usually from default construction.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsInvalid => Name is null || Host is null || Port is 0 || Game is null;

    /// <inheritdoc />
    public bool Equals(HistoryEntry other) =>
        HostEquals(other) && FrozenSortedDictionary.Comparer.Equals(Name, other.Name);

    /// <summary>Determines whether both hosts are equal.</summary>
    /// <param name="other">The instance to compare.</param>
    /// <returns>Whether both hosts are equal.</returns>
    public bool HostEquals(HistoryEntry other) =>
        Port == other.Port &&
        FrozenSortedDictionary.Comparer.Equals(Host, other.Host);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Port, Name, Game, Host, Password ?? "");

    /// <summary>Gets the alias.</summary>
    /// <returns>The alias.</returns>
    public string GetAliasOrEmpty() => Alias ?? "";

    /// <summary>Gets the items.</summary>
    /// <returns>The items.</returns>
    public ImmutableDictionary<string, int> GetItemsOrEmpty() => Items ?? ImmutableDictionary<string, int>.Empty;

    /// <summary>Gets the locations.</summary>
    /// <returns>The locations.</returns>
    public ImmutableHashSet<string> GetLocationsOrEmpty() => Locations ?? ImmutableHashSet<string>.Empty;

    /// <summary>Gets the tagged locations.</summary>
    /// <returns>The tagged locations.</returns>
    public ImmutableHashSet<string> GetTaggedOrEmpty() => Tagged ?? ImmutableHashSet<string>.Empty;

    /// <summary>Converts this instance to the equivalent <see cref="Yaml"/> instance.</summary>
    /// <returns>The <see cref="Yaml"/> instance, or <see langword="null"/> if none found on disk.</returns>
    public Yaml ToYaml() =>
        new()
        {
            Game = Game ?? "",
            Name = Name ?? "",
        };
}
