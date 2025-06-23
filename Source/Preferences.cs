// SPDX-License-Identifier: MPL-2.0
namespace Remote;

using Vector2 = System.Numerics.Vector2;

/// <summary>Contains the preferences that are stored persistently.</summary>
public sealed partial class Preferences
{
    /// <summary>The flags to use across any text field.</summary>
    [CLSCompliant(false)]
    public const ImGuiInputTextFlags TextFlags =
        ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.CtrlEnterForNewLine;

    /// <summary>The default port number used by Archipelago.</summary>
    const int DefaultPort = 38281;

    /// <summary>The limits for UI scaling.</summary>
    public const float MinUiScale = 0.5f, MaxUiScale = 2.5f;

    /// <summary>The default host address that hosts Archipelago games.</summary>
    const string DefaultAddress = "archipelago.gg", PreferencesFile = "preferences.cfg";

    /// <summary>Contains the current port.</summary>
    int _port;

    /// <summary>Contains the current UI settings.</summary>
    float _uiScale = 0.75f, _uiPadding = 5, _uiRounding = 10;

    /// <summary>Contains the current text field values.</summary>
    string _address = DefaultAddress, _directory = DefaultDirectory, _password = "";

    /// <summary>Gets the color of the <see cref="Client.LocationStatus"/></summary>
    /// <param name="status">The status to get the color of.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The parameter <paramref name="status"/> is a value that isn't explicitly defined by the enum.
    /// </exception>
    public AppColor this[Client.LocationStatus status] =>
        status switch
        {
            Client.LocationStatus.Checked => this[AppPalette.Checked],
            Client.LocationStatus.Reachable => this[AppPalette.Reachable],
            Client.LocationStatus.OutOfLogic => this[AppPalette.OutOfLogic],
            Client.LocationStatus.ProbablyReachable => this[AppPalette.Neutral],
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    /// <summary>Gets the color of the <see cref="AppPalette"/></summary>
    /// <param name="color">The color to get the preference's color of.</param>
    public AppColor this[AppPalette color] => Colors[(int)color];

    /// <summary>Gets the default installation path of Archipelago.</summary>
    public static string DefaultDirectory { get; } = Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Archipelago"
    );

    /// <summary>Contains the path to the preferences file to read and write from.</summary>
    public static string FilePath { get; } =
        Environment.GetEnvironmentVariable("REMOTE_PREFERENCES_PATH") is { } preferences
            ? System.IO.Directory.Exists(preferences) ? Path.Join(preferences, PreferencesFile) : preferences
            : Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                typeof(Preferences).Assembly.GetName().Name,
                PreferencesFile
            );

    /// <summary>Gets or sets the UI scaling.</summary>
    public float UiScale
    {
        get => _uiScale;
        set => _uiScale = value;
    }

    /// <summary>Gets or sets the UI padding.</summary>
    public float UiPadding
    {
        get => _uiPadding;
        set => _uiPadding = value;
    }

    /// <summary>Gets or sets the UI rounding.</summary>
    public float UiRounding
    {
        get => _uiRounding;
        set => _uiRounding = value;
    }

    /// <summary>Gets or sets the port.</summary>
    [CLSCompliant(false)]
    public ushort Port
    {
        get => (ushort)_port;
        set => _port = value;
    }

    /// <summary>Gets or sets the address.</summary>
    public string Address
    {
        get => _address;
        set => _address = value;
    }

    /// <summary>Gets or sets the directory.</summary>
    public string Directory
    {
        get => _directory;
        set => _directory = value;
    }

    /// <summary>Gets or sets the password.</summary>
    public string Password
    {
        get => _password;
        set => _password = value;
    }

    /// <summary>The list of colors.</summary>
#pragma warning disable MA0016
    public List<AppColor> Colors { get; private set; } = [];
#pragma warning restore MA0016
    /// <summary>Loads the preferences from disk.</summary>
    /// <returns>The preferences.</returns>
    public static Preferences Load()
    {
        if (File.Exists(FilePath) && Kvp.Deserialize<Preferences>(File.ReadAllText(FilePath)) is var fromDisk)
        {
            fromDisk.Sanitize();
            return fromDisk;
        }

        var directory = Path.GetDirectoryName(FilePath);
        Debug.Assert(directory is not null);
        System.IO.Directory.CreateDirectory(directory);
        Preferences fromMemory = new();
        fromMemory.Sanitize();
        File.WriteAllText(FilePath, Kvp.Serialize(fromMemory));
        return fromMemory;
    }

    /// <summary>Pushes all colors and styling variables.</summary>
    public void PushStyling()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(_uiScale * 600));

        for (var i = (int)AppPalette.Count; i < Colors.Count; i++)
            ImGui.PushStyleColor((ImGuiCol)(i - AppPalette.Count), Colors[i]);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, _uiRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, _uiRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, _uiRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, _uiRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, _uiRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, _uiRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, _uiRounding);

        Vector2 padding = new(_uiPadding);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, padding);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, padding);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, padding);
        ImGui.PushStyleVar(ImGuiStyleVar.SeparatorTextPadding, padding);
    }

    /// <summary>Pops all colors and styling variables.</summary>
    public void PopStyling()
    {
        if (Colors.Count - (int)AppPalette.Count is > 0 and var count)
            ImGui.PopStyleColor(count);

        ImGui.PopStyleVar(12);
    }

    /// <summary>Writes this instance to disk.</summary>
    public void Save() => File.WriteAllText(FilePath, Kvp.Serialize(this));

    /// <summary>Shows the preferences window.</summary>
    /// <returns>Whether to create a new instance of <see cref="Client"/>.</returns>
    public bool Show()
    {
        if (!ImGui.BeginTabBar("Tabs"))
            return false;

        ImGui.SetWindowFontScale(UiScale);
        var ret = ShowConnectionTab();
        ShowSettings();
        ImGui.EndTabBar();
        return ret;
    }

    /// <summary>Gets the available width while conforming to the specified margin.</summary>
    /// <param name="margin">The margin.</param>
    /// <returns>The width to use.</returns>
    public float Width(int margin) => ImGui.GetContentRegionAvail().X - UiScale * margin;

    /// <summary>Gets the size of a child window.</summary>
    /// <param name="margin">The margin.</param>
    /// <returns>The size to use.</returns>
    public Vector2 ChildSize(int margin = 150) => ImGui.GetContentRegionAvail() - new Vector2(0, UiScale * margin);

    /// <summary>Displays the settings tab.</summary>
    void ShowSettings()
    {
        if (!ImGui.BeginTabItem("Settings"))
            return;

        ImGui.SeparatorText("Primary");
        ImGui.SetNextItemWidth(Width(225));

        _ = ImGuiRenderer.InputTextWithHint(
            "AP Directory",
            DefaultDirectory,
            ref _directory,
            ushort.MaxValue
        );

        ImGui.Separator();
        Slider("UI Scale", ref _uiScale, 0.25f, 2, "%.2f");
        Slider("UI Padding", ref _uiPadding, 0, 20);
        Slider("UI Rounding", ref _uiRounding, 0, 30);
        ImGui.SeparatorText("Theming");

        if (ImGui.CollapsingHeader("Theme"))
            for (var i = 0; i < Colors.Count.Min((int)AppPalette.Count); i++)
                ShowColor(i);

        if (ImGui.CollapsingHeader("Theme (Advanced)"))
            for (var i = (int)AppPalette.Count; i < Colors.Count; i++)
                ShowColor(i);

        ImGui.EndTabItem();
    }

    /// <summary>Helper method for displaying a slider.</summary>
    /// <param name="title">The title.</param>
    /// <param name="amount">The current amount.</param>
    /// <param name="min">The minimum.</param>
    /// <param name="max">The maximum.</param>
    /// <param name="format">The format to display.</param>
    void Slider(string title, ref float amount, float min, float max, string format = "%.1f")
    {
        ImGui.SetNextItemWidth(Width(225));
        _ = ImGui.SliderFloat(title, ref amount, min, max, format);
        amount = amount.Clamp(min, max);
    }

    /// <summary>Sanitizes the port and colors.</summary>
    void Sanitize()
    {
        if (_port is < 1024 or > ushort.MaxValue)
            _port = DefaultPort;

        if (Colors.Count < s_defaultColors.Length)
            Colors = [..Colors, ..s_defaultColors.AsSpan()[Colors.Count..]];
    }

    /// <summary>Shows the color editor for the index.</summary>
    /// <param name="i">The index from <see cref="Colors"/> to display and mutate.</param>
    void ShowColor(int i)
    {
        ImGui.Separator();

        var name = i < (int)AppPalette.Count
            ? ((AppPalette)i).ToString()
            : ((ImGuiCol)(i - (int)AppPalette.Count)).ToString();

        var v = Colors[i].Vector;
        _ = ImGui.ColorEdit4(name, ref v, ImGuiColorEditFlags.DisplayHex);
        _ = ImGui.ColorEdit4($"##{name}", ref v, ImGuiColorEditFlags.DisplayRGB | ImGuiColorEditFlags.NoSmallPreview);
        Colors[i] = new(v);
    }

    /// <summary>Shows the connection tab.</summary>
    /// <returns>Whether to create a new <see cref="Client"/>.</returns>
    bool ShowConnectionTab()
    {
        if (!ImGui.BeginTabItem("Connection"))
            return false;

        ImGui.SeparatorText("Host");
        ImGui.SetNextItemWidth(Width(450));
        ImGuiRenderer.InputTextWithHint("Address", DefaultAddress, ref _address, ushort.MaxValue, TextFlags);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(Width(100));
        ImGui.InputInt("Port", ref _port, 0);
        ImGui.SetNextItemWidth(Width(450));

        var enter = ImGuiRenderer.InputText(
            "Password",
            ref _password,
            ushort.MaxValue,
            ImGuiInputTextFlags.Password | TextFlags
        );

        ImGui.SeparatorText("Join");
        ImGui.TextDisabled("Drop a YAML file to start playing, or...");
        Sanitize();

        if (ImGui.Button("Enter slot manually") || enter)
        {
            ImGui.EndTabItem();
            return true;
        }

        ImGui.EndTabItem();
        return false;
    }
}
