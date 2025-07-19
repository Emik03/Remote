// SPDX-License-Identifier: MPL-2.0
namespace Remote.Domains;

/// <summary>Contains the main colors the application uses.</summary>
public enum RemotePalette
{
    /// <summary>The background color.</summary>
    Background,

    /// <summary>The neutral text color.</summary>
    Neutral,

    /// <summary>The text color to indicate that the item is a trap.</summary>
    Trap,

    /// <summary>The text color to indicate that the item is useful.</summary>
    Useful,

    /// <summary>The text color to indicate that the item is progression.</summary>
    Progression,

    /// <summary>The text color for an item that hasn't been obtained yet.</summary>
    PendingItem,

    /// <summary>The text color for a location that is reachable.</summary>
    Reachable,

    /// <summary>The text color for a location that has already been checked.</summary>
    Checked,

    /// <summary>The text color for a location that is out-of-logic.</summary>
    OutOfLogic,

    /// <summary>The text color for when a location that is out-of-logic is attempted to be released.</summary>
    ReleasingOutOfLogic,

    /// <summary>The text color for when a location is being released.</summary>
    Releasing,

    /// <summary>The text color for when a location has been released.</summary>
    Released,

    /// <summary>The text color to indicate that the slot cannot make any progress without breaking logic.</summary>
    BK,

    /// <summary>The number of items in this enumeration.</summary>
    Count,
}
