// SPDX-License-Identifier: MPL-2.0
namespace Remote;

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
    [CLSCompliant(false), StructLayout(LayoutKind.Auto)]
    public readonly record struct Connection(string? Name, string? Password, string? Host, ushort Port, string? Game)
        : ISpanParsable<Connection>
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
        /// <param name="yaml">The yaml to deconstruct.</param>
        /// <param name="password">The password of the game.</param>
        /// <param name="host">The host.</param>
        /// <param name="port">The port of the host.</param>
        public Connection(Yaml yaml, string? password, string? host, ushort port)
            : this(yaml.Name, password, host, port, yaml.Game) { }

        /// <summary>Determines whether this instance is invalid, usually from default construction.</summary>
        public bool IsInvalid => Name is null || Host is null || Port is 0 || Game is null;

        /// <inheritdoc />
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Connection result) =>
            TryParse(s.AsSpan(), provider, out result);

        /// <inheritdoc />
        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Connection result)
        {
            var (first, (second, (game, _))) = s.SplitOn('@');
            var (slot, (password, _)) = first.SplitOn(':');
            var (host, (portSpan, _)) = second.SplitOn(':');

            if (!ushort.TryParse(portSpan, NumberStyles.Any, provider, out var port))
            {
                result = default;
                return false;
            }

            var gameStr = game.ToString();
            var slotStr = slot.ToString();
            var passwordStr = password.ToString();
            var hostStr = host.ToString();

            if (File.Exists(gameStr) &&
                Go(Yaml.FromFile, gameStr, out _, out var yaml) &&
                yaml?.FirstOrDefault(x => x.Name == slotStr) is { } y)
                result = new(y, passwordStr, hostStr, port);

            result = new(slotStr, passwordStr, hostStr, port, gameStr);
            return true;
        }

        /// <inheritdoc />
        public static Connection Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

        /// <inheritdoc />
        public static Connection Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            TryParse(s, provider, out var result);
            return result;
        }

        /// <summary>Gets the string representation for displaying as text.</summary>
        /// <returns>The string representation.</returns>
        public string ToDisplayString() => $"{Name} ({Host}:{Port})";

        /// <inheritdoc />
        public override string ToString() => $"{Name}:{Password}@{Host}:{Port}@{Game}";

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

    /// <summary>Contains the history.</summary>
    readonly Connection.List _list = Connection.List.Load();

    /// <summary>Whether to use tabs or separate windows.</summary>
    bool _useTabs = true;

    /// <summary>Contains the current port.</summary>
    int _language, _port;

    /// <summary>Contains the current UI settings.</summary>
    float _fontSize = 36, _uiScale = 0.75f, _uiPadding = 6, _uiRounding = 4, _uiSpacing = 6;

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

    /// <summary>Gets or sets the value determining whether to use tabs.</summary>
    public bool UseTabs
    {
        get => _useTabs;
        [UsedImplicitly] private set => _useTabs = value;
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
#pragma warning disable MA0016
    /// <summary>Gets the list of colors.</summary>
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
    public void PushStyling()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(_uiScale * 600));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, _useTabs ? 0 : 5);

        for (var i = (int)AppPalette.Count; i < Colors.Count; i++)
            ImGui.PushStyleColor((ImGuiCol)(i - AppPalette.Count), Colors[i]);

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

    /// <summary>Shows the preferences window.</summary>
    /// <param name="gameTime">The time elapsed.</param>
    /// <param name="clients">The list of clients to show.</param>
    /// <param name="clientsToRegister">The clients created from history, or <see langword="null"/>.</param>
    /// <returns>Whether to create a new instance of <see cref="Client"/>.</returns>
    [CLSCompliant(false)]
    public bool Show(GameTime gameTime, IList<Client> clients, out IEnumerable<Client>? clientsToRegister)
    {
        clientsToRegister = null;

        if (!ImGui.BeginTabBar("Tabs"))
        {
            if (!_useTabs)
                Show(gameTime, clients);

            return false;
        }

        ImGui.SetWindowFontScale(UiScale);
        var ret = ShowConnectionTab(out clientsToRegister);
        ShowSettings();

        if (_useTabs)
            Show(gameTime, clients);

        ImGui.EndTabBar();

        if (!_useTabs)
            Show(gameTime, clients);

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
    void Show(GameTime gameTime, IList<Client> clients)
    {
        for (var i = clients.Count - 1; i >= 0 && clients[i] is var c; i--)
            if (c.Draw(gameTime, clients.Count - i - 1, this))
                clients.RemoveAt(i);
    }

    /// <summary>Displays the settings tab.</summary>
    void ShowSettings()
    {
        const string Hint = "Only required for manual worlds that use hooks";

        if (!ImGui.BeginTabItem("Settings"))
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
        ImGui.SeparatorText("UI Settings");
        Slider("UI Scale", ref _uiScale, 0.4f, 2, "%.2f");
        Slider("UI Padding", ref _uiPadding, 0, 20);
        Slider("UI Rounding", ref _uiRounding, 0, 30);
        Slider("UI Spacing", ref _uiSpacing, 0, 20);
        ImGui.SetNextItemWidth(Width(250));
        ImGui.Checkbox("Tabs instead of separate windows", ref _useTabs);
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

        var v = Colors[i].Vector;
        _ = ImGui.ColorEdit4(name, ref v, ImGuiColorEditFlags.DisplayHex);
        _ = ImGui.ColorEdit4($"##{name}", ref v, ImGuiColorEditFlags.DisplayRGB | ImGuiColorEditFlags.NoSmallPreview);
        Colors[i] = new(v);
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
            else if (ImGui.Button(current.ToDisplayString()))
            {
                Client client = new(current);

                if (!client.Connect(this, current.Host ?? Address, current.Port, current.Password) &&
                    client.Connect(this, Address, Port, Password))
                    current = current with { Host = Address, Port = Port, Password = Password };

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
