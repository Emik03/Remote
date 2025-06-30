namespace Remote;

/// <summary>Sets up the environment to display <see cref="Client"/>.</summary>
[CLSCompliant(false)]
public sealed class RemoteGame : Game
{
    /// <summary>The amount of <see cref="Client"/> instances that can run at once.</summary>
    const int Limit = 1024;

    /// <summary>Gets the main window name.</summary>
    static readonly string s_name = $"Remote ({typeof(RemoteGame).Assembly.GetName().Version?.ToConciseString()})";

    /// <summary>Provides the path to the ini file. Must be kept as instance to prevent GC.</summary>
    [UsedImplicitly]
    readonly byte[] _iniPath;

    /// <summary>The bridge between <see cref="ImGui"/> and SDL.</summary>
    readonly ImGuiRenderer _renderer;

    /// <summary>The list of windows to draw.</summary>
    readonly List<Client> _clients = [];

    /// <summary>The current user preferences.</summary>
    readonly Preferences _preferences = Preferences.Load();

    /// <summary>Contains the active tab.</summary>
    int? _tab;

    /// <summary>Keeps <see cref="_iniPath"/> pinned.</summary>
    GCHandle _iniPathPin;

    /// <summary>Initializes a new instance of the <see cref="Game"/> class.</summary>
    public RemoteGame()
    {
        new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1600,
            PreferredBackBufferHeight = 900,
            SynchronizeWithVerticalRetrace = false,
        }.ApplyChanges();

        _renderer = new(this, true);
        var io = ImGui.GetIO();
        (_iniPath, _iniPathPin) = SpecifyIniFilePath(io);
        _preferences.AddFont();
        _renderer.RebuildFontAtlas();
        IsMouseVisible = true;
        IsFixedTimeStep = false;
        Window.Title = s_name;
        Window.AllowUserResizing = true;
#if !ANDROID
        Window.FileDrop += OnFileDrop;
#endif
    }

    /// <inheritdoc />
    protected override unsafe void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderer.Dispose();
            _preferences.Save();
        }

        ImGui.GetIO().NativePtr->IniFilename = null;

        if (_iniPathPin.IsAllocated)
            _iniPathPin.Free();

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        if (_renderer.IsDisposed)
            return;

        GraphicsDevice.Clear(_preferences[AppPalette.Background].XnaColor);
        _renderer.BeforeLayout(gameTime);
        _preferences.PushStyling(_tab is { } t ? _clients[t] : null);

        const ImGuiWindowFlags OnTab = ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoBringToFrontOnFocus;

        var tab = _preferences.UseTabs ? OnTab : ImGuiWindowFlags.None;

        if (_preferences.UseTabs)
        {
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowViewport(viewport.ID);
            ImGui.SetNextWindowSize(new(GraphicsDevice.Width(), GraphicsDevice.Height()));
        }

        if (ImGui.Begin(s_name, ImGuiWindowFlags.HorizontalScrollbar | tab))
        {
            if (_preferences.Show(gameTime, _clients, out _tab, out var fromHistory))
                _ = Add(new());

            _ = fromHistory?.All(Add);
        }

        ImGui.End();
        _preferences.PopStyling();
        _renderer.AfterLayout();
    }
#if !ANDROID
    /// <summary>
    /// Called when a file is dropped on the window. Adds <see cref="Client"/> for each slot deserialized.
    /// </summary>
    /// <param name="__">Discard.</param>
    /// <param name="e">The files that were dropped.</param>
    void OnFileDrop([UsedImplicitly] object? __, FileDropEventArgs e) =>
        _ = e.Files.All(x => Client.FromFile(x, _preferences).All(Add));
#endif
    /// <summary>Sets <see cref="ImGuiIO.IniFilename"/></summary>
    /// <param name="io">The IO.</param>
    static unsafe (byte[], GCHandle) SpecifyIniFilePath(ImGuiIOPtr io)
    {
        var arr = Encoding.UTF8.GetBytes(Path.Join(Path.GetDirectoryName(Preferences.FilePath), "imgui.ini"));
        var pin = GCHandle.Alloc(arr, GCHandleType.Pinned);
        io.NativePtr->IniFilename = (byte*)pin.AddrOfPinnedObject();
        return (arr, pin);
    }

    /// <summary>Attempts to add a new client.</summary>
    /// <param name="client">The client to add.</param>
    /// <returns>Whether the operation was successful.</returns>
    bool Add(Client client)
    {
        if (_clients.Count >= Limit)
            return false;

        _clients.Add(client);
        return true;
    }
}
