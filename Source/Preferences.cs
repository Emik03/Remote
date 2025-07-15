// SPDX-License-Identifier: MPL-2.0
namespace Remote;

using ConnectionGroup = IGrouping<(string? Alias, string? Host, ushort Port), HistoryEntry>;
using Vector2 = System.Numerics.Vector2;

/// <summary>Contains the preferences that are stored persistently.</summary>
public sealed partial class Preferences
{
    /// <summary>Contains the font languages.</summary>
    public enum Language
    {
        /// <summary>The default font.</summary>
        Default,

        /// <summary>The english font.</summary>
        English,

        /// <summary>The japanese font.</summary>
        Japanese,

        /// <summary>The korean font.</summary>
        Korean,

        /// <summary>The thai font.</summary>
        Thai,
    }

    /// <summary>Contains the orderings for history.</summary>
    public enum HistoryOrder
    {
        /// <summary>This value indicates to sort the history by date, newest to oldest.</summary>
        Date,

        /// <summary>This value indicates to sort the history alphabetically.</summary>
        Name,
    }

    /// <summary>The flags to use across any text field.</summary>
    [CLSCompliant(false)]
    public const ImGuiInputTextFlags TextFlags =
        ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.CtrlEnterForNewLine;

    /// <summary>The default port number used by Archipelago.</summary>
    const int DefaultPort = 38281;

    /// <summary>The limits for UI scaling.</summary>
    public const float MinUiScale = 0.5f, MaxUiScale = 2.5f;

    /// <summary>The default host address that hosts Archipelago games.</summary>
    const string DefaultAddress = "archipelago.gg",
        PreferencesFile = "preferences.cfg",
        UpdateHostOrPortMessage =
            """
            If the host or port changes, you can enter them
            in the above fields, and then click to join.
            The history will update accordingly.
            """;

    /// <summary>Gets the languages.</summary>
    static readonly string s_historyOrder = NamesSeparatedByZeros<HistoryOrder>(),
        s_languages = NamesSeparatedByZeros<Language>();

    /// <summary>Gets the <see cref="ImGuiCol"/> set that represents background colors.</summary>
    static readonly FrozenSet<ImGuiCol> s_backgrounds = Enum.GetValues<ImGuiCol>()
       .Where(x => x.ToString() is [.., 'B' or 'b', 'G' or 'g'])
       .ToFrozenSet();

    static readonly IEqualityComparer<(string? Alias, string? Host, ushort Port)> s_equality =
        Equating<(string? Alias, string? Host, ushort Port)>(
            (x, y) => x.Port == y.Port && FrozenSortedDictionary.Comparer.Equals(x.Host, y.Host)
        );

    /// <summary>Determines whether a restart needs to be performed to apply changes.</summary>
    static bool s_requiresRestart;

    /// <summary>Contains the state for whether to display the editing text for connection headers.</summary>
    readonly Dictionary<string, bool> _editStates = new(FrozenSortedDictionary.Comparer);

    /// <summary>Contains the history.</summary>
    readonly HistoryEntry.List _list = HistoryEntry.List.Load();

    /// <summary>Contains boolean settings.</summary>
    bool _alwaysShowChat,
        _desktopNotifications,
        _holdToConfirm = true,
        _moveToChatTab = true,
        _sideBySide,
        _useTabs = true;

    /// <summary>Contains integer settings.</summary>
    int _language, _port, _sortHistoryBy;

    /// <summary>Contains the current UI settings.</summary>
    float _activeTabDim = 2.5f,
        _inactiveTabDim = 5,
        _suggestions = 5,
        _uiScale = 0.75f,
        _uiRounding = 4,
        _uiSpacing = 6,
        _windowDim = 10;

    /// <summary>Contains the current text field values.</summary>
    string _address = DefaultAddress,
        _directory = DefaultDirectory,
        _password = "",
        _python = "",
        _repo = "",
        _yamlFilePath = "";

    /// <summary>Gets the color of the <see cref="AppPalette"/></summary>
    /// <param name="color">The color to get the preference's color of.</param>
    public AppColor this[AppPalette color] => Colors[(int)color];

    /// <summary>Gets the color of the <see cref="Client.LocationStatus"/></summary>
    /// <param name="status">The status to get the color of.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The parameter <paramref name="status"/> is a value that isn't explicitly defined by the enum.
    /// </exception>
    public AppColor this[Client.LocationStatus status] =>
        status switch
        {
            Client.LocationStatus.Hidden => default,
            Client.LocationStatus.Checked => this[AppPalette.Checked],
            Client.LocationStatus.Reachable => this[AppPalette.Reachable],
            Client.LocationStatus.OutOfLogic => this[AppPalette.OutOfLogic],
            Client.LocationStatus.ProbablyReachable => this[AppPalette.Neutral],
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    /// <summary>Gets the color of the <see cref="ItemFlags"/>.</summary>
    /// <param name="flags">The flags to get the color of.</param>
    [CLSCompliant(false)]
    public AppColor this[ItemFlags? flags] =>
        flags switch
        {
            ItemFlags.None => this[AppPalette.Neutral],
            { } f when f.Has(ItemFlags.Advancement) => this[AppPalette.Progression],
            { } f when f.Has(ItemFlags.NeverExclude) => this[AppPalette.Useful],
            { } f when f.Has(ItemFlags.Trap) => this[AppPalette.Trap],
            _ => this[AppPalette.PendingItem],
        };

    /// <summary>Gets the default installation path of Archipelago.</summary>
    public static string DefaultDirectory { get; } = Path.Join(
        Environment.GetFolderPath(
            OperatingSystem.IsWindows()
                ? Environment.SpecialFolder.CommonApplicationData
                : Environment.SpecialFolder.LocalApplicationData
        ),
        "Archipelago"
    );

    /// <summary>Gets or sets the value determining the tooltip was shown in this frame.</summary>
    public static bool ShownTooltip { get; set; }

    /// <summary>Contains the path to the preferences file to read and write from.</summary>
    public static string FilePath { get; } = PathTo(PreferencesFile, "REMOTE_PREFERENCES_PATH");

    /// <summary>Gets or sets the value determining whether to always show the chat.</summary>
    public bool AlwaysShowChat
    {
        get => _alwaysShowChat;
        [UsedImplicitly] private set => _alwaysShowChat = value;
    }

    /// <summary>Gets or sets the value determining whether to have desktop notifications.</summary>
    public bool DesktopNotifications
    {
        get => _desktopNotifications;
        [UsedImplicitly] private set => _desktopNotifications = value;
    }

    /// <summary>Gets or sets the value determining whether to have holding to confirm location releases.</summary>
    public bool HoldToConfirm
    {
        get => _holdToConfirm;
        [UsedImplicitly] private set => _holdToConfirm = value;
    }

    /// <summary>Gets or sets the value determining whether to use tabs.</summary>
    public bool UseTabs
    {
        get => _useTabs;
        [UsedImplicitly] private set => _useTabs = value;
    }

    /// <summary>Gets or sets the value determining whether to use tabs.</summary>
    public bool MoveToChatTab
    {
        get => _moveToChatTab;
        [UsedImplicitly] private set => _moveToChatTab = value;
    }

    /// <summary>Gets or sets the value determining whether to display the chat side-by-side.</summary>
    public bool SideBySide
    {
        get => _sideBySide;
        [UsedImplicitly] private set => _sideBySide = value;
    }

    /// <summary>Gets or sets the active tab dim.</summary>
    public float ActiveTabDim
    {
        get => _activeTabDim;
        [UsedImplicitly] private set => _activeTabDim = value;
    }

    /// <summary>Gets or sets the inactive tab dim.</summary>
    public float InactiveTabDim
    {
        get => _inactiveTabDim;
        [UsedImplicitly] private set => _inactiveTabDim = value;
    }

    /// <summary>Gets or sets the UI scaling.</summary>
    public float FontSize { get; [UsedImplicitly] private set; } = 36;

    /// <summary>Gets or sets the number of suggestions in the autocomplete.</summary>
    public float Suggestions
    {
        get => _suggestions;
        [UsedImplicitly] private set => _suggestions = value;
    }

    /// <summary>Gets or sets the UI scaling.</summary>
    public float UiScale
    {
        get => _uiScale;
        [UsedImplicitly] private set => _uiScale = value;
    }

    /// <summary>Gets or sets the UI rounding.</summary>
    public float UiRounding
    {
        get => _uiRounding;
        [UsedImplicitly] private set => _uiRounding = value;
    }

    /// <summary>Gets or sets the UI spacing.</summary>
    public float UiSpacing
    {
        get => _uiSpacing;
        [UsedImplicitly] private set => _uiSpacing = value;
    }

    /// <summary>Gets or sets the window dim.</summary>
    public float WindowDim
    {
        get => _windowDim;
        [UsedImplicitly] private set => _windowDim = value;
    }

    /// <summary>Gets or sets the port.</summary>
    [CLSCompliant(false)]
    public ushort Port
    {
        get => (ushort)_port;
        [UsedImplicitly] private set => _port = value;
    }

    /// <summary>Gets or sets the address.</summary>
    public string Address
    {
        get => _address;
        [UsedImplicitly] private set => _address = value;
    }

    /// <summary>Gets or sets the directory.</summary>
    public string Directory
    {
        get => _directory;
        [UsedImplicitly] private set => _directory = value;
    }

    /// <summary>Gets or sets the password.</summary>
    public string Password
    {
        get => _password;
        [UsedImplicitly] private set => _password = value;
    }

    /// <summary>Gets or sets the python path.</summary>
    public string Python
    {
        get => _python;
        [UsedImplicitly] private set => _python = value;
    }

    /// <summary>Gets or sets the archipelago repository path.</summary>
    public string Repo
    {
        get => _repo;
        [UsedImplicitly] private set => _repo = value;
    }

    /// <summary>Gets or sets the value determining whether to sort the history by name or by last used.</summary>
    public HistoryOrder SortHistoryBy
    {
        get => (HistoryOrder)_sortHistoryBy;
        [UsedImplicitly] private set => _sortHistoryBy = (int)value;
    }

    /// <summary>Gets or sets the font language.</summary>
    public Language FontLanguage
    {
        get => (Language)_language;
        [UsedImplicitly] private set => _language = (int)value;
    }

    /// <summary>Gets or sets the UI padding.</summary>
#pragma warning disable MA0016
    public List<float> UiPadding { get; [UsedImplicitly] private set; } = [5, 5];
#pragma warning restore MA0016
    /// <summary>Gets the child process to wait for before disposing.</summary>
#pragma warning disable IDISP006
    public Process? ChildProcess { get; private set; }
#pragma warning restore IDISP006
    /// <summary>Shows the color edit widget.</summary>
    /// <param name="name">The displayed text.</param>
    /// <param name="color">The color that will change.</param>
    /// <returns>The new color.</returns>
    public static AppColor ShowColorEdit(string name, AppColor color)
    {
        var v = color.Vector;
        ImGui.ColorEdit4(name, ref v, ImGuiColorEditFlags.DisplayHex);
        return new(v);
    }

    /// <summary>Gets the list of colors.</summary>
#pragma warning disable MA0016
    public List<AppColor> Colors { get; private set; } = [];
#pragma warning restore MA0016
    /// <summary>Gets the full path to the file.</summary>
    /// <param name="file">The file path to get.</param>
    /// <param name="environment">The environment variable that allows users to override the return.</param>
    /// <returns>The full path to the parameter <paramref name="file"/>.</returns>
    public static string PathTo(string file, string environment) =>
        Environment.GetEnvironmentVariable(environment) is { } preferences
            ? System.IO.Directory.Exists(preferences) ? Path.Join(preferences, file) : preferences
            : Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                typeof(Preferences).Assembly.GetName().Name,
                file
            );

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
        fromMemory.Save();
        return fromMemory;
    }

    /// <summary>Prepends the element to the beginning of the history.</summary>
    /// <param name="historyEntry">The connection to prepend.</param>
    [CLSCompliant(false)]
    public void Prepend(HistoryEntry historyEntry) => _list.History.Insert(0, historyEntry);

    /// <summary>Pushes the specific color into most widgets.</summary>
    /// <param name="color">The color.</param>
    public void PushStyling(AppColor color)
    {
        var active = color / ActiveTabDim;
        var inactive = color / InactiveTabDim;
        ImGui.PushStyleColor(ImGuiCol.TextSelectedBg, color);
        ImGui.PushStyleColor(ImGuiCol.TabSelected, active);
        ImGui.PushStyleColor(ImGuiCol.TabHovered, active);
        ImGui.PushStyleColor(ImGuiCol.Tab, inactive);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, active);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, active);
        ImGui.PushStyleColor(ImGuiCol.Button, inactive);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, active);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, active);
        ImGui.PushStyleColor(ImGuiCol.Header, inactive);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, active);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, active);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, inactive);
    }

    /// <summary>Pushes all colors and styling variables.</summary>
    /// <param name="active">The current tab.</param>
    public void PushStyling(Client? active)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(_uiScale * 600));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, _useTabs ? 0 : 5);

        for (var i = (int)AppPalette.Count; i < Colors.Count && (ImGuiCol)(i - AppPalette.Count) is var color; i++)
            ImGui.PushStyleColor(
                color,
                s_backgrounds.Contains(color) && active?.Color is { } c ? c / _windowDim : Colors[i]
            );

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, _useTabs ? 0 : _uiRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, _uiRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, _uiRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, _uiRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, _uiRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, _uiRounding);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, _uiRounding);

        Vector2 padding = new(UiPadding[0], UiPadding[1]);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, padding);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, padding);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, padding);
        ImGui.PushStyleVar(ImGuiStyleVar.SeparatorTextPadding, padding);

        Vector2 spacing = new(_uiSpacing);

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, spacing);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, spacing);
        ImGui.PushStyleVar(ImGuiStyleVar.IndentSpacing, _uiSpacing);
    }

    /// <summary>Pops all colors and styling variables.</summary>
    public void PopStyling()
    {
        if (Colors.Count - (int)AppPalette.Count is > 0 and var count)
            ImGui.PopStyleColor(count);

        ImGui.PopStyleVar(16);
    }

    /// <summary>Writes this instance to disk.</summary>
    public void Save()
    {
        File.WriteAllText(FilePath, Kvp.Serialize(this));
        _list.Save();
    }

    /// <summary>Shows a help widget.</summary>
    /// <param name="message">The message to display when the question is hovered on.</param>
    public void ShowHelp(string message)
    {
        ImGui.SameLine();
        ImGui.TextDisabled("(?)");
        Tooltip(message);
    }

    /// <summary>Shows the text.</summary>
    /// <param name="text">The text to show.</param>
    /// <param name="color">The color of the text.</param>
    /// <param name="clipboard">The text to copy when this is clicked.</param>
    /// <param name="tooltip">The tooltip to display when hovered over.</param>
    /// <param name="disabled">Whether the text is disabled.</param>
#pragma warning disable MA0051
    public unsafe void ShowText(
#pragma warning restore MA0051
        string text,
        AppColor? color = null,
        string? clipboard = null,
        string? tooltip = null,
        bool disabled = false
    )
    {
        var (copy, pad, pushed) = (clipboard ?? text, false, true);
        Pad(text, copy);

        if (disabled && ImGui.GetStyleColorVec4(ImGuiCol.TextDisabled) is not null and var ptr)
            ImGui.PushStyleColor(ImGuiCol.Text, *ptr);
        else if (color is { } v)
            ImGui.PushStyleColor(ImGuiCol.Text, v);
        else
            pushed = false;

        foreach (var w in text.SplitSpanWhitespace())
        {
            if ((pad ? ImGui.CalcTextSize(w).X + ImGui.CalcTextSize([' ']).X : ImGui.CalcTextSize(w).X) is var width &&
                ImGui.GetContentRegionAvail().X is var available &&
                available <= width)
            {
                ImGui.NewLine();
                Pad(text, copy);
                (pad, width, available) = (false, ImGui.CalcTextSize(w).X, ImGui.GetContentRegionAvail().X);
            }

            if (w is var drain && available > width)
            {
                if (pad)
                {
                    ImGui.TextUnformatted([' ']);
                    CopyIfClicked(copy);
                    Tooltip(tooltip);
                    ImGui.SameLine(0, 0);
                }

                ImGui.TextUnformatted(w);
                CopyIfClicked(copy);
                Tooltip(tooltip);
                ImGui.SameLine(0, 0);
                pad = true;
                continue;
            }

            for (var i = 2; i <= drain.Length; i++)
                if (ImGui.GetContentRegionAvail().X <= ImGui.CalcTextSize(drain[..i]).X)
                {
                    ImGui.TextUnformatted(drain[..(i - 1)]);
                    Pad(text, copy);
                    CopyIfClicked(copy);
                    Tooltip(tooltip);
                    drain = drain[(i - 1)..];
                    i = 2;
                }

            ImGui.TextUnformatted(drain);
            CopyIfClicked(copy);
            Tooltip(tooltip);
            ImGui.SameLine(0, 0);
            pad = true;
        }

        ImGui.TextUnformatted([' ']);
        CopyIfClicked(copy);
        Tooltip(tooltip);

        if (pushed)
            ImGui.PopStyleColor();
    }

    /// <summary>Copies the text if the mouse has been clicked.</summary>
    /// <param name="text">The text to copy.</param>
    /// <param name="button">The button to check for.</param>
    [CLSCompliant(false)]
    public void CopyIfClicked(string text, ImGuiMouseButton button = ImGuiMouseButton.Right)
    {
        if (!ImGui.IsItemHovered())
            return;

        if (ImGui.IsMouseDown(button))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, this[AppPalette.Reachable]);
            Tooltip("Copied!");
            ImGui.PopStyleColor();
        }

        if (ImGui.IsMouseClicked(button))
#if ANDROID
            ImGui.SetClipboardText(text);
#else
            ClipboardService.SetText(text);
#endif
    }

    /// <summary>Convenience function for displaying a tooltip with text scaled by the user preferences.</summary>
    /// <param name="text">The text to display.</param>
    /// <param name="colored">Whether to make the text colorful.</param>
    public void Tooltip(string? text, bool colored = false)
    {
        if (text is null || !ImGui.IsItemHovered())
            return;

        ImGui.PushStyleColor(ImGuiCol.PopupBg, this[AppPalette.Count + (int)ImGuiCol.PopupBg]);

        if (!ImGui.BeginTooltip())
        {
            ImGui.PopStyleColor();
            return;
        }

        ImGui.SetWindowFontScale(UiScale);

        if (ShownTooltip)
            ImGui.NewLine();

        ShownTooltip = true;
        Wrapped(text, ImGui.GetMainViewport().Size.X, colored);
        ImGui.EndTooltip();
        ImGui.PopStyleColor();
    }

    /// <summary>Shows the text.</summary>
    /// <param name="text">The text to show.</param>
    /// <param name="color">The color of the text.</param>
    /// <param name="clipboard">The text to copy when this is clicked.</param>
    /// <param name="tooltip">The tooltip to display when hovered over.</param>
    public void ShowText(string text, AppPalette color, string? clipboard = null, string? tooltip = null) =>
        ShowText(text, this[color], clipboard, tooltip);

    /// <inheritdoc cref="ShowText(string, AppPalette, string, string)"/>
    public void ShowText(string text, Client.LocationStatus color, string? clipboard = null, string? tooltip = null) =>
        ShowText(text, this[color], clipboard, tooltip);

    /// <inheritdoc cref="ShowText(string, AppPalette, string, string)"/>
    [CLSCompliant(false)]
    public void ShowText(string text, ItemFlags? color, string? clipboard = null, string? tooltip = null) =>
        ShowText(text, this[color], clipboard, tooltip);

    /// <summary>Synchronizes the connection with the one found within the internal collection.</summary>
    /// <param name="historyEntry">The connection to synchronize.</param>
    [CLSCompliant(false)]
    public void Sync(ref HistoryEntry historyEntry)
    {
        string FindNextAvailableColor()
        {
            foreach (var color in TabColors)
            {
                var history = CollectionsMarshal.AsSpan(_list.History);

                for (var i = 0; i < history.Length; i++)
                {
                    ref var current = ref history[i];

                    if (!string.IsNullOrWhiteSpace(current.Color) && AppColor.Parse(current.Color) == color)
                        goto Next;
                }

                return color.ToString();

            Next: ;
            }

            return TabColors.PickRandom().ToString();
        }

        var history = CollectionsMarshal.AsSpan(_list.History);

        for (var i = 0; i < history.Length; i++)
        {
            ref var next = ref history[i];

            if (string.IsNullOrWhiteSpace(historyEntry.Color) && string.IsNullOrWhiteSpace(next.Color))
                next = next with { Color = FindNextAvailableColor() };

            if (!historyEntry.HostEquals(next))
                continue;

            if (!FrozenSortedDictionary.Comparer.Equals(historyEntry.Alias, next.Alias))
                next = next with { Alias = historyEntry.Alias };

            if (!FrozenSortedDictionary.Comparer.Equals(historyEntry.Name, next.Name))
                continue;

            historyEntry = new(historyEntry, next.GetLocationsOrEmpty(), next.Alias, next.Color);
            next = historyEntry;
        }
    }

    /// <summary>Invokes <see cref="ImGui.BeginChild(string)"/>.</summary>
    /// <param name="id">The id.</param>
    /// <param name="sameLine">Whether to call <see cref="ImGui.SameLine()"/></param>
    /// <returns>Whether to continue rendering.</returns>
    public bool BeginChild(string id, bool sameLine = false)
    {
        if (!_alwaysShowChat)
            return ImGui.BeginChild(id);

        var available = ImGui.GetContentRegionAvail();

        if (_sideBySide)
            available.X /= 2;
        else if (!sameLine)
            available.Y /= 2;

        if (sameLine && _sideBySide)
            ImGui.SameLine();

        return ImGui.BeginChild(id, available);
    }

    /// <summary>Mimics <see cref="ImGui.Combo(string, ref int, string)"/>.</summary>
    /// <param name="label">The label.</param>
    /// <param name="currentItem">The index of the current item.</param>
    /// <param name="itemsSeparatedByZeros">The items separated by the character <c>\0</c>.</param>
    /// <param name="applyUiScale">Whether to apply the current UI scaling.</param>
    /// <returns></returns>
    public bool Combo(string label, ref int currentItem, string itemsSeparatedByZeros, bool applyUiScale = true)
    {
        var split = itemsSeparatedByZeros.SplitSpanOn('\0');

        if (!ImGui.BeginCombo(label, split[currentItem]))
            return false;

        ImGui.SetWindowFontScale(UiScale);
        var i = 0;

        foreach (var span in split)
        {
            if (ImGui.MenuItem(span))
                currentItem = i;

            i++;
        }

        ImGui.EndCombo();
        return true;
    }

    /// <summary>Determines whether <see cref="HistoryEntry.HasDeathLink"/> should be enabled.</summary>
    /// <param name="address">The address.</param>
    /// <param name="port">The port.</param>
    /// <param name="name">The name of the slot.</param>
    /// <returns>Whether death link should be enabled.</returns>
    [CLSCompliant(false)]
    public bool HasDeathLink(string address, ushort port, string name)
    {
        for (var i = 0; _list.History.Nth(i) is { IsInvalid: false } connection; i++)
            if (port == connection.Port &&
                FrozenSortedDictionary.Comparer.Equals(address, connection.Host) &&
                FrozenSortedDictionary.Comparer.Equals(name, connection.Name))
                return connection.HasDeathLink;

        return false;
    }

    /// <summary>Shows the preferences window.</summary>
    /// <param name="gameTime">The time elapsed.</param>
    /// <param name="clients">The list of clients to show.</param>
    /// <param name="tab">The selected tab.</param>
    /// <param name="clientsToRegister">The clients created from history, or <see langword="null"/>.</param>
    /// <returns>Whether to create a new instance of <see cref="Client"/>.</returns>
    [CLSCompliant(false)]
#pragma warning disable MA0016
    public bool Show(GameTime gameTime, List<Client> clients, out int? tab, out IEnumerable<Client>? clientsToRegister)
#pragma warning restore MA0016
    {
        clientsToRegister = null;
        var useTabs = _useTabs;

        if (!ImGui.BeginTabBar("Tabs", ImGuiTabBarFlags.Reorderable))
        {
            tab = useTabs ? null : Show(gameTime, clients);
            return false;
        }

        ImGui.SetWindowFontScale(UiScale);
        var ret = ShowConnectionTab(clients, out clientsToRegister);
        ShowPreferences();

        if (useTabs)
        {
            tab = Show(gameTime, clients);
            ImGui.EndTabBar();
        }
        else
        {
            ImGui.EndTabBar();
            tab = Show(gameTime, clients);
        }

        return ret;
    }

    /// <summary>Gets the available width while conforming to the specified margin.</summary>
    /// <param name="margin">The margin.</param>
    /// <returns>The width to use.</returns>
    public float Width(int margin) => ImGui.GetContentRegionAvail().X - UiScale * margin;

    /// <summary>Gets the python path.</summary>
    /// <returns>The python path.</returns>
    public string GetPythonPath() =>
        string.IsNullOrWhiteSpace(_python) ? "python" :
        System.IO.Directory.Exists(_python) ? Path.Join(Python, "python") : _python;

    /// <summary>Adds the current font.</summary>
    /// <returns>The created font, or <see langword="default"/> if the resource doesn't exist.</returns>
    [CLSCompliant(false)]
    public unsafe ImFontPtr AddFont()
    {
        var resource = FontLanguage switch
        {
            Language.English => $"{nameof(Remote)}.{nameof(Resources)}.Fonts.alt.ttf",
            Language.Japanese => $"{nameof(Remote)}.{nameof(Resources)}.Fonts.japanese.ttf",
            Language.Korean => $"{nameof(Remote)}.{nameof(Resources)}.Fonts.korean.ttf",
            Language.Thai => $"{nameof(Remote)}.{nameof(Resources)}.Fonts.thai.ttf",
            _ => $"{nameof(Remote)}.{nameof(Resources)}.Fonts.main.ttf",
        };

        if (typeof(AssemblyMarker).Assembly.GetManifestResourceStream(resource) is not { } stream)
            return default;

        var font = Read(stream);
        var io = ImGui.GetIO();
        var fonts = io.Fonts;

        var ranges = FontLanguage switch
        {
            Language.Japanese => fonts.GetGlyphRangesJapanese(),
            Language.Korean => fonts.GetGlyphRangesKorean(),
            Language.Thai => fonts.GetGlyphRangesThai(),
            _ => fonts.GetGlyphRangesDefault(),
        };

        fixed (byte* ptr = font)
            return io.Fonts.AddFontFromMemoryTTF((nint)ptr, font.Length, FontSize.Clamp(8, 72), 0, ranges);
    }

    /// <summary>Gets the size of a child window.</summary>
    /// <param name="margin">The margin.</param>
    /// <returns>The size to use.</returns>
    public Vector2 ChildSize(int margin = 150) => ImGui.GetContentRegionAvail() - new Vector2(0, UiScale * margin);

    /// <summary>Pushes the text.</summary>
    /// <param name="span">The span.</param>
    /// <param name="makeColorful">Whether or not to make it colorful.</param>
    /// <param name="braces">The number of braces.</param>
    /// <param name="isIdentifier">Whether or not it is processing an identifier.</param>
    static void PushRange(ReadOnlySpan<char> span, bool makeColorful, ref int braces, ref bool isIdentifier)
    {
        static void Push(char c, bool makeColorful, in int braces)
        {
            const int L = 3;

            if (makeColorful)
            {
                var index = (braces * L + braces * L / TabColors.Length) % TabColors.Length;
                ImGui.PushStyleColor(ImGuiCol.Text, TabColors[index]);
            }

            ImGui.TextUnformatted([c]);
            ImGui.SameLine(0, 0);

            if (makeColorful)
                ImGui.PopStyleColor();
        }

        foreach (var c in span)
            switch (c)
            {
                case '(':
                    Push(c, makeColorful, isIdentifier ? braces : ++braces);
                    break;
                case ')':
                    Push(c, makeColorful, isIdentifier ? braces : braces--);
                    break;
                case '|':
                    isIdentifier ^= isIdentifier;
                    goto default;
                default:
                    Push(c, makeColorful, braces);
                    break;
            }
    }

    /// <summary>Creates the wrapped string.</summary>
    /// <param name="text">The text to wrap.</param>
    /// <param name="maxWidth">The maximum width.</param>
    /// <param name="colored">Whether to make the text colorful.</param>
    /// <returns>The wrapped version of the parameter <paramref name="text"/>.</returns>
    static void Wrapped(string text, float maxWidth, bool colored)
    {
        var sum = 0f;
        var span = text.AsSpan();
        var isIdentifier = false;
        int braces = 0, last = 0, replace = 0;
        SearchValues<char> br = Whitespaces.BreakingSearch.GetSpan()[0], ws = Whitespaces.UnicodeSearch.GetSpan()[0];

        for (var i = 0; i < span.Length && span[i] is var c && (sum += ImGui.CalcTextSize([c]).X) is var _; i++)
            switch (c)
            {
                case var _ when br.Contains(c):
                    sum = 0;
                    PushRange(span[last..i], colored, ref braces, ref isIdentifier);
                    replace = last = i + 1;
                    ImGui.NewLine();
                    break;
                case var _ when ws.Contains(c):
                    replace = i;
                    break;
                case var _ when sum <= maxWidth: break;
                default:
                    PushRange(span[last..replace], colored, ref braces, ref isIdentifier);
                    sum = ImGui.CalcTextSize(span[replace..i]).X;
                    last = replace + 1;
                    ImGui.NewLine();
                    break;
            }

        PushRange(span[last..], colored, ref braces, ref isIdentifier);
    }

    /// <summary>Reads the stream into a byte array.</summary>
    /// <param name="input">The stream to read.</param>
    /// <returns>The byte array.</returns>
    static byte[] Read(Stream input)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1 << 14);
        using MemoryStream ms = new();

        while (input.Read(buffer) is > 0 and var read)
            ms.Write(buffer.AsSpan(0, read));

        var ret = ms.ToArray();
        ArrayPool<byte>.Shared.Return(buffer);
        return ret;
    }

    /// <summary>Gets the names separated by the null (<c>\0</c>) character.</summary>
    /// <typeparam name="T">The type to get the names of.</typeparam>
    /// <returns>
    /// The <see cref="string"/> containing the values in the type parameter <typeparamref name="T"/>.
    /// </returns>
    static string NamesSeparatedByZeros<T>()
        where T : struct, Enum =>
        Enum.GetNames<T>().Append("\0").Conjoin('\0');

    /// <summary>Adds padding.</summary>
    void Pad(string? text, string? copy)
    {
        if (copy is not null && !FrozenSortedDictionary.Comparer.Equals(text, copy) ||
            UiPadding is not [var first, var second])
            return;

        ImGui.Dummy(new(first, second));
        ImGui.SameLine();
    }

    /// <summary>Displays the preferences tab.</summary>
    void ShowPreferences()
    {
        if (!ImGui.BeginTabItem("Preferences"))
            return;

        ShowLayout();
        ShowBehavior();
        ShowStyle();
        ShowPath();
        ImGui.EndTabItem();
    }

    /// <summary>Shows the layout header and options.</summary>
    void ShowLayout()
    {
        ImGui.SeparatorText("Layout");
        _ = ImGui.Checkbox("Tabs instead of separate windows", ref _useTabs);
        _ = ImGui.Checkbox("Always show chat", ref _alwaysShowChat);

        if (_alwaysShowChat)
            _ = ImGui.Checkbox("Chat window on side", ref _sideBySide);

        Slider("Autocomplete suggestion count", ref _suggestions, 0, 20, "%.0f", 500);
    }

    /// <summary>Shows the behavior header and options.</summary>
    void ShowBehavior()
    {
        ImGui.SeparatorText("Behavior");
        _ = ImGui.Checkbox("Hold to confirm location release", ref _holdToConfirm);

        if (!_alwaysShowChat)
            ImGui.Checkbox("Move to chat tab when releasing", ref _moveToChatTab);

        if (OperatingSystem.IsFreeBSD() || OperatingSystem.IsLinux())
            _ = ImGui.Checkbox("Push notifications for new items", ref _desktopNotifications);
    }

    /// <summary>Shows the style header and options.</summary>
    void ShowStyle()
    {
        ImGui.SeparatorText("Style");
        Slider("UI Scale", ref _uiScale, 0.4f, 2, "%.2f");

        Vector2 v = new(UiPadding[0], UiPadding[1]);
        ImGui.SetNextItemWidth(Width(250));
        ImGui.SliderFloat2("UI Padding", ref v, 0, 20, "%.1f", ImGuiSliderFlags.AlwaysClamp);
        (UiPadding[0], UiPadding[1]) = (v.X, v.Y);

        Slider("UI Rounding", ref _uiRounding, 0, 30);
        Slider("UI Spacing", ref _uiSpacing, 0, 20);

        Slider("Window Dim", ref _windowDim, 1, 20, "%.2f");
        Slider("Inactive Dim", ref _inactiveTabDim, 1, 20, "%.2f");
        Slider("Active Dim", ref _activeTabDim, 1, 20, "%.2f");

        var (fontSize, language) = (FontSize, _language);
        Slider("Font Size", ref fontSize, 8, 72, "%.0f");
        ImGui.SetNextItemWidth(Width(250));
        _ = Combo("Font Language", ref language, s_languages, false);
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        s_requiresRestart |= FontSize != fontSize || _language != language;
        (FontSize, _language) = (fontSize, language);

        if (s_requiresRestart && ImGui.Button("Restart"))
        {
#if !ANDROID
            if (Environment.ProcessPath is { } path)
            {
                ChildProcess?.Dispose();
                ChildProcess = Process.Start(path);
            }
            else
#endif // ReSharper disable once BadPreprocessorIndent
                Environment.Exit(0);
        }

        ShowText("Press on the color for more options!", disabled: true);

        if (ImGui.CollapsingHeader("Theme"))
            for (var i = 0; i < Colors.Count.Min((int)AppPalette.Count); i++)
                ShowColor(i);

        if (!ImGui.CollapsingHeader("Theme (Advanced)"))
            return;

        for (var i = (int)AppPalette.Count; i < Colors.Count; i++)
            ShowColor(i);
    }

    /// <summary>Shows the path header and options.</summary>
    void ShowPath()
    {
        const string
            DirectoryMessage =
                """
                This is where manual worlds are read from.
                If left empty, the default installation
                path of Archipelago is used.
                """,
            GitRepoMessage =
                """
                This is used for manual worlds that use the Hooks feature
                that depend on the Archipelago source code. Ensure that
                setup.py has been executed on the repository. If left empty,
                manual worlds using Hooks are treated as non-manual worlds,
                meaning all manual-specific features will be disabled.
                """,
            PythonMessage =
                """
                This is used for manual worlds that use the Hooks feature
                that depend on the Archipelago source code. If left empty,
                python is assumed to be installed in PATH. This setting
                does nothing if the above setting isn't specified.
                """;

        ImGui.SeparatorText("Path");
        ImGui.SetNextItemWidth(Width(300));
        _ = ImGuiRenderer.InputTextWithHint("AP Directory", DefaultDirectory, ref _directory, ushort.MaxValue);
        ShowHelp(DirectoryMessage);
        ImGui.SetNextItemWidth(Width(300));
        _ = ImGuiRenderer.InputText("AP Git Repo", ref _repo, ushort.MaxValue);
        ShowHelp(GitRepoMessage);
        ImGui.SetNextItemWidth(Width(300));
        _ = ImGuiRenderer.InputTextWithHint("Python", "python", ref _python, ushort.MaxValue);
        ShowHelp(PythonMessage);
    }

    /// <summary>Helper method for displaying a slider.</summary>
    /// <param name="title">The title.</param>
    /// <param name="amount">The current amount.</param>
    /// <param name="min">The minimum.</param>
    /// <param name="max">The maximum.</param>
    /// <param name="format">The format to display.</param>
    /// <param name="width">The amount of space to leave for the title.</param>
    void Slider(string title, ref float amount, float min, float max, string format = "%.1f", int width = 250)
    {
        ImGui.SetNextItemWidth(Width(width));
        _ = ImGui.SliderFloat(title, ref amount, min, max, format, ImGuiSliderFlags.AlwaysClamp);
    }

    /// <summary>Sanitizes the port and colors.</summary>
    void Sanitize()
    {
        if (_port is < 1024 or > ushort.MaxValue)
            _port = DefaultPort;

        if (Colors.Count < s_defaultColors.Length)
            Colors = [..Colors, ..s_defaultColors.AsSpan()[Colors.Count..]];

        if (UiPadding is not [_, _])
            UiPadding = [5, 5];
    }

    /// <summary>Shows the paste field.</summary>
    /// <param name="clients">The clients created from history, or <see langword="null"/>.</param>
    void ShowPasteTextField(ref IEnumerable<Client>? clients)
    {
        ImGui.SetNextItemWidth(Width(100));

        var enter = ImGuiRenderer.InputTextWithHint(
            "Path",
            "Paste the YAML path here and hit enter, or...",
            ref _yamlFilePath,
            ushort.MaxValue,
            TextFlags
        );

        if (!enter || string.IsNullOrWhiteSpace(_yamlFilePath) || !File.Exists(_yamlFilePath))
            return;

        clients = Client.FromFile(_yamlFilePath, this);
        _yamlFilePath = "";
    }

    /// <summary>Shows the color editor for the index.</summary>
    /// <param name="i">The index from <see cref="Colors"/> to display and mutate.</param>
    void ShowColor(int i)
    {
        ImGui.Separator();

        var name = i < (int)AppPalette.Count
            ? ((AppPalette)i).ToString()
            : ((ImGuiCol)(i - (int)AppPalette.Count)).ToString();

        Colors[i] = ShowColorEdit(name, Colors[i]);
    }

    /// <summary>Shows the connection tab.</summary>
    /// <param name="clients">The list of clients to show.</param>
    /// <param name="clientsToRegister">The clients created from history, or <see langword="null"/>.</param>
    /// <returns>Whether to create a new <see cref="Client"/>.</returns>
    bool ShowConnectionTab(List<Client> clients, out IEnumerable<Client>? clientsToRegister)
    {
        clientsToRegister = null;

        if (!ImGui.BeginTabItem("Connection"))
            return false;

        ImGui.SeparatorText("Host");
        ImGui.SetNextItemWidth(Width(450));
        _ = ImGuiRenderer.InputTextWithHint("Address", DefaultAddress, ref _address, ushort.MaxValue, TextFlags);
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
        ShowText("Drop a YAML file to start playing, or...", disabled: true);
        ShowPasteTextField(ref clientsToRegister);
        Sanitize();
        var ret = ImGui.Button("Enter slot manually") || enter;
        ImGui.SeparatorText("History");
        ImGui.SetNextItemWidth(Width(100));
        _ = Combo("Sort", ref _sortHistoryBy, s_historyOrder, false);

        var message = _list.History.Count is 0
            ? "Join a game for buttons to appear here!"
            : "Left click to join. Right click to delete.";

        ShowText(message, disabled: true);
        ShowHelp(UpdateHostOrPortMessage);

        for (var i = 0; i < _list.History.Count && CollectionsMarshal.AsSpan(_list.History) is var history; i++)
        {
            ref var current = ref history[i];

            if (current.IsInvalid || history[..i].Contains(current))
                _list.History.RemoveAt(i--);
        }

        var query = _list.History.ToArray()
           .Where(x => clients.TrueForAll(y => !y.Has(x)))
           .GroupBy(x => (x.Alias, x.Host, x.Port), s_equality);

        clientsToRegister = Order(query)
           .SelectMany(ShowHistoryHeader)
           .Filter()
#pragma warning disable IDE0305
           .ToList();
#pragma warning restore IDE0305
        ImGui.EndTabItem();
        return ret;
    }

    /// <summary>Shows the connection.</summary>
    /// <param name="historyEntry">The connection to show.</param>
    /// <returns>Whether the button was clicked.</returns>
    bool ShowHistoryButton(HistoryEntry historyEntry)
    {
        var ret = ImGui.Button($"{historyEntry.Name}###{historyEntry.Host}:|{historyEntry.Port}:|{historyEntry.Name}");

        if (ImGui.IsItemClicked(ImGuiMouseButton.Middle) || ImGui.IsItemClicked(ImGuiMouseButton.Right))
            _list.History.Remove(historyEntry);

        return ret;
    }

    /// <summary>Shows the clients.</summary>
    /// <param name="gameTime">The time elapsed.</param>
    /// <param name="clients">The clients to show.</param>
    // ReSharper disable once SuggestBaseTypeForParameter
    int? Show(GameTime gameTime, List<Client> clients)
    {
        int? ret = null;
        ShownTooltip = false;

        for (var i = clients.Count - 1; clients.Nth(i) is { } client; i--)
        {
            if (client.Draw(gameTime, this, out var selected))
                clients.RemoveAt(i);

            if (selected)
                ret = i;
        }

        return ret;
    }

    /// <summary>Connects to the server using the <see cref="HistoryEntry"/> instance.</summary>
    /// <param name="historyEntry">The connection to use.</param>
    /// <returns>The <see cref="Client"/> created based on the parameter <paramref name="historyEntry"/>.</returns>
    Client ConnectAndReturn(HistoryEntry historyEntry)
    {
        Client client = new(historyEntry);
        client.Connect(this, historyEntry.Host ?? Address, historyEntry.Port, historyEntry.Password);
        return client;
    }

    /// <summary>Shows the group of history.</summary>
    /// <param name="group">The group of history.</param>
    /// <returns>The clients created.</returns>
    IEnumerable<Client> ShowHistoryHeader(ConnectionGroup group)
    {
        if (group.ToIList() is not [var f, ..] connection)
            return [];

        var count = $"{(connection.Count is 1 ? "" : $" ({connection.Count})")}";
        var id = $"{f.Host}:{f.Port}";
        ImGui.SetNextItemWidth(Width(100));
        ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(_editStates, id, out _);
        var label = $"{(string.IsNullOrWhiteSpace(f.Alias) ? id : f.Alias)}{count}###{id}";

        if (!ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.SpanTextWidth))
            return [];

        ImGui.SameLine(0, 15);
        _ = ImGui.Checkbox($"Edit###Edit:|{id}", ref state);
        var oldAlias = f.GetAliasOrEmpty();
        var newAlias = oldAlias;
        ImGui.SetNextItemWidth(Width(100));

        if (state &&
            ImGuiRenderer.InputTextWithHint(
                $"Alias###Alias:|{id}",
                "Type here to change the name, then hit enter to confirm...",
                ref newAlias,
                ushort.MaxValue,
                TextFlags
            ) &&
            !(state = false) &&
            !FrozenSortedDictionary.Comparer.Equals(oldAlias, newAlias) &&
            f with { Alias = newAlias } is var alias)
            Sync(ref alias);

        return Order(connection).Where(ShowHistoryButton).Select(ConnectAndReturn);
    }

    /// <summary>Orders the connections.</summary>
    /// <param name="cs">The connections.</param>
    /// <returns>The ordered enumerable of the parameter <paramref name="cs"/>.</returns>
    IOrderedEnumerable<HistoryEntry> Order(IEnumerable<HistoryEntry> cs) =>
        SortHistoryBy switch
        {
            HistoryOrder.Date => cs.OrderBy(_list.History.IndexOf),
            HistoryOrder.Name => cs.OrderBy(x => x.Name, FrozenSortedDictionary.Comparer),
            var x => throw new ArgumentOutOfRangeException(nameof(cs), x, null),
        };

    /// <summary>Orders the connection group.</summary>
    /// <param name="cs">The connections.</param>
    /// <returns>The ordered enumerable of the parameter <paramref name="cs"/>.</returns>
    IEnumerable<ConnectionGroup> Order(IEnumerable<ConnectionGroup> cs) =>
        SortHistoryBy switch
        {
            HistoryOrder.Date => cs.OrderBy(_list.Find),
            HistoryOrder.Name =>
                cs.OrderBy(x => x.Key.Alias ?? x.Key.Host, FrozenSortedDictionary.Comparer).ThenBy(x => x.Key.Port),
            _ => [],
        };
}
