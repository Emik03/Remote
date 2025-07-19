// SPDX-License-Identifier: MPL-2.0
namespace Remote;

using Color = System.Drawing.Color;

/// <inheritdoc cref="Preferences"/>
sealed partial class Preferences
{
    /// <summary>Contains the default set of colors.</summary>
    static readonly ImmutableArray<RemoteColor> s_defaultColors =
    [
        // AppPalette
        new(Color.Black), // Background
        RemoteColor.Foreground, // Neutral
        new(Color.Salmon), // Trap
        new(Color.SlateBlue), // Useful
        new(Color.Plum), // Progression
        RemoteColor.Yellow, // PendingItem
        RemoteColor.Green, // Reachable
        RemoteColor.Comment, // Checked
        RemoteColor.Red, // OutOfLogic
        new(Color.Red), // ReleasingOutOfLogic
        RemoteColor.Orange, // Releasing
        RemoteColor.Green, // Released
        RemoteColor.Orange, // BK
        // ImGuiCol
        RemoteColor.Foreground, // Text
        new(RemoteColor.Foreground.Vector with { W = 0.75f }), // TextDisabled
        RemoteColor.Black, // WindowBg
        RemoteColor.Black, // ChildBg
        RemoteColor.Black, // PopupBg
        RemoteColor.Comment, // Border
        RemoteColor.Black, // BorderShadow
        RemoteColor.Background, // FrameBg
        RemoteColor.Comment, // FrameBgHovered
        RemoteColor.Comment, // FrameBgActive
        RemoteColor.CurrentLine, // TitleBg
        RemoteColor.Comment, // TitleBgActive
        RemoteColor.CurrentLine, // TitleBgCollapsed
        RemoteColor.CurrentLine, // MenuBarBg
        RemoteColor.CurrentLine, // ScrollbarBg
        RemoteColor.CurrentLine, // ScrollbarGrab
        RemoteColor.Cyan, // ScrollbarGrabHovered
        RemoteColor.Cyan, // ScrollbarGrabActive
        RemoteColor.Cyan, // CheckMark
        RemoteColor.Cyan, // SliderGrab
        RemoteColor.Purple, // SliderGrabActive
        RemoteColor.CurrentLine, // Button
        RemoteColor.Comment, // ButtonHovered
        RemoteColor.Comment, // ButtonActive
        RemoteColor.CurrentLine, // Header
        RemoteColor.Comment, // HeaderHovered
        RemoteColor.Comment, // HeaderActive
        RemoteColor.CurrentLine, // Separator
        RemoteColor.Comment, // SeparatorHovered
        RemoteColor.Comment, // SeparatorActive
        RemoteColor.CurrentLine, // ResizeGrip
        RemoteColor.Comment, // ResizeGripHovered
        RemoteColor.Comment, // ResizeGripActive
        RemoteColor.Comment, // TabHovered
        RemoteColor.CurrentLine, // Tab
        RemoteColor.Comment, // TabSelected
        RemoteColor.Comment, // TabSelectedOverline
        RemoteColor.CurrentLine, // TabDimmed
        RemoteColor.Comment, // TabDimmedSelected
        RemoteColor.Comment, // TabDimmedSelectedOverline
        RemoteColor.Comment, // DockingPreview
        RemoteColor.Background, // DockingEmptyBg
        RemoteColor.CurrentLine, // PlotLines
        RemoteColor.Comment, // PlotLinesHovered
        RemoteColor.CurrentLine, // PlotHistogram
        RemoteColor.Comment, // PlotHistogramHovered
        RemoteColor.CurrentLine, // TableHeaderBg
        RemoteColor.Comment, // TableBorderStrong
        RemoteColor.CurrentLine, // TableBorderLight
        RemoteColor.CurrentLine, // TableRowBg
        RemoteColor.Comment, // TableRowBgAlt
        RemoteColor.Cyan, // TextLink
        RemoteColor.CurrentLine, // TextSelectedBg
        RemoteColor.Comment, // DragDropTarget
        RemoteColor.Cyan, // NavCursor
        RemoteColor.Cyan, // NavWindowingHighlight
        RemoteColor.CurrentLine, // NavWindowingDimBg
        RemoteColor.CurrentLine, // ModalWindowDimBg
    ];

    /// <summary>Contains the colors for random tabs.</summary>
    public static ImmutableArray<RemoteColor> TabColors { get; } =
    [
        0xFF7B7BFF, 0xF89F72FF, 0xFEB464FF, 0xEFD25FFF,
        0xC0ED5EFF, 0x83ED66FF, 0x40EFAFFF, 0x5FD0EFFF,
        0x65A0FFFF, 0x7B83FFFF, 0xAA76FFFF, 0xD268F9FF,
        0xFC6AE2FF, 0xFF79A8FF, 0xABA9A9FF, 0x6B6B6BFF,
    ];
}
