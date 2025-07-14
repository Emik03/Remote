// SPDX-License-Identifier: MPL-2.0
namespace Remote;

using HintMessage = (Hint Hint, string Message);

/// <inheritdoc cref="Client"/>
public sealed partial class Client
{
    /// <summary>The window flags.</summary>
    [CLSCompliant(false)]
    public const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.HorizontalScrollbar | ImGuiWindowFlags.NoScrollbar;

    /// <summary>The user-defined category name.</summary>
    const string UserCategorized = "(User Categorized)";

    /// <summary>The index in the combo box for which type of hints to display.</summary>
    static int s_hintIndex;

    /// <summary>Contains the list of sent messages, with the drafting message at the end.</summary>
    readonly List<string> _sentMessages = [""];

    /// <summary>Whether to show the dialog.</summary>
    bool _hasEverShownPopup,
        _showAlreadyChecked,
        _showConfirmationDialog,
        _showLocationFooter,
        _showObtainedHints,
        _showOutOfLogic,
        _showUsedItems,
        _showYetToReceive;

    /// <summary>Whether the user is attempting to release.</summary>
    /// <remarks><para>
    /// <see langword="false"/> means this slot is not attempting to release.
    /// <see langword="null"/> means this slot has released.
    /// <see langword="true"/> means this slot is attempting to release.
    /// </para></remarks>
    bool? _isAttemptingToRelease;

    /// <summary>Contains the last amount of checks.</summary>
    int _itemType, _lastItemCount = int.MinValue, _lastLocationCount = int.MaxValue, _locationSort, _sentMessagesIndex;

    /// <summary>The current state of the text field.</summary>
    string _itemSearch = "", _lastSuggestion = "", _locationSearch = "";

    /// <summary>The amount of time before release.</summary>
    TimeSpan _confirmationTimer;

    /// <summary>Gets a value indicating whether locations are being released.</summary>
    bool IsReleasing => _confirmationTimer < TimeSpan.Zero;

    /// <summary>Calls <see cref="ImGui"/> a lot.</summary>
    /// <param name="gameTime">The time elapsed since.</param>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="selected">Whether this tab is selected.</param>
    /// <returns>Whether this window is closed, and should be dequeued to allow the GC to free this instance.</returns>
    [CLSCompliant(false)]
    public bool Draw(GameTime gameTime, Preferences preferences, out bool selected)
    {
        const int Styles = 13;
        var pushedColor = AppColor.TryParse(_info.Color, out var color);

        if (pushedColor)
            preferences.PushStyling(color);

        var open = true;

        if (preferences.UseTabs)
        {
            if (!ImGui.BeginTabItem(_windowName, ref open) || !open)
            {
                if (pushedColor)
                    ImGui.PopStyleColor(Styles);

                selected = false;
                return Close(preferences, open);
            }
        }
        else if (!ImGui.Begin(_windowName, ref open, WindowFlags) || !open)
        {
            if (pushedColor)
                ImGui.PopStyleColor(Styles);

            ImGui.End();
            selected = false;
            return Close(preferences, open);
        }

        ImGui.SetWindowFontScale(preferences.UiScale);

        if (!_connectingTask.IsCompleted)
            ImGui.TextDisabled(_connectionMessage);
        else if (_session is null)
            ShowBuilder(preferences);

        if (_session is not null)
            ShowConnected(gameTime, preferences);

        if (preferences.UseTabs)
            ImGui.EndTabItem();
        else
            ImGui.End();

        if (pushedColor)
            ImGui.PopStyleColor(Styles);

        selected = true;
        return false;
    }

    /// <summary>Invokes <see cref="ImGui.BeginTabItem(string, ref bool, ImGuiTabItemFlags)"/>.</summary>
    /// <param name="name">The name of the tab item.</param>
    /// <param name="tab">The tab to check focus for.</param>
    /// <returns></returns>
    static bool BeginTabItem(string name, Tab tab)
    {
        var ret = ImGuiRenderer.BeginTabItem(
            name,
            ref Unsafe.NullRef<bool>(),
            CurrentTab == tab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None
        );

        if (ImGui.IsItemClicked())
            CurrentTab = tab;

        return ret;
    }

    /// <summary>Creates two inline buttons.</summary>
    /// <param name="first">The label of the first button.</param>
    /// <param name="second">The label of the second button.</param>
    /// <returns>
    /// <see langword="false"/> means the button with the label of the parameter <paramref name="first"/> was pushed.
    /// <see langword="null"/> means no button was pushed.
    /// <see langword="true"/> means the button with the label of the parameter <paramref name="second"/> was pushed.
    /// </returns>
    static bool? InlineButtons(string first, string second)
    {
        bool? ret = null;

        if (ImGui.Button(first))
            ret = true;

        ImGui.SameLine();

        if (ImGui.Button(second))
            ret = false;

        return ret;
    }

    /// <summary>Gets the setter for the next item open.</summary>
    /// <returns>The function that invokes <see cref="ImGui.SetNextItemOpen(bool)"/>.</returns>
    static Action GetNextItemOpenSetter() =>
        InlineButtons("Expand all", "Collapse all") switch
        {
            null => Noop,
            true => static () => ImGui.SetNextItemOpen(true),
            false => static () => ImGui.SetNextItemOpen(false),
        };

    /// <summary>Shows the components for entering slot information.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowBuilder(Preferences preferences)
    {
        ImGui.SeparatorText("Slot");
        ImGui.SetNextItemWidth(preferences.Width(100));
        _ = ImGuiRenderer.InputText("Game", ref _yaml.Game, ushort.MaxValue, Preferences.TextFlags);
        ImGui.SetNextItemWidth(preferences.Width(100));
        var enter = ImGuiRenderer.InputText("Name", ref _yaml.Name, 16, Preferences.TextFlags);

        if (_errors is not null)
            foreach (var error in _errors.AsSpan())
                if (!string.IsNullOrEmpty(error))
                    preferences.ShowText(error, AppPalette.Trap);

        ImGui.SeparatorText("Create");

        if (ImGui.Button("Connect") || enter)
            Connect(preferences);
    }

    /// <summary>Shows the components for having connected to the server.</summary>
    /// <param name="gameTime">The time elapsed since.</param>
    /// <param name="preferences">The user preferences.</param>
    void ShowConnected(GameTime gameTime, Preferences preferences)
    {
        Debug.Assert(_session is not null);
        _pushNotifs = preferences.DesktopNotifications;

        if (_session.Items.AllItemsReceived.Count is var itemCount &&
            _session.Locations.AllMissingLocations.Count is var locationCount &&
            _lastItemCount < itemCount ||
            _lastLocationCount > locationCount)
        {
            _lastItemCount = _lastLocationCount;
            _lastLocationCount = locationCount;
            UpdateStatus();
        }

        if (!ImGui.BeginTabBar("Tabs", ImGuiTabBarFlags.Reorderable))
            return;

        ShowChatTab(preferences);

        if (_session is null)
            return;

        ShowPlayerTab(preferences);
        ShowLocationTab(gameTime, preferences);
        ShowItemTab(preferences);
        ShowHintTab(preferences);
        ShowSettingsTab(preferences);
        ImGui.EndTabBar();
    }

    /// <summary>Shows the messaging client.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowChatTab(Preferences preferences)
    {
        Debug.Assert(_session is not null);
        var forced = CurrentTab is Tab.Chat || preferences is { AlwaysShowChat: false, MoveToChatTab: true } && IsReleasing;
        var flags = forced ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        var ret = !ImGuiRenderer.BeginTabItem("Chat", ref Unsafe.NullRef<bool>(), flags);

        if (ImGui.IsItemClicked())
            CurrentTab = Tab.Chat;

        if (forced)
        {
            Release(preferences);

            if (_session is null)
                return;

            ClearChecked();
        }

        if (ret)
            return;

        ShowChat(preferences, true);
        ImGui.EndTabItem();
    }

    /// <summary>Shows the player client.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowPlayerTab(Preferences preferences)
    {
        const ImGuiTableFlags Flags = ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchSame;
        Debug.Assert(_session is not null);

        if (!BeginTabItem("Players", Tab.Player))
            return;

        if (!preferences.BeginChild("Players"))
        {
            ImGui.EndChild();
            return;
        }

        if (!ImGui.BeginTable("Players", 4, Flags))
        {
            ImGui.EndTable();
            return;
        }

        ShowPlayers(preferences);
        ImGui.EndTable();
        ImGui.EndChild();
        ShowChat(preferences);
        ImGui.EndTabItem();
    }

    /// <summary>Shows the list of locations</summary>
    /// <param name="gameTime">The time elapsed since.</param>
    /// <param name="preferences">The user preferences.</param>
    void ShowLocationTab(GameTime gameTime, Preferences preferences)
    {
        Debug.Assert(_session is not null);

        if (!BeginTabItem("Locations", Tab.Location))
            return;

        if (!preferences.BeginChild("Locations"))
        {
            ImGui.EndChild();
            return;
        }

        if (_showConfirmationDialog)
            ShowConfirmationDialog(gameTime, preferences);
        else
            ShowLocations(preferences);

        ImGui.EndChild();
        ShowChat(preferences);
        ImGui.EndTabItem();
    }

    /// <summary>Shows the list of items.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowItemTab(Preferences preferences)
    {
        Debug.Assert(_session is not null);

        if (!BeginTabItem("Items", Tab.Item))
            return;

        if (!preferences.BeginChild("Items"))
        {
            ImGui.EndChild();
            return;
        }

        ImGui.SetNextItemWidth(preferences.Width(0));

        _ = preferences.Combo(
            "###Item Sort",
            ref _itemSort,
            "Sort by Name\0Sort by Name (Reversed)\0Sort by First Acquired\0Sort by Last Acquired\0\0"
        );

        if (_evaluator is { ItemToPhantoms.Count: not 0 })
            preferences.Combo("Item type", ref _itemType, "Real items\0Phantom items\0\0");

        _ = ImGui.Checkbox("Show used items", ref _showUsedItems);

        if (_evaluator is null)
            ShowNonManualItems(preferences);
        else
            ShowManualItems(preferences);

        ImGui.EndChild();
        ShowChat(preferences);
        ImGui.EndTabItem();
    }

    /// <summary>Shows the list of hints.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowHintTab(Preferences preferences)
    {
        Debug.Assert(_session is not null);

        if (!BeginTabItem("Hints", Tab.Hint))
        {
            _hintTask = Task.FromResult<HintMessage[]?>(null);
            return;
        }

        if (!preferences.BeginChild("Hints"))
        {
            ImGui.EndChild();
            return;
        }

        var roomState = _session.RoomState;
        var hintCost = roomState.HintCost.Max(1);
        var hintCount = roomState.HintPoints / hintCost;
        var percentageText = $"Hint cost percentage: {roomState.HintCostPercentage}% ({hintCost.Conjugate("point")})";
        var hintText = $"You can do {hintCount.Conjugate("hint")} ({roomState.HintPoints.Conjugate("point")})";
        preferences.ShowText(percentageText, disabled: true);
        preferences.ShowText(hintText, disabled: true);
        ImGui.SetNextItemWidth(preferences.Width(150));

        _ = preferences.Combo(
            "Filter",
            ref s_hintIndex,
            "Show sent hints\0Show received hints\0Show either hint type\0\0"
        );

        _ = ImGui.Checkbox("Show obtained hints", ref _showObtainedHints);

        if (LastHints is { } hints)
            foreach (var (hint, message) in hints)
                if (ShouldBeVisible(hint))
                    preferences.ShowText(message, hint.ItemFlags);

        ImGui.EndChild();
        ShowChat(preferences);
        ImGui.EndTabItem();
    }

    /// <summary>Displays the color edit widget.</summary>
    void ShowSettingsTab(Preferences preferences)
    {
        Debug.Assert(_session is not null);

        if (!BeginTabItem("Settings", Tab.Settings))
            return;

        if (!preferences.BeginChild("Settings"))
        {
            ImGui.EndChild();
            return;
        }

        ImGui.SeparatorText("Diagnostics");

        if (ImGui.Button("Open APWorld Directory") && Evaluator.FindApWorld(_yaml, preferences, Set) is { } apWorld)
        {
            ProcessStartInfo startInfo = new()
                { FileName = Path.GetDirectoryName(apWorld), CreateNoWindow = true, UseShellExecute = true };

            using var _ = Process.Start(startInfo);
        }

        ImGui.SameLine();

        if (ImGui.Button("Reconnect"))
        {
            Close(preferences, false);
            Connect(preferences);
        }

        ImGui.SeparatorText("Theming");
        var color = AppColor.Parse(_info.Color);
        var newColor = Preferences.ShowColorEdit("Color", color);

        if (newColor != color)
            _info = _info with { Color = newColor.ToString() };

        ImGui.EndChild();
        ShowChat(preferences);
        ImGui.EndTabItem();
    }

    /// <summary>Shows the chat.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="isChatTab">Whether this was called from the chat tab.</param>
    void ShowChat(Preferences preferences, bool isChatTab = false)
    {
        const ImGuiInputTextFlags Flags = Preferences.TextFlags | ImGuiInputTextFlags.AllowTabInput;
        Debug.Assert(_session is not null);

        if (!isChatTab && !preferences.AlwaysShowChat)
            return;

        if (isChatTab ? !ImGui.BeginChild("Chat") : !preferences.BeginChild("Chat", true))
        {
            ImGui.EndChild();
            return;
        }

        if (isChatTab)
            preferences.ShowText("TIP: Right click text or checkboxes to copy them!", disabled: true);

        ShowLog(preferences);
        ImGui.SeparatorText("Message");
        ImGui.SetNextItemWidth(preferences.Width(250));

        if (isChatTab)
            ImGui.SetKeyboardFocusHere();

        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
            MoveSentMessageIndex(1);

        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
            MoveSentMessageIndex(-1);

        ref var latestMessage = ref CollectionsMarshal.AsSpan(_sentMessages)[^1];
        var enter = ImGuiRenderer.InputText("##Message", ref latestMessage, ushort.MaxValue, Flags);
        ShowAutocomplete(preferences, latestMessage);
        ImGui.SameLine();

        if ((ImGui.Button("Send") || enter) && !string.IsNullOrWhiteSpace(latestMessage))
        {
            _session.Say(latestMessage);
            _sentMessagesIndex = _sentMessages.Count;
            _sentMessages.Add("");
        }

        preferences.ShowHelp(HelpMessage1);
        preferences.ShowHelp(HelpMessage2);
        ImGui.EndChild();
    }

    /// <summary>Shows the list of players.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowPlayers(Preferences preferences)
    {
        Debug.Assert(_session is not null);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, preferences.UiScale * 10);
        ImGui.TableSetupColumn("Game", ImGuiTableColumnFlags.None, preferences.UiScale * 10);
        ImGui.TableSetupColumn("Slot", ImGuiTableColumnFlags.None, 2);
        ImGui.TableSetupColumn("Team", ImGuiTableColumnFlags.None, 2);
        ImGui.TableHeadersRow();

        foreach (var player in _session.Players.AllPlayers)
            ShowPlayer(preferences, player);
    }

    /// <summary>Shows the confirmation dialog for releasing locations.</summary>
    /// <param name="gameTime">The time elapsed since.</param>
    /// <param name="preferences">The user preferences.</param>
    void ShowConfirmationDialog(GameTime gameTime, Preferences preferences)
    {
        Debug.Assert(_session is not null);

        ImGui.SeparatorText(
            IsReleasing ? "You have released the following:" : "You are about to release the following:"
        );

        var outOfLogic = ShowReleasedOrReleasingLocations(preferences);

        if (outOfLogic)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, preferences[AppPalette.ReleasingOutOfLogic]);
            ImGui.SeparatorText("WARNING: One or more locations are out of logic!");
            ImGui.PopStyleColor();
        }

        if (!IsReleasing && ImGui.Button("Cancel"))
            _showConfirmationDialog = false;

        ImGui.SameLine();

        if (!IsReleasing)
            _ = ImGui.Button("Confirm (Enter)");

        if (ImGui.IsItemActive() ||
            (preferences.HoldToConfirm ? ImGui.IsKeyDown(ImGuiKey.Enter) : ImGui.IsKeyPressed(ImGuiKey.Enter)) ||
            IsReleasing)
            ShowCountdownToRelease(gameTime, preferences, outOfLogic);
        else
            ClearChecked();
    }

    /// <summary>Shows locations.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowLocations(Preferences preferences)
    {
        Debug.Assert(_session is not null);
        _ = preferences.Combo("##Location Sort", ref _locationSort, "Sort by Name\0Sort by ID\0\0");
        _ = ImGui.Checkbox("Show Already Checked Locations", ref _showAlreadyChecked);
        var stuck = _evaluator is null ? ShowNonManualLocations(preferences) : ShowManualLocations(preferences);
        ImGui.EndChild();
        ImGui.Separator();
        var isAnyReleasable = _locations.Any(IsReleasable);
        var showStatus = stuck is null or true;
        _showLocationFooter = isAnyReleasable || showStatus;

        if (isAnyReleasable)
        {
            if (ImGui.Button("Check (Enter)") || ImGui.IsKeyPressed(ImGuiKey.Enter))
                _showConfirmationDialog = true;

            ImGui.SameLine();

            if (ImGui.Button("Move in or out of user category"))
            {
                _info = _info with
                {
                    Tagged = _info.Tagged?.SymmetricExcept(_locations.Where(IsReleasable).Select(x => x.Key)),
                };

                preferences.Sync(ref _info);
            }
        }

        if (isAnyReleasable && showStatus)
            ImGui.SameLine(0, 20);

        switch (stuck)
        {
            case null:
                preferences.ShowText("BK", AppPalette.BK);
                break;
            case true:
                preferences.ShowText("Done", AppPalette.Released);

                if (_canGoal is false)
                    _canGoal = null;

                break;
        }

        if (_canGoal is not null)
            return;

        if (isAnyReleasable || stuck is null or true)
            ImGui.SameLine(preferences.Width(75), 0);

        if (!ImGui.Button("Goal"))
            return;

        _canGoal = true;
        _session.SetGoalAchieved();
        _session.SetClientState(ArchipelagoClientState.ClientGoal);
    }

    /// <summary>Shows the autocomplete.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="message">The current message.</param>
    void ShowAutocomplete(Preferences preferences, ref readonly string message)
    {
        Debug.Assert(_session is not null);

        if (preferences.Suggestions <= 0)
            return;

        var sum = 0;

        foreach (var suggestion in GetSuggestions(message, out var user))
            if (StrictStartsWith(user, suggestion) && ++sum >= preferences.Suggestions)
                goto HasAnythingToRender;

        if (sum is 0)
            return;

    HasAnythingToRender:

        var pos = ImGui.GetCursorPos();
        var size = ImGui.CalcTextSize(message);
        var length = preferences.Suggestions.Min(sum);
        pos.Y += size.Y * -length;
        var (x, y) = (ImGui.GetContentRegionAvail().X, size.Y * (length + 1));
        ImGui.SetNextWindowPos(pos);
        ImGui.SetNextWindowSizeConstraints(default, new(x, y));

        if (!ImGui.BeginPopup("Autocomplete"))
            return;

        ImGui.SetWindowFontScale(preferences.UiScale);

        foreach (var suggestion in GetSuggestions(message, out var user))
            if (StrictStartsWith(user, suggestion))
                PasteIfClicked(user, suggestion, message.Nth(^1)?.IsWhitespace() is false);

        ImGui.EndPopup();
    }

    /// <summary>Shows the message log.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowLog(Preferences preferences)
    {
        ImGui.SeparatorText("Log");

        if (!ImGui.BeginChild("Log", preferences.ChildSize()))
        {
            ImGui.EndChild();
            return;
        }

        ImGui.SetWindowFontScale(preferences.UiScale);
        ShowMessages(preferences, 0);

        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            ImGui.SetScrollHereY(1);

        ImGui.EndChild();
    }

    /// <summary>Shows the player within a table.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="player">The player to show.</param>
    void ShowPlayer(Preferences preferences, PlayerInfo player)
    {
        const string FlavorText = "Despite everything, it's still you.";
        Debug.Assert(_session is not null);
        ImGui.TableNextRow();

        if (!ImGui.TableNextColumn())
            return;

        var playerName = player.ToString();
        var isSelf = player.Slot == _session.Players.ActivePlayer.Slot;
        var selfColor = preferences[isSelf ? AppPalette.Useful : AppPalette.Neutral];
        preferences.ShowText(playerName, selfColor);

        if (isSelf)
            preferences.Tooltip(FlavorText);

        var (gameName, isManual) = player.Game is ['M', 'a', 'n', 'u', 'a', 'l', '_', .. var rest]
            ? (rest.SplitOn('_')[..^1].ToString(), true)
            : (player.Game, false);

        if (!ImGui.TableNextColumn())
            return;

        preferences.ShowText(gameName, isManual ? AppPalette.Progression : AppPalette.Neutral);

        if (isManual)
            preferences.Tooltip("Manual Game");

        var slotName = player.Slot.ToString();

        if (!ImGui.TableNextColumn())
            return;

        preferences.ShowText(slotName, selfColor);

        if (isSelf)
            preferences.Tooltip(FlavorText);

        if (!ImGui.TableNextColumn())
            return;

        var teamName = player.Team.ToString();
        var isTeammate = player.Team == _session.Players.ActivePlayer.Team;
        preferences.ShowText(teamName, isTeammate ? AppPalette.Neutral : AppPalette.Checked);
    }

    /// <summary>Moves the index by an amount.</summary>
    /// <param name="offset">The offset.</param>
    void MoveSentMessageIndex(int offset)
    {
        var io = ImGui.GetIO();

        for (var i = 0; i < _sentMessages[_sentMessagesIndex].Length; i++)
        {
            io.AddKeyEvent(ImGuiKey.Backspace, true);
            io.AddKeyEvent(ImGuiKey.Backspace, false);
        }

        _sentMessagesIndex = (_sentMessagesIndex + offset).Clamp(0, _sentMessages.Count - 1);
        var message = _sentMessagesIndex == _sentMessages.Count - 1 ? "" : _sentMessages[_sentMessagesIndex];
        io.AddInputCharactersUTF8(message);
    }

    /// <summary>Handles closing the tab or window.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="open">Whether to keep the socket alive.</param>
    /// <returns>Not the parameter <paramref name="open"/>.</returns>
    bool Close(Preferences preferences, bool open)
    {
        var session = _session;

        if (open || session is null)
            return !open;
#pragma warning disable IDISP013
        _ = Task.Run(session.Socket.DisconnectAsync).ConfigureAwait(false);
#pragma warning restore IDISP013
        preferences.Sync(ref _info);
        _session = null;
        return !open;
    }

    /// <summary>Shows the location list in a non-manual context.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <returns>Whether this slot is completed. Never <see langword="null"/>.</returns>
    bool? ShowNonManualLocations(Preferences preferences)
    {
        Debug.Assert(_session is not null);
        ShowLocationSearch(preferences);

        if (!ImGui.BeginChild("Locations", preferences.ChildSize(_showLocationFooter ? 100 : 10)))
        {
            ImGui.EndChild();
            return false;
        }

        ImGui.SetWindowFontScale(preferences.UiScale);
        ShowUserCategorizedLocations(preferences, Noop, null);
        var locationHelper = _session.Locations;

        string GetLocationNameFromId(long x) => locationHelper.GetLocationNameFromId(x, _yaml.Game);

        var locations = _showAlreadyChecked ? locationHelper.AllLocations : locationHelper.AllMissingLocations;

        var orderedLocations = _locationSort is 0
            ? locations.Select(GetLocationNameFromId).Order(FrozenSortedDictionary.Comparer)
            : locations.Order().Select(GetLocationNameFromId);

        foreach (var location in orderedLocations.Where(ShouldBeVisible))
            Checkbox(preferences, location, ApWorldReader.Uncategorized);

        return locationHelper.AllMissingLocations.Count is 0;
    }

    /// <summary>Shows the location list in a manual context.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <returns>Whether this slot is completed, or <see langword="null"/> if BK'ed.</returns>
    bool? ShowManualLocations(Preferences preferences)
    {
        Debug.Assert(_session is not null);
        Debug.Assert(_evaluator is not null);
        _ = ImGui.Checkbox("Show Out of Logic Locations", ref _showOutOfLogic);
        var setter = GetNextItemOpenSetter();
        ImGui.SameLine();
        var newValue = InlineButtons("Tick all", "Untick all");
        ShowLocationSearch(preferences);

        if (!ImGui.BeginChild("Locations", preferences.ChildSize(_showLocationFooter ? 100 : 10)))
        {
            ImGui.EndChild();
            return false;
        }

        ImGui.SetWindowFontScale(preferences.UiScale);
        ShowUserCategorizedLocations(preferences, setter, newValue);
        bool? ret = true;

        var orderedCategories = _locationSort is 0
            ? _evaluator.CategoryToLocations.Array.Select(x => (x.Key, x.Value as IReadOnlyCollection<string>))
            : _evaluator.CategoryToLocations.Array.Select(OrderById)
               .OrderBy(x => x.Location.Max(static x => x.Id))
               .Select(x => (x.Category, (IReadOnlyCollection<string>)[..x.Location.Select(x => x.Name)]));

        foreach (var (category, locations) in orderedCategories)
        {
            if (_evaluator.HiddenCategories.Contains(category))
                continue;

            var count = 0;

            foreach (var location in locations)
            {
                var status = this[location].Status;

                if (status is LocationStatus.Reachable or LocationStatus.ProbablyReachable)
                    ret = false;
                else if (status is not LocationStatus.Checked && ret is true)
                    ret = null;

                if (ShouldBeVisible(location))
                    count++;
            }

            if (count is 0)
                continue;

            setter();

            if (!ImGui.CollapsingHeader($"{category} ({count})###{category}:|LocationCategory"))
                continue;

            foreach (var location in locations.Where(ShouldBeVisible))
                Checkbox(preferences, location, category, newValue);
        }

        return ret;
    }

    /// <summary>Show the locations that have been or are going to be released.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <returns>Whether any locations released are out of logic.</returns>
    bool ShowReleasedOrReleasingLocations(Preferences preferences)
    {
        if (!ImGui.BeginChild("Release", preferences.ChildSize(200)))
        {
            ImGui.EndChild();
            return false;
        }

        ImGui.SetWindowFontScale(preferences.UiScale);

        if (IsReleasing && _isAttemptingToRelease is null)
        {
            ShowMessages(preferences, _releaseIndex);
            ImGui.EndChild();
            return false;
        }

        var outOfLogic = false;

        foreach (var key in _sortedKeys)
            if (this[key] is (_, var status, true))
            {
                outOfLogic |= status is LocationStatus.OutOfLogic;
                preferences.ShowText(key, status);
            }

        ImGui.EndChild();
        return outOfLogic;
    }

    /// <summary>Shows the countdown before locations are being released.</summary>
    /// <param name="gameTime">The time elapsed since.</param>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="outOfLogic">Whether any locations released are out-of-logic.</param>
    void ShowCountdownToRelease(GameTime gameTime, Preferences preferences, bool outOfLogic)
    {
        if (_isAttemptingToRelease is false)
        {
            ResetTimer(outOfLogic);
            _isAttemptingToRelease = true;
        }

        if (IsReleasing)
        {
            if (preferences.AlwaysShowChat)
            {
                Release(preferences);

                if (_session is null)
                    return;

                ClearChecked();
                _showConfirmationDialog = false;
                return;
            }

            preferences.ShowText(ReleaseMessage(), AppPalette.Released);
            preferences.ShowText("Press left click to return to the previous screen", disabled: true);
            Release(preferences);

            if (_session is null)
                return;

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                ClearChecked();

            return;
        }

        _confirmationTimer = preferences.HoldToConfirm ? _confirmationTimer - gameTime.ElapsedGameTime : new(-1);
        preferences.ShowText(ReleasingMessage(), outOfLogic ? AppPalette.ReleasingOutOfLogic : AppPalette.Releasing);
    }

    /// <summary>Displays all messages starting from the index provided.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="startIndex">The index to start displaying messages from.</param>
    void ShowMessages(Preferences preferences, int startIndex)
    {
        for (; startIndex < _messages.Count && _messages[startIndex].Parts is var parts; startIndex++)
        {
            var first = true;
            var message = parts.Conjoin("");

            foreach (var part in parts)
            {
                if (!first || (first = false))
                    ImGui.SameLine(0, 0);

                var priority = part.PaletteColor switch
                {
                    PaletteColor.SlateBlue => AppPalette.Useful,
                    PaletteColor.Salmon => AppPalette.Trap,
                    PaletteColor.Plum => AppPalette.Progression,
                    _ => AppPalette.Neutral,
                };

                var palette = priority switch
                {
                    not AppPalette.Neutral => priority,
                    _ when _session is null => AppPalette.Neutral,
                    _ when _yaml.Name == part.Text => AppPalette.Useful,
                    _ when _session.Players.AllPlayers.Any(x => x.Name == part.Text) => AppPalette.Progression,
                    _ => AppPalette.Neutral,
                };

                preferences.ShowText(part.Text, palette, message);

                if (priority is not AppPalette.Neutral)
                    preferences.Tooltip($"Item Class: {priority}");
            }

            for (var i = 0; i < 5; i++)
                ImGui.Spacing();
        }
    }

    /// <summary>Shows the location search text field.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowLocationSearch(Preferences preferences)
    {
        ImGui.SetNextItemWidth(preferences.Width(0));
        _ = ImGuiRenderer.InputTextWithHint("##LocationSearch", "Search...", ref _locationSearch, ushort.MaxValue);
    }

    /// <summary>Shows the item search text field.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowItemSearch(Preferences preferences)
    {
        ImGui.SetNextItemWidth(preferences.Width(450));
        _ = ImGuiRenderer.InputTextWithHint("##ItemSearch", "Search...", ref _itemSearch, ushort.MaxValue);
    }

    /// <summary>Shows user-defined locations.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="setter">The setter.</param>
    /// <param name="overrideAll">The value to override the checkbox with.</param>
    void ShowUserCategorizedLocations(Preferences preferences, Action setter, bool? overrideAll)
    {
        Debug.Assert(_session is not null);

        if (_info.Tagged is not { Count: not 0 and var c } tagged)
            return;

        setter();

        if (!ImGui.CollapsingHeader($"{UserCategorized} ({c})###{UserCategorized}:|LocationCategory"))
            return;

        var orderedLocations = _locationSort is 0
            ? tagged.AsEnumerable()
            : tagged.OrderBy(x => _session.Locations.GetLocationIdFromName(_yaml.Game, x));

        foreach (var location in orderedLocations.Where(ShouldBeVisible))
            Checkbox(preferences, location, UserCategorized, overrideAll);
    }

    /// <summary>Shows non-manual items</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowNonManualItems(Preferences preferences)
    {
        const string Default = ApWorldReader.Uncategorized;
        ShowItemSearch(preferences);

        if (GroupItems(Default, default)
           .Where(x => x.IsMatch(_itemSearch) && x.IsMatch(_info, _showUsedItems))
           .Aggregate(false, (a, n) => n.Show(preferences, ref _info) is true || a))
            preferences.Sync(ref _info);
    }

    /// <summary>Shows manual items.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowManualItems(Preferences preferences)
    {
        Debug.Assert(_evaluator is not null);
        _ = ImGui.Checkbox("Show pending items", ref _showYetToReceive);
        ShowItemSearch(preferences);
        ImGui.SameLine();
        var setter = GetNextItemOpenSetter();

        if (_itemType is 0)
            ShowRealManualItems(preferences, setter);
        else
            ShowPhantomManualItems(preferences, setter);
    }

    /// <summary>Shows real items from the manual client.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="setter">The setter.</param>
    void ShowRealManualItems(Preferences preferences, Action setter)
    {
        Debug.Assert(_evaluator is not null);

        foreach (var (category, items) in _evaluator.CategoryToItems)
        {
            if (_evaluator.HiddenCategories.Contains(category))
                continue;

            IList<ReceivedItem> filtered =
                [..GroupItems(category, items).Where(x => x.IsMatch(_itemSearch) && x.IsMatch(_info, _showUsedItems))];

            if (filtered.Sum(x => x.Count) is var sum && sum is 0)
                continue;

            setter();

            if (!ImGui.CollapsingHeader($"{category} ({sum})###{category}:|ItemCategory"))
                continue;

            if (filtered.Aggregate(false, (a, n) => n.Show(preferences, ref _info) is true || a))
                preferences.Sync(ref _info);
        }
    }

    /// <summary>Shows phantom items from the manual client.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="setter">The setter.</param>
    void ShowPhantomManualItems(Preferences preferences, Action setter)
    {
        Debug.Assert(_session is not null);
        Debug.Assert(_evaluator is not null);
        var requiresSync = false;

        foreach (var (phantomItem, values) in _evaluator.PhantomToItems)
        {
            if (values.IsEmpty)
                continue;

            IList<(ReceivedItem Item, int Count)> found = [..values.Select(GetItemInfo)];
            var sum = found.Sum(x => x.Item.Count * x.Count);

            if (sum is 0)
                continue;

            var last = found is [] ? int.MaxValue : found.Max(x => x.Item.LastOrderReceived);

            ReceivedItem received =
                new(ItemFlags.None, phantomItem, sum, last, [..found.SelectMany(x => x.Item.Locations ?? [])]);

            if (!received.IsMatch(_info, _showUsedItems))
                continue;

            setter();

            switch (received.Show(preferences, ref _info, true))
            {
                case true:
                    requiresSync = true;
                    break;
                case null: goto Next;
            }

            foreach (var (item, count) in found.Where(x => x.Item.Count is not 0))
            {
                switch (item.Show(preferences, ref _info))
                {
                    case true:
                        requiresSync = true;
                        break;
                    case null: goto Next;
                }

                ImGui.SameLine();
                preferences.ShowText($"= {item.Count * count}");
            }

        Next: ;
        }

        if (requiresSync)
            preferences.Sync(ref _info);
    }

    /// <summary>Convenience function for displaying a checkbox with a specific color.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="location">The location that this checkbox represents.</param>
    /// <param name="category">The category that the location falls under, used to ensure IDs remain distinct.</param>
    /// <param name="overrideAll">The value to override the checkbox with.</param>
    void Checkbox(Preferences preferences, string location, string category, bool? overrideAll = null)
    {
        ref var value = ref this[location];
        ImGui.PushStyleColor(ImGuiCol.Text, preferences[value.Status]);
        ImGui.Checkbox($"{location}###{location}:|{category}:|Location", ref value.Checked);
        preferences.CopyIfClicked(location);

        if (overrideAll is { } all)
            value.Checked = all;

        if (value.Logic is { } logic)
            preferences.Tooltip(logic.ToMinimalString(), true);

        ImGui.PopStyleColor();
        value.Checked &= value.Status is not LocationStatus.Checked;
    }

    /// <summary>Clears all checkboxes.</summary>
    void ClearChecked()
    {
        if (_isAttemptingToRelease is false)
            return;

        var isReleasing = IsReleasing;
        ResetTimer(false);
        _isAttemptingToRelease = false;

        if (!isReleasing)
            return;

        _showConfirmationDialog = false;

        foreach (var key in _sortedKeys)
            this[key].Checked = false;
    }

    /// <summary>Adds the text provided if the selectable is clicked.</summary>
    /// <param name="user">The user input.</param>
    /// <param name="match">The match.</param>
    /// <param name="addSpacePrefix">Whether to add whitespace as a prefix.</param>
    void PasteIfClicked(ReadOnlySpan<char> user, string match, bool addSpacePrefix)
    {
        _ = ImGui.Selectable(match);

        if (ImGui.IsItemHovered())
            _lastSuggestion = match;

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) || !match.Equals(_lastSuggestion, StringComparison.Ordinal))
            return;

        if (addSpacePrefix)
            ImGui.GetIO().AddInputCharactersUTF8([' ']);

        ImGui.GetIO().AddInputCharactersUTF8(match[user.Length..]);
    }

    /// <summary>Gets the message for having released locations.</summary>
    /// <returns>The message.</returns>
    string ReleaseMessage() => $"Released {_locations.Values.Count(x => x.Checked).Conjugate("location")}";

    /// <summary>Gets the message for releasing locations, showing how much time is left.</summary>
    /// <returns>The message.</returns>
    string ReleasingMessage() => $"{_confirmationTimer.TotalSeconds.Max(0):N2}s before release";

    /// <summary>Converts the key-value pair into the tuple containing the name and id.</summary>
    /// <param name="kvp">The key-value pair to deconstruct.</param>
    /// <returns>The names and ids.</returns>
    (string Category, (string Name, long Id)[] Location) OrderById(
        KeyValuePair<string, FrozenSortedDictionary.Element> kvp
    )
    {
        Debug.Assert(_session is not null);

        return (kvp.Key, Location:
        [
            ..kvp.Value
               .Select(x => (Key: x, Value: _session.Locations.GetLocationIdFromName(_yaml.Game, x)))
               .OrderBy(x => x.Value),
        ]);
    }
}
