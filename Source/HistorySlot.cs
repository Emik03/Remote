// SPDX-License-Identifier: MPL-2.0
namespace Remote;
#pragma warning disable MA0016
/// <summary>Holds the previous connection info.</summary>
public sealed class HistorySlot
{
    /// <summary>Gets or sets the value determining whether <c>DeathLink</c> is enabled on this slot.</summary>
    public bool HasDeathLink { get; set; }

    /// <summary>Gets or sets the color for this slot's tab or window.</summary>
    [NotNull]
    public string? Color
    {
        get => field ??= "";
        set;
    }

    /// <summary>Gets or sets the game of this slot.</summary>
    [NotNull]
    public string? Game
    {
        get => field ??= "";
        set;
    }

    /// <summary>Gets or sets the amount of times items were used.</summary>
    [NotNull]
    public SortedDictionary<string, int>? Items
    {
        get => field ??= new(FrozenSortedDictionary.Comparer);
        init;
    }

    /// <summary>
    /// Gets or sets the locations that were checked and won't be retrievable by the server, requiring us to store it.
    /// </summary>
    [NotNull]
    public SortedSet<string>? CheckedLocations
    {
        get => field ??= new(FrozenSortedDictionary.Comparer);
        init;
    }

    /// <summary>Gets or sets the locations that were tagged.</summary>
    [NotNull]
    public SortedSet<string>? TaggedLocations
    {
        get => field ??= new(FrozenSortedDictionary.Comparer);
        init;
    }
}
