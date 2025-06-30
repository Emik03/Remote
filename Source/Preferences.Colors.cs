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
        AppColor.Comment, // TextDisabled
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
    /// <seealso href="https://sronpersonalpages.nl/~pault/"/>
    static readonly ImmutableArray<AppColor> s_sron =
    [
        0xddaa77ff, 0x998844ff, 0x00aaaaff, 0x6688eeff,
        0x3377eeff, 0x7733eeff, 0x552288ff, 0x0000eeff,
        0xeecc99ff, 0xbbaa66ff, 0x22ccccff, 0x88aaffff,
        0x5599ffff, 0x9955ffff, 0x7744aaff, 0x2222ffff,
        0xffdd99ff, 0x33ccbbff, 0x88ddeeff, 0xbbaaffff,
        0x1133ccff, 0x7766ccff, 0x9944aaff, 0x00ee00ff,
        0xffeebbff, 0x55ddddff, 0xaaeeffff, 0xddccffff,
        0x3355ddff, 0x9988ddff, 0xbb66ccff, 0x22ff22ff,
    ];
}
