// SPDX-License-Identifier: MPL-2.0
namespace Remote;

using JsonIgnoreAttribute = System.Text.Json.Serialization.JsonIgnoreAttribute;
using JsonSerializer = System.Text.Json.JsonSerializer;
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

    /// <summary>Holds a previous connection info.</summary>
    /// <param name="Name">The slot.</param>
    /// <param name="Password">The password of the game.</param>
    /// <param name="Host">The host.</param>
    /// <param name="Port">The port of the host.</param>
    /// <param name="Game">The game.</param>
    /// <param name="Locations">The checked locations.</param>
    /// <param name="Color">The color for the tab or window.</param>
    [CLSCompliant(false), StructLayout(LayoutKind.Auto)]
    public readonly record struct Connection(
        string? Name,
        string? Password,
        string? Host,
        ushort Port,
        string? Game,
        ImmutableHashSet<string>? Locations,
        string? Color
    )
    {
        /// <summary>Contains the list of connections.</summary>
        /// <param name="History">The history.</param>
        // ReSharper disable MemberHidesStaticFromOuterClass
#pragma warning disable MA0016
        public readonly record struct List(List<Connection> History)
#pragma warning restore MA0016
        {
            /// <summary>The default host address that hosts Archipelago games.</summary>
            const string HistoryFile = "history.json";

            /// <summary>Contains the path to the preferences file to read and write from.</summary>
            public static string FilePath { get; } = PathTo(HistoryFile, "REMOTE_HISTORY_PATH");

            /// <summary>Loads the history from disk.</summary>
            /// <returns>The preferences.</returns>
            public static List Load()
            {
                if (File.Exists(FilePath) && !Go(Deserialize, out _, out var fromDisk) && fromDisk is not null)
                    return new(fromDisk);

                var directory = Path.GetDirectoryName(FilePath);
                Debug.Assert(directory is not null);
                System.IO.Directory.CreateDirectory(directory);
                List fromMemory = new([]);
                fromMemory.Save();
                return fromMemory;
            }

            /// <summary>Writes this instance to disk.</summary>
            public void Save() =>
                File.WriteAllText(
                    FilePath,
                    JsonSerializer.Serialize(History, RemoteJsonSerializerContext.Default.ListConnection)
                );

            static List<Connection>? Deserialize() =>
                JsonSerializer.Deserialize<List<Connection>>(
                    File.OpenRead(FilePath),
                    RemoteJsonSerializerContext.Default.ListConnection
                );
        }

        /// <summary>Initializes a new instance of the <see cref="Connection"/> struct.</summary>
        /// <param name="connection">The connection to copy.</param>
        /// <param name="locations">The locations to inherit.</param>
        /// <param name="color">The color for the tab or window.</param>
        public Connection(Connection connection, IEnumerable<string>? locations, string? color = null)
            : this(
                connection.Name,
                connection.Password,
                connection.Host,
                connection.Port,
                connection.Game,
                connection.GetLocationsOrEmpty().Union(locations ?? []),
                color
            ) { }

        /// <summary>Initializes a new instance of the <see cref="Connection"/> struct.</summary>
        /// <param name="yaml">The yaml to deconstruct.</param>
        /// <param name="password">The password of the game.</param>
        /// <param name="host">The host.</param>
        /// <param name="port">The port of the host.</param>
        /// <param name="color">The color for the tab or window.</param>
        public Connection(Yaml yaml, string? password, string? host, ushort port, string? color = null)
            : this(yaml.Name, password, host, port, yaml.Game, [], color) { }

        /// <summary>Determines whether this instance is invalid, usually from default construction.</summary>
        [JsonIgnore]
        public bool IsInvalid => Name is null || Host is null || Port is 0 || Game is null;

        /// <inheritdoc />
        public bool Equals(Connection other) =>
            Port == other.Port &&
            StringComparer.Ordinal.Equals(Name, other.Name) &&
            StringComparer.Ordinal.Equals(Game, other.Game) &&
            StringComparer.Ordinal.Equals(Host, other.Host) &&
            StringComparer.Ordinal.Equals(Password ?? "", other.Password ?? "");

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Port, Name, Game, Host, Password ?? "");

        /// <inheritdoc />
        public override string ToString() => $"{Name} ({Host}:{Port})";

        /// <summary>Gets the locations.</summary>
        /// <returns>The locations.</returns>
        public ImmutableHashSet<string> GetLocationsOrEmpty() => Locations ?? ImmutableHashSet<string>.Empty;

        /// <summary>Converts this instance to the equivalent <see cref="Yaml"/> instance.</summary>
        /// <returns>The <see cref="Yaml"/> instance, or <see langword="null"/> if none found on disk.</returns>
        public Yaml ToYaml() =>
            new()
            {
                Game = Game ?? "",
                Name = Name ?? "",
            };
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
    const string DefaultAddress = "archipelago.gg", PreferencesFile = "preferences.cfg";

    /// <summary>Gets the languages.</summary>
    static readonly string s_languages = Enum.GetNames<Language>().Append("\0").Conjoin('\0');

    /// <summary>Gets the <see cref="ImGuiCol"/> set that represents background colors.</summary>
    static readonly FrozenSet<ImGuiCol> s_backgrounds = Enum.GetValues<ImGuiCol>()
       .Where(x => x.ToString().EndsWith("BG", StringComparison.OrdinalIgnoreCase))
       .ToFrozenSet();

    /// <summary>Contains the history.</summary>
    readonly Connection.List _list = Connection.List.Load();

    /// <summary>Whether to use tabs or separate windows.</summary>
    bool _holdToConfirm = true, _useTabs = true, _moveToChatTab = true;

    /// <summary>Contains the current port.</summary>
    int _language, _port;

    /// <summary>Contains the current UI settings.</summary>
    float _activeTabDim = 1.25f,
        _windowDim = 3.75f,
        _fontSize = 36,
        _inactiveTabDim = 2.5f,
        _uiScale = 0.75f,
        _uiPadding = 6,
        _uiRounding = 4,
        _uiSpacing = 6;

    /// <summary>Contains the current text field values.</summary>
    string _address = DefaultAddress,
        _directory = DefaultDirectory,
        _password = "",
        _python = "",
        _repo = "",
        _yamlFilePath = "";

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
        Environment.GetFolderPath(
            OperatingSystem.IsWindows()
                ? Environment.SpecialFolder.CommonApplicationData
                : Environment.SpecialFolder.LocalApplicationData
        ),
        "Archipelago"
    );

    /// <summary>Contains the path to the preferences file to read and write from.</summary>
    public static string FilePath { get; } = PathTo(PreferencesFile, "REMOTE_PREFERENCES_PATH");

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

    /// <summary>Gets or sets the active tab dim.</summary>
    public float ActiveTabDim
    {
        get => _activeTabDim;
        [UsedImplicitly] private set => _activeTabDim = value;
    }

    /// <summary>Gets or sets the window dim.</summary>
    public float WindowDim
    {
        get => _windowDim;
        [UsedImplicitly] private set => _windowDim = value;
    }

    /// <summary>Gets or sets the inactive tab dim.</summary>
    public float InactiveTabDim
    {
        get => _inactiveTabDim;
        [UsedImplicitly] private set => _inactiveTabDim = value;
    }

    /// <summary>Gets or sets the UI scaling.</summary>
    public float FontSize
    {
        get => _fontSize;
        [UsedImplicitly] private set => _fontSize = value;
    }

    /// <summary>Gets or sets the UI scaling.</summary>
    public float UiScale
    {
        get => _uiScale;
        [UsedImplicitly] private set => _uiScale = value;
    }

    /// <summary>Gets or sets the UI padding.</summary>
    public float UiPadding
    {
        get => _uiPadding;
        [UsedImplicitly] private set => _uiPadding = value;
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

    /// <summary>Gets or sets the font language.</summary>
    public Language FontLanguage
    {
        get => (Language)_language;
        [UsedImplicitly] private set => _language = (int)value;
    }

    /// <summary>Shows the color edit widget.</summary>
    /// <param name="name">The displayed text.</param>
    /// <param name="color">The color that will change.</param>
    /// <returns>The new color.</returns>
    public static AppColor ShowColorEdit(string name, AppColor color)
    {
        var v = color.Vector;
        ImGui.ColorEdit4(name, ref v, ImGuiColorEditFlags.DisplayHex);
        ImGui.ColorEdit4($"##{name}", ref v, ImGuiColorEditFlags.DisplayHSV | ImGuiColorEditFlags.NoSmallPreview);
        return new(v);
    }

    /// <summary>Gets the list of colors.</summary>
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
        fromMemory.Save();
        return fromMemory;
    }

    /// <summary>Prepends the element to the beginning of the history.</summary>
    /// <param name="connection">The connection to prepend.</param>
    [CLSCompliant(false)]
    public void Prepend(Connection connection) => _list.History.Insert(0, connection);

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

        Vector2 padding = new(_uiPadding);

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

    /// <summary>Synchronizes the connection with the one found within the internal collection.</summary>
    /// <param name="connection">The connection to synchronize.</param>
    [CLSCompliant(false)]
    public void Sync(ref Connection connection)
    {
        string FindNextAvailableColor()
        {
            foreach (var color in Sron)
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

            return Sron.PickRandom().ToString();
        }

        var history = CollectionsMarshal.AsSpan(_list.History);

        for (var i = 0; i < history.Length; i++)
        {
            ref var current = ref history[i];

            if (string.IsNullOrWhiteSpace(connection.Color) && string.IsNullOrWhiteSpace(current.Color))
                current = current with { Color = FindNextAvailableColor() };

            if (!connection.Equals(current))
                continue;

            connection = new(connection, current.GetLocationsOrEmpty(), connection.Color ?? current.Color);
            current = connection;
        }
    }

    /// <summary>Shows the preferences window.</summary>
    /// <param name="gameTime">The time elapsed.</param>
    /// <param name="clients">The list of clients to show.</param>
    /// <param name="tab">The selected tab.</param>
    /// <param name="clientsToRegister">The clients created from history, or <see langword="null"/>.</param>
    /// <returns>Whether to create a new instance of <see cref="Client"/>.</returns>
    [CLSCompliant(false)]
    public bool Show(GameTime gameTime, IList<Client> clients, out int? tab, out IEnumerable<Client>? clientsToRegister)
    {
        clientsToRegister = null;

        if (!ImGui.BeginTabBar("Tabs"))
        {
            tab = _useTabs ? null : Show(gameTime, clients);
            return false;
        }

        ImGui.SetWindowFontScale(UiScale);
        var ret = ShowConnectionTab(out clientsToRegister);
        ShowPreferences();

        if (_useTabs)
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
            Language.English => $"{nameof(Remote)}.Fonts.alt.ttf",
            Language.Japanese => $"{nameof(Remote)}.Fonts.japanese.ttf",
            Language.Korean => $"{nameof(Remote)}.Fonts.korean.ttf",
            Language.Thai => $"{nameof(Remote)}.Fonts.thai.ttf",
            _ => $"{nameof(Remote)}.Fonts.main.ttf",
        };

        if (typeof(RemoteGame).Assembly.GetManifestResourceStream(resource) is not { } stream)
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
            return io.Fonts.AddFontFromMemoryTTF((nint)ptr, font.Length, _fontSize.Clamp(8, 72), 0, ranges);
    }

    /// <summary>Gets the size of a child window.</summary>
    /// <param name="margin">The margin.</param>
    /// <returns>The size to use.</returns>
    public Vector2 ChildSize(int margin = 150) => ImGui.GetContentRegionAvail() - new Vector2(0, UiScale * margin);

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

    /// <summary>Gets the full path to the file.</summary>
    /// <param name="file">The file path to get.</param>
    /// <param name="environment">The environment variable that allows users to override the return.</param>
    /// <returns>The full path to the parameter <paramref name="file"/>.</returns>
    static string PathTo(string file, string environment) =>
        Environment.GetEnvironmentVariable(environment) is { } preferences
            ? System.IO.Directory.Exists(preferences) ? Path.Join(preferences, file) : preferences
            : Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                typeof(Preferences).Assembly.GetName().Name,
                file
            );

    /// <summary>Shows the clients.</summary>
    /// <param name="gameTime">The time elapsed.</param>
    /// <param name="clients">The clients to show.</param>
    int? Show(GameTime gameTime, IList<Client> clients)
    {
        int? ret = null;

        for (var i = clients.Count - 1; i >= 0 && clients[i] is var c; i--)
        {
            if (c.Draw(gameTime, this, out var v))
                clients.RemoveAt(i);

            if (v)
                ret = i;
        }

        return ret;
    }

    /// <summary>Displays the preferences tab.</summary>
    void ShowPreferences()
    {
        const string Hint = "Only required for manual worlds that use hooks";

        if (!ImGui.BeginTabItem("Preferences"))
            return;

        ImGui.SeparatorText("Paths");
        ImGui.SetNextItemWidth(Width(250));

        _ = ImGuiRenderer.InputTextWithHint(
            "AP Directory",
            DefaultDirectory,
            ref _directory,
            ushort.MaxValue
        );

        ImGui.SetNextItemWidth(Width(250));
        _ = ImGuiRenderer.InputTextWithHint("AP Git Repo", Hint, ref _repo, ushort.MaxValue);
        ImGui.SetNextItemWidth(Width(250));
        _ = ImGuiRenderer.InputTextWithHint("Python", "python", ref _python, ushort.MaxValue);
        ImGui.SeparatorText("Navigation");
        _ = ImGui.Checkbox("Tabs instead of separate windows", ref _useTabs);
        _ = ImGui.Checkbox("Move to chat tab when releasing", ref _moveToChatTab);
        _ = ImGui.Checkbox("Hold to confirm location release", ref _holdToConfirm);
        ImGui.SeparatorText("UI Settings");
        Slider("UI Scale", ref _uiScale, 0.4f, 2, "%.2f");
        Slider("UI Padding", ref _uiPadding, 0, 20);
        Slider("UI Rounding", ref _uiRounding, 0, 30);
        Slider("UI Spacing", ref _uiSpacing, 0, 20);
        ImGui.SeparatorText("Color Dimming (Only when connected)");
        Slider("Window Dim", ref _windowDim, 1, 10, "%.2f");
        Slider("Active Dim", ref _activeTabDim, 1, 10, "%.2f");
        Slider("Inactive Dim", ref _inactiveTabDim, 1, 10, "%.2f");
        ImGui.SeparatorText("Fonts (Requires Restart)");
        Slider("Font Size", ref _fontSize, 8, 72, "%.0f");
        ImGui.SetNextItemWidth(Width(250));
        _ = ImGui.Combo("Font Language", ref _language, s_languages);
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
        ImGui.SetNextItemWidth(Width(250));
        _ = ImGui.SliderFloat(title, ref amount, min, max, format, ImGuiSliderFlags.AlwaysClamp);
    }

    /// <summary>Sanitizes the port and colors.</summary>
    void Sanitize()
    {
        if (_port is < 1024 or > ushort.MaxValue)
            _port = DefaultPort;

        if (Colors.Count < s_defaultColors.Length)
            Colors = [..Colors, ..s_defaultColors.AsSpan()[Colors.Count..]];
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
    /// <param name="clients">The clients created from history, or <see langword="null"/>.</param>
    /// <returns>Whether to create a new <see cref="Client"/>.</returns>
    bool ShowConnectionTab(out IEnumerable<Client>? clients)
    {
        clients = null;

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
        ImGui.TextDisabled("Drop a YAML file to start playing, or...");
        ShowPasteTextField(ref clients);
        Sanitize();
        var ret = ImGui.Button("Enter slot manually") || enter;
        ImGui.SeparatorText("History");

        if (_list.History.Count is not 0)
            ImGui.TextDisabled("Left click to join. Right click to delete.");

        for (var i = 0; i < _list.History.Count && CollectionsMarshal.AsSpan(_list.History) is var history; i++)
        {
            ref var current = ref history[i];

            if (current.IsInvalid || history[..i].Contains(current))
                _list.History.RemoveAt(i--);
            else if (ImGui.Button(current.ToString()))
            {
                Client client = new(current);
                client.Connect(this, current.Host ?? Address, current.Port, current.Password);
                clients = [client];
            }
            else if (ImGui.IsItemClicked(ImGuiMouseButton.Middle) || ImGui.IsItemClicked(ImGuiMouseButton.Right))
                _list.History.RemoveAt(i--);
        }

        if (_list.History.Count is 0)
            ImGui.Text("Join a game for buttons to appear here!");

        ImGui.EndTabItem();
        return ret;
    }
}
