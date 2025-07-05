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
    /// <seealso href="https://sronpersonalpages.nl/~pault/"/>
    public static ImmutableArray<AppColor> Sron { get; } =
    [
        0x77aaddff, 0x99ddffff, 0x448899ff, 0xbbcc33ff, 0xaaaa00ff, 0xeedd88ff, 0xee8866ff, 0xffaabbff,
        0xee7733ff, 0xcc3311ff, 0xee3377ff, 0xcc6677ff, 0x882255ff, 0xaa4499ff, 0xee0000ff, 0x00ee00ff,
        0x99cceeff, 0xbbeeffff, 0x66aabbff, 0xdddd55ff, 0xcccc22ff, 0xffeeaaff, 0xffaa88ff, 0xffccddff,
        0xff9955ff, 0xdd5533ff, 0xff5599ff, 0xdd8899ff, 0xaa4477ff, 0xcc66bbff, 0xff2222ff, 0x22ff22ff,
    ];
}
