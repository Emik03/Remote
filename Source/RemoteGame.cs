namespace Remote;

/// <summary>Sets up the environment to display <see cref="Client"/>.</summary>
[CLSCompliant(false)]
public sealed class RemoteGame : Game
{
    /// <summary>The amount of <see cref="Client"/> instances that can run at once.</summary>
    const int Limit = 1024;

    /// <summary>The bridge between <see cref="ImGui"/> and SDL.</summary>
    readonly ImGuiRenderer _renderer;

    /// <summary>The list of windows to draw.</summary>
    readonly List<Client> _clients = [];

    /// <summary>The current user preferences.</summary>
    readonly Preferences _preferences = Preferences.Load();

    /// <summary>The pointer to the byte string on the ini location.</summary>
    nint _ini;

    /// <summary>Initializes a new instance of the <see cref="Game"/> class.</summary>
    public RemoteGame()
    {
        new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1600,
            PreferredBackBufferHeight = 900,
            SynchronizeWithVerticalRetrace = false,
        }.ApplyChanges();

        _renderer = new(this);
        var io = ImGui.GetIO();
        AddFont(io);
        SpecifyIniFilePath(io);
        _renderer.RebuildFontAtlas();
        IsFixedTimeStep = false;
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
#if !ANDROID
        Window.FileDrop += OnFileDrop;
#endif
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderer.Dispose();
            _preferences.Save();

            if (_ini is not 0)
                Marshal.FreeHGlobal(_ini);

            _ini = 0;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        if (_renderer.IsDisposed)
            return;

        GraphicsDevice.Clear(_preferences[AppPalette.Background].XnaColor);
        _renderer.BeforeLayout(gameTime);
        _preferences.PushStyling();

        if (ImGui.Begin("Main", ImGuiWindowFlags.HorizontalScrollbar) && _preferences.Show())
            _ = Add(new());

        ImGui.End();

        for (var i = _clients.Count - 1; i >= 0 && _clients[i] is var client; i--)
            if (client.Draw(gameTime, _preferences))
                _clients.RemoveAt(i);

        _preferences.PopStyling();
        _renderer.AfterLayout();
    }

    /// <summary>Attempts to add the font.</summary>
    /// <param name="io">The IO.</param>
    static unsafe void AddFont(ImGuiIOPtr io)
    {
        if (typeof(RemoteGame).Assembly.GetManifestResourceStream("Remote.main.ttf") is { } stream &&
            new byte[285000] is var font &&
            font.Length == stream.Read(font))
            fixed (byte* ptr = font)
                _ = io.Fonts.AddFontFromMemoryTTF((nint)ptr, font.Length, 36);
    }

    /// <summary>
    /// Called when a file is dropped on the window. Adds <see cref="Client"/> for each slot deserialized.
    /// </summary>
    /// <param name="__">Discard.</param>
    /// <param name="e">The files that were dropped.</param>
    void OnFileDrop(object? __, FileDropEventArgs e) =>
        _ = e.Files.All(x => Client.FromFile(x, _preferences).All(Add));

    /// <summary>Sets <see cref="ImGuiIO.IniFilename"/></summary>
    /// <param name="io">The IO.</param>
    unsafe void SpecifyIniFilePath(ImGuiIOPtr io) =>
        io.NativePtr->IniFilename = (byte*)(_ini =
            Marshal.StringToHGlobalAnsi(Path.Join(Path.GetDirectoryName(Preferences.FilePath), "imgui.ini")));

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
