// SPDX-License-Identifier: MPL-2.0
namespace Remote;

using CheckboxStatus = (Logic? Logic, Client.LocationStatus Status, bool Checked);

/// <summary>
/// Responsible for rendering the <see cref="ImGui"/> window to display its state of <see cref="ArchipelagoSession"/>.
/// </summary>
/// <param name="yaml">The yaml file used to generate the world.</param>
public sealed partial class Client(Yaml? yaml = null)
{
    /// <summary>Indicates the status of the location.</summary>
    public enum LocationStatus
    {
        /// <summary>This location has already been checked and doesn't need to be sent again.</summary>
        Checked,

        /// <summary>This location is reachable.</summary>
        Reachable,

        /// <summary>This location requires more items before it can be acquired in-logic.</summary>
        OutOfLogic,

        /// <summary>This location cannot be determined on whether it is in- or out-of-logic.</summary>
        ProbablyReachable,
    }

    /// <summary>Wraps the importance of an item and where it was obtained.</summary>
    /// <param name="Flags">The priority of the obtained item.</param>
    /// <param name="DisplayName">The item name.</param>
    /// <param name="Count">The number of times this item was obtained.</param>
    /// <param name="Locations">The locations that obtained this item.</param>
    readonly record struct ReceivedItem(
        ItemFlags? Flags,
        string? DisplayName,
        int Count,
        IReadOnlyList<(string LocationDisplayName, string LocationGame)>? Locations
    )
    {
        /// <summary>Initializes a new instance of the <see cref="ReceivedItem"/> struct.</summary>
        /// <param name="info">The <see cref="ItemInfo"/> to deconstruct.</param>
        public ReceivedItem(ICollection<ItemInfo?> info)
            : this(
                info.Nth(0)?.Flags,
                info.Nth(0)?.ItemDisplayName,
                info.Count,
                [..info.Filter().Select(x => (x.LocationDisplayName, x.LocationGame))]
            ) { }

        /// <summary>Displays this instance as text with a tooltip.</summary>
        /// <param name="preferences">The user preferences.</param>
        public void Show(Preferences preferences)
        {
            if (DisplayName is null)
                return;

            var text = $"{DisplayName}{(Count is 1 ? "" : $" ({Count})")}";
            ImGui.PushStyleColor(ImGuiCol.Text, ColorOf(Flags, preferences));
            ImGui.BulletText(text);
            CopyIfClicked(preferences, DisplayName);

            if (Locations is not null and not [] && ImGui.IsItemHovered())
                Tooltip(preferences, Locations.Select(ToString).Conjoin('\n'));

            ImGui.PopStyleColor();
        }

        /// <summary>Determines whether this instance matches the search result.</summary>
        /// <param name="search">The filter.</param>
        /// <returns>Whether this instance contains the parameter <paramref name="search"/> as a substring.</returns>
        public bool IsMatch(string search) => DisplayName?.Contains(search, StringComparison.OrdinalIgnoreCase) is true;

        /// <summary>Gets the string representation of the tuple.</summary>
        /// <param name="tuple">The tuple to get the string representation of.</param>
        /// <returns>The string representation of the parameter <paramref name="tuple"/>.</returns>
        static string ToString((string LocationDisplayName, string LocationGame) tuple) =>
            $"{tuple.LocationDisplayName} at {tuple.LocationGame}";
    }

    /// <summary>Constant strings.</summary>
    const string Fail = "The file path shown above was dropped into the window but is not a valid YAML file.",
        HelpMessage1 =
            """
            !help
                Returns the help listing
            !license
                Returns the licensing information
            !countdown seconds=10
                Start a countdown in seconds
            !options
                List all current options. Warning: lists password
            !admin [command]
                Allow remote administration of the multiworld server
                    Usage: "!admin login <password>" in order to log in to the remote interface.
                    Once logged in, you can then use "!admin <command>" to issue commands.
                    If you need further help once logged in, use "!admin help".
            !players
                Get information about connected and missing players
            !status [tag]
                Get status information about your team
                    Optionally mention a Tag name and get information on who has that Tag.
                    For example: DeathLink or EnergyLink.
            !release
                Sends remaining items in your world to their recipients.
            """,
        HelpMessage2 =
            """
            !collect
                Send your remaining items to yourself
            !remaining
                List remaining items in your game, but not their location or recipient
            !missing [filter_text]
                List all missing location checks from the server's perspective
                    Can be given text, which will be used as a filter.
            !checked
                List all done location checks from the server's perspective
                    Can be given text, which will be used as a filter.
            !alias [alias_name]
                Set your alias to the passed name
            !getitem [item_name]
                Cheat in an item, if it is enabled on this server
            !hint [item_name]
                Get a spoiler peek for an item
                    For example !hint Lamp to get a spoiler peek for that item.
                    If hint costs are on, this will only give you one new result,
                    you can rerun the command to get more in that case.
            !hint_location [location]
                Get a spoiler peek for a location
                    For example !hint_location atomic-bomb to get a spoiler peek for that location.
            """;

    /// <summary>Whether to show errors in <see cref="MessageBox.Show"/>.</summary>
    static bool s_displayErrors = true;

    /// <summary>Contains the number of instances that have been created. Used to make each instance unique.</summary>
    static int s_instances;

    /// <summary>Contains this instance's unique id.</summary>
    readonly int _instance = ++s_instances;

    /// <summary>
    /// The mapping of the names of locations to the status of said location.
    /// Meant to always be in-sync with <see cref="_sortedKeys"/>.
    /// </summary>
    readonly Dictionary<string, CheckboxStatus> _locations = new(FrozenSortedDictionary.Comparer);

    /// <summary>
    /// The sorted list containing all names of locations. Meant to always be in-sync with <see cref="_locations"/>.
    /// </summary>
    readonly List<string> _sortedKeys = [];

    /// <summary>Contains the logs of messages received.</summary>
    readonly List<LogMessage> _messages = [];

    /// <summary>Contains this player's <c>.yaml</c> file.</summary>
    readonly Yaml _yaml = yaml ?? new();

    /// <summary>Whether this client can be or has reached its goal.</summary>
    /// <remarks><para>
    /// <see langword="false"/> means this slot does not meet its goal.
    /// <see langword="null"/> means this slot can meet its goal.
    /// <see langword="true"/> means this slot has meet its goal.
    /// </para></remarks>
    bool? _canGoal = false;

    /// <summary>Contains the last index in <see cref="_messages"/> right before a release occurs.</summary>
    int _releaseIndex;

    /// <summary>Contains the window name.</summary>
    string _connectionMessage = "", _windowName = $"Client###{s_instances}";

    /// <summary>Contains all errors to display.</summary>
    string[]? _errors;

    /// <summary>The current web socket connection.</summary>
    ArchipelagoSession? _session;

    /// <summary>The logic evaluator.</summary>
    Evaluator? _evaluator;

    /// <summary>Gets the last successful connection.</summary>
    Preferences.Connection _info;

    /// <summary>The attempt to login.</summary>
    Task _connectingTask = Task.CompletedTask;

    /// <summary>The attempt to get hints.</summary>
    Task<Hint[]?> _hintTask = Task.FromResult<Hint[]?>(null);

    /// <summary>Initializes a new instance of the <see cref="Client"/> class.</summary>
    /// <param name="errors">The errors to show immediately.</param>
    public Client(string[]? errors)
        : this() =>
        _errors = errors;

    /// <summary>Initializes a new instance of the <see cref="Client"/> class.</summary>
    /// <param name="connection">The connection information.</param>
    [CLSCompliant(false)]
    internal Client(Preferences.Connection connection)
        : this(connection.ToYaml()) =>
        _info = connection;

    /// <summary>Gets or adds this location and retrieves the checkbox status of that location.</summary>
    /// <param name="key">The key to add or get.</param>
    public ref CheckboxStatus this[string key]
    {
        get
        {
            lock (_locations)
            {
                ref var value = ref CollectionsMarshal.GetValueRefOrAddDefault(_locations, key, out var exists);

                if (!exists && _sortedKeys.BinarySearch(key, FrozenSortedDictionary.Comparer) is < 0 and var i)
                    _sortedKeys.Insert(~i, key);

                return ref value;
            }
        }
    }

    /// <summary>Gets the color.</summary>
    public AppColor? Color => AppColor.TryParse(_info.Color, out var color) ? color : null;

    /// <summary>Contains the last retrieved hints.</summary>
    Hint[]? LastHints
    {
        get
        {
            Debug.Assert(_session is not null);

            if (_hintTask is { IsCompletedSuccessfully: true, Result: { } hints })
                return hints;

            if (_hintTask.IsCompleted)
                _hintTask = Task.Run(() => _session.DataStorage.GetHintsAsync());

            return null;
        }
    }

    /// <summary>Gets the players as a sequence of <see cref="Client"/> by parsing the file provided.</summary>
    /// <param name="path">The yaml file to parse.</param>
    /// <param name="preferences">The user preferences.</param>
    /// <returns>The components representing each player in the parameter <paramref name="path"/>.</returns>
    public static IEnumerable<Client> FromFile(string path, Preferences preferences) =>
        Go(Yaml.FromFile, path, out var error, out var yaml)
            ? [new(ToMessages(error, Fail, path))]
            : yaml.Select(x => new Client(x)).Lazily(x => x.Connect(preferences));

    /// <summary>Connects to the archipelago server.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <returns>Whether the connection succeeded.</returns>
    public void Connect(Preferences preferences) =>
        Connect(preferences, preferences.Address, preferences.Port, preferences.Password);

    /// <summary>Connects to the archipelago server.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="address">
    /// The address to connect to instead of the one specified in the parameter <paramref name="preferences"/>.
    /// </param>
    /// <param name="port">
    /// The port to use instead of the one specified in the parameter <paramref name="preferences"/>.
    /// </param>
    /// <param name="password">
    /// The password to authenticate with instead of the one specified in the parameter <paramref name="preferences"/>.
    /// </param>
    /// <returns>Whether the connection succeeded.</returns>
    [CLSCompliant(false)]
    public void Connect(Preferences preferences, string address, ushort port, string? password)
    {
        async Task AttemptAsync()
        {
            const ItemsHandlingFlags Flags = ItemsHandlingFlags.AllItems;

            await Task.Yield();
            var session = ArchipelagoSessionFactory.CreateSession(address, port);
            session.MessageLog.OnMessageReceived += OnMessageReceived;
            string[] tags = ["AP", nameof(Remote)];
            _connectionMessage = "Attempting new connection.\nConnecting... (1/5)";
            _ = await session.ConnectAsync();
            _yaml.EscapeName();
            _connectionMessage = "Connected!\nLogging in... (2/5)";
            var login = await session.LoginAsync(_yaml.Game, _yaml.Name, Flags, tags: tags, password: password);

            if (login is LoginFailure failure)
            {
                session.MessageLog.OnMessageReceived -= OnMessageReceived;
                _errors = failure.Errors;
                return;
            }

            _errors = null;
            _windowName = $"{_yaml.Name}###{_instance}";
            _connectionMessage = "Logged in!\nReading slot data... (3/5)";
            session.SetClientState(ArchipelagoClientState.ClientPlaying);

            foreach (var (key, value) in await session.DataStorage.GetSlotDataAsync())
                ((IDictionary<string, object?>)_yaml)[key] = value;

            _connectionMessage = "Slot data has been read!\nReading APWorld... (4/5)";
            _evaluator = Evaluator.Read(session.DataStorage, session.Items, _yaml, preferences);
            _connectionMessage = "APWorld has been read!\nSaving history in memory... (5/5)";
            _info = new(_yaml, password, address, port, _info.Color);
            preferences.Prepend(_info);
            preferences.Sync(ref _info);
            _session = session;
        }

        async Task? TryConnectAsync()
        {
            try
            {
                await AttemptAsync();
            }
            catch (Exception ex)
            {
                _errors = ["Failed to connect. Is the server down, is the host and port correct?", ..ToMessages(ex)];
            }
        }

        _connectionMessage = "";
        _connectingTask = Task.Run(TryConnectAsync);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Client { _instance: var i } && i == _instance;

    /// <inheritdoc />
    public override int GetHashCode() => _instance;

    /// <inheritdoc />
    public override string ToString() => _windowName;

    /// <summary>Whether this pair can be released.</summary>
    /// <param name="x">The pair to deconstruct.</param>
    /// <returns>Whether the parameter <paramref name="x"/> can be released.</returns>
    static bool IsReleasable(KeyValuePair<string, CheckboxStatus> x) =>
        x.Value is (_, LocationStatus.Reachable or LocationStatus.ProbablyReachable or LocationStatus.OutOfLogic, true);

    /// <summary>Converts the exception to the <see cref="string"/> array.</summary>
    /// <param name="e">The exception to convert.</param>
    /// <param name="additions">The additional strings to add before-hand.</param>
    /// <returns>The messages.</returns>
    static string[] ToMessages(Exception e, params ReadOnlySpan<string> additions) =>
    [
        ..additions,
        ..e.FindPathToNull(x => x.InnerException)
           .SelectMany(x => (IEnumerable<Exception>)[x, ..(x as AggregateException)?.InnerExceptions ?? []])
           .Select(x => x.Message),
    ];

    /// <summary>Gets the color of the flag.</summary>
    /// <param name="flags">The flag to get the color of.</param>
    /// <param name="preferences">The user preferences.</param>
    /// <returns>The color for the parameter <paramref name="flags"/>.</returns>
    static AppColor ColorOf(ItemFlags? flags, Preferences preferences) =>
        flags switch
        {
            ItemFlags.None => preferences[AppPalette.Neutral],
            { } f when f.Has(ItemFlags.Advancement) => preferences[AppPalette.Progression],
            { } f when f.Has(ItemFlags.NeverExclude) => preferences[AppPalette.Useful],
            { } f when f.Has(ItemFlags.Trap) => preferences[AppPalette.Trap],
            _ => preferences[AppPalette.PendingItem],
        };

    /// <summary>Invoked when a new message is received, adds the message to the log.</summary>
    /// <param name="message">The new message.</param>
    void OnMessageReceived(LogMessage message)
    {
        _messages.Add(message);

        if (_messages.Count < ushort.MaxValue)
            return;

        _releaseIndex--;
        _messages.RemoveAt(0);
    }

    /// <summary>Releases the locations whose checkboxes are ticked.</summary>
    void Release(Preferences preferences)
    {
        bool NeedsToBeTrackedLocally(string location)
        {
            Debug.Assert(_session is not null);

            return _session.Locations.GetLocationIdFromName(_yaml.Game, location) is not -1 and var id &&
                !_session.Locations.AllLocations.Contains(id);
        }

        long? ToId(KeyValuePair<string, CheckboxStatus> kvp)
        {
            async Task DisplayErrorAsync()
            {
                const string Title = "Archipelago Error";
                IEnumerable<string> buttons = ["Dismiss all", "Step to next error"];
                var description = $"Name of this location cannot be converted into an id by the session: {kvp.Key}";

                if (await MessageBox.Show(Title, description, buttons) is not 1)
                    s_displayErrors = false;
            }

            if (_session.Locations.GetLocationIdFromName(_yaml.Game, kvp.Key) is not -1 and var ret)
                return ret;

            if (!s_displayErrors)
                return null;
#pragma warning disable MA0134
            _ = Task.Run(DisplayErrorAsync).ConfigureAwait(false);
#pragma warning restore MA0134
            return null;
        }

        if (!IsReleasing || _isAttemptingToRelease is false or null)
            return;

        Debug.Assert(_session is not null);
        _releaseIndex = _messages.Count;
        _session.Locations.CompleteLocationChecks([.._locations.Where(x => x.Value.Checked).Select(ToId).Filter()]);
        IList<string> keys = [.._locations.Where(x => x.Value.Checked).Select(x => x.Key)];
        _isAttemptingToRelease = null;

        if (_canGoal is false && keys.Any(IsGoal))
            _canGoal = null;

        if (keys.Where(NeedsToBeTrackedLocally) is ({ } first, { } rest))
        {
            _info = new(_info, rest.Prepend(first));
            preferences.Sync(ref _info);
            (rest as IDisposable)?.Dispose();
        }

        foreach (var key in keys)
        {
            ref var location = ref this[key];

            if (location.Checked)
                location.Status = LocationStatus.Checked;
        }
    }

    /// <summary>Resets the timer.</summary>
    /// <param name="outOfLogic">Whether to use the time span for in- or out-of-logic.</param>
    void ResetTimer(bool outOfLogic) =>
        _confirmationTimer = outOfLogic ? TimeSpan.FromSeconds(4) : TimeSpan.FromSeconds(1);

    /// <summary>Invoked when logic needs to be updated. Computes what locations are reachable.</summary>
    /// <param name="_">The discard for the hook.</param>
    void UpdateStatus(ReceivedItemsHelper? _ = null)
    {
        void Update(string location, ILocationCheckHelper itemHelper)
        {
            ref var value = ref this[location];

            (value.Logic, value.Status) = 0 switch
            {
                _ when itemHelper.GetLocationIdFromName(_yaml.Game, location) is var id &&
                    itemHelper.AllLocations.Contains(id) &&
                    !itemHelper.AllMissingLocations.Contains(id) ||
                    _info.GetLocationsOrEmpty().Contains(location) =>
                    (null, LocationStatus.Checked),
                _ when _evaluator is null => (null, LocationStatus.ProbablyReachable),
                _ when _evaluator.InLogic(location) is { } logic => (logic, LocationStatus.OutOfLogic),
                _ => (null, LocationStatus.Reachable),
            };
        }

        Debug.Assert(_session is not null);
        Debug.Assert(_locationSearch is not null);
        var locationHelper = _session.Locations;

        if (_evaluator is null)
        {
            foreach (var location in locationHelper.AllLocations)
                if (locationHelper.GetLocationNameFromId(location, _yaml.Game) is { } name)
                    Update(name, locationHelper);

            return;
        }

        foreach (var (_, locations) in _evaluator.CategoryToLocations)
            foreach (var location in locations)
                Update(location, locationHelper);
    }

    /// <summary>Determines whether the location matches the current goal.</summary>
    /// <param name="location">The location.</param>
    /// <returns>Whether the parameter <paramref name="location"/> matches the current goal.</returns>
    bool IsGoal(string location) => _yaml.Goal.Equals(location, StringComparison.Ordinal);

    /// <summary>Whether the location should be visible based on the status given.</summary>
    /// <param name="location">The location.</param>
    /// <returns>Whether to make the location visible.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The parameter <paramref name="location"/> has a status whose value is without a defined name.
    /// </exception>
    bool ShouldBeVisible(string location) =>
        location.Contains(_locationSearch, StringComparison.OrdinalIgnoreCase) &&
        this[location].Status switch
        {
            LocationStatus.Reachable => true,
            LocationStatus.ProbablyReachable => true,
            LocationStatus.OutOfLogic => _showOutOfLogic,
            LocationStatus.Checked => _showAlreadyChecked,
            _ => throw new ArgumentOutOfRangeException(nameof(location), this[location].Status, null),
        };

    /// <summary>Whether the hint should be visible.</summary>
    /// <param name="hint">The hint.</param>
    /// <returns>Whether to make the parameter <paramref name="hint"/> visible.</returns>
    bool ShouldBeVisible(Hint hint)
    {
        Debug.Assert(_session is not null);

        return (!hint.Found || _showObtainedHints) &&
            (_hintIndex is 0 ? hint.FindingPlayer : hint.ReceivingPlayer) != _session.Players.ActivePlayer.Slot;
    }

    /// <summary>Gets the player name.</summary>
    /// <param name="playerSlot">The slot to get the player name.</param>
    /// <returns>The player name.</returns>
    string GetPlayerName(int playerSlot)
    {
        Debug.Assert(_session is not null);

        return _session.Players.GetPlayerAlias(playerSlot) ??
            _session.Players.GetPlayerName(playerSlot) ?? $"Player: {playerSlot}";
    }

    /// <summary>Gets the message for the hint.</summary>
    /// <param name="hint">The hint to get the message of.</param>
    /// <returns>The message representing the parameter <paramref name="hint"/>.</returns>
    string Message(Hint hint)
    {
        Debug.Assert(_session is not null);
        var findingPlayer = GetPlayerName(hint.FindingPlayer);
        var findingGame = _session.Players.GetPlayerInfo(hint.FindingPlayer)?.Game;
        var location = _session.Locations.GetLocationNameFromId(hint.LocationId, findingGame);
        var receivingPlayer = GetPlayerName(hint.ReceivingPlayer);
        var receivingGame = _session.Players.GetPlayerInfo(hint.ReceivingPlayer)?.Game;
        var item = _session.Items.GetItemName(hint.ItemId, receivingGame);
        var entrance = string.IsNullOrWhiteSpace(hint.Entrance) ? "" : $"\n({hint.Entrance})";
        return $"{receivingPlayer}'s {item}\nis at {findingPlayer}'s {location}{entrance}";
    }

    /// <summary>Groups all items into sorted items with count.</summary>
    /// <param name="category">The category that the returned items must be under.</param>
    /// <param name="lookup">The lookup table.</param>
    /// <returns>The sorted items with count.</returns>
    IEnumerable<ReceivedItem> GroupItems(string category, FrozenSortedDictionary.Element lookup)
    {
        static ReceivedItem ConsCount<T>(IGrouping<T, ItemInfo> x)
        {
            // ReSharper disable once NotDisposedResource
            var items = x.ToIList();
            return new([..items]);
        }

        bool Contains(FrozenSortedDictionary.Element lookup, ItemInfo x) =>
            x.ItemDisplayName.Contains(_itemSearch, StringComparison.OrdinalIgnoreCase) &&
#pragma warning disable MA0002
            lookup.Contains(x.ItemDisplayName);
#pragma warning restore MA0002
        Debug.Assert(_session is not null);
        Debug.Assert(_itemSearch is not null);

        var query = _session.Items.AllItemsReceived
           .Where(x => Contains(lookup, x))
           .GroupBy(x => x.ItemId)
           .Select(ConsCount);

        if (!_showYetToReceive || _evaluator is null)
            return query;

        var list = query.ToIList();

        foreach (var next in _evaluator.CategoryToItems[category])
        {
            var max = _evaluator.ItemCount.GetValueOrDefault(next, 1);

            for (var i = 0; i < list.Count && list[i] is var item; i++)
                if (next.Equals(item.DisplayName, StringComparison.Ordinal))
                    if (item.Count == max)
                        goto NoAdding;
                    else
                        list.RemoveAt(i);

            list.Add(new(null, next, max, null));
        NoAdding: ;
        }

        return list;
    }
}
