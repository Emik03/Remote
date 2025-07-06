// SPDX-License-Identifier: MPL-2.0
namespace Remote;

using Color = System.Drawing.Color;

/// <inheritdoc cref="Preferences"/>
sealed partial class Preferences
{
    /// <summary>Contains the default set of colors.</summary>
    static readonly ImmutableArray<AppColor> s_defaultColors =
    [
        // AppPalette
        new(Color.Black), // Background
        AppColor.Foreground, // Neutral
        new(Color.Salmon), // Trap
        new(Color.SlateBlue), // Useful
        new(Color.Plum), // Progression
        AppColor.Yellow, // PendingItem
        AppColor.Green, // Reachable
        AppColor.Comment, // Checked
        AppColor.Red, // OutOfLogic
        new(Color.Red), // ReleasingOutOfLogic
        AppColor.Orange, // Releasing
        AppColor.Green, // Released
        AppColor.Orange, // BK
        // ImGuiCol
        AppColor.Foreground, // Text
        new(AppColor.Foreground.Vector with { W = 0.75f }), // TextDisabled
        AppColor.Black, // WindowBg
        AppColor.Black, // ChildBg
        AppColor.Black, // PopupBg
        AppColor.Comment, // Border
        AppColor.Black, // BorderShadow
        AppColor.Background, // FrameBg
        AppColor.Comment, // FrameBgHovered
        AppColor.Comment, // FrameBgActive
        AppColor.CurrentLine, // TitleBg
        AppColor.Comment, // TitleBgActive
        AppColor.CurrentLine, // TitleBgCollapsed
        AppColor.CurrentLine, // MenuBarBg
        AppColor.CurrentLine, // ScrollbarBg
        AppColor.CurrentLine, // ScrollbarGrab
        AppColor.Cyan, // ScrollbarGrabHovered
        AppColor.Cyan, // ScrollbarGrabActive
        AppColor.Cyan, // CheckMark
        AppColor.Cyan, // SliderGrab
        AppColor.Purple, // SliderGrabActive
        AppColor.CurrentLine, // Button
        AppColor.Comment, // ButtonHovered
        AppColor.Comment, // ButtonActive
        AppColor.CurrentLine, // Header
        AppColor.Comment, // HeaderHovered
        AppColor.Comment, // HeaderActive
        AppColor.CurrentLine, // Separator
        AppColor.Comment, // SeparatorHovered
        AppColor.Comment, // SeparatorActive
        AppColor.CurrentLine, // ResizeGrip
        AppColor.Comment, // ResizeGripHovered
        AppColor.Comment, // ResizeGripActive
        AppColor.Comment, // TabHovered
        AppColor.CurrentLine, // Tab
        AppColor.Comment, // TabSelected
        AppColor.Comment, // TabSelectedOverline
        AppColor.CurrentLine, // TabDimmed
        AppColor.Comment, // TabDimmedSelected
        AppColor.Comment, // TabDimmedSelectedOverline
        AppColor.Comment, // DockingPreview
        AppColor.Background, // DockingEmptyBg
        AppColor.CurrentLine, // PlotLines
        AppColor.Comment, // PlotLinesHovered
        AppColor.CurrentLine, // PlotHistogram
        AppColor.Comment, // PlotHistogramHovered
        AppColor.CurrentLine, // TableHeaderBg
        AppColor.Comment, // TableBorderStrong
        AppColor.CurrentLine, // TableBorderLight
        AppColor.CurrentLine, // TableRowBg
        AppColor.Comment, // TableRowBgAlt
        AppColor.Cyan, // TextLink
        AppColor.CurrentLine, // TextSelectedBg
        AppColor.Comment, // DragDropTarget
        AppColor.Cyan, // NavCursor
        AppColor.Cyan, // NavWindowingHighlight
        AppColor.CurrentLine, // NavWindowingDimBg
        AppColor.CurrentLine, // ModalWindowDimBg
    ];

    /// <summary>Contains the colors for random tabs.</summary>
    public static ImmutableArray<AppColor> TabColors { get; } =
    [
        0xFF7B7BFF, 0xF89F72FF, 0xFEB464FF, 0xEFD25FFF,
        0xC0ED5EFF, 0x83ED66FF, 0x40EFAFFF, 0x5FD0EFFF,
        0x65A0FFFF, 0x7B83FFFF, 0xAA76FFFF, 0xD268F9FF,
        0xFC6AE2FF, 0xFF79A8FF, 0xABA9A9FF, 0x6B6B6BFF,
    ];
}
