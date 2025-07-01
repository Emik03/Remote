// SPDX-License-Identifier: MPL-2.0
namespace Remote;

/// <inheritdoc cref="Client"/>
public sealed partial class Client
{
    /// <summary>Contains the options for the hint setting.</summary>
    static readonly string[] s_hintOptions = ["Show sent hints", "Show received hints"];

    /// <summary>Contains the list of sent messages, with the drafting message at the end.</summary>
    readonly List<string> _sentMessages = [""];

    /// <summary>Whether to show the dialog.</summary>
    bool _showAlreadyChecked, _showConfirmationDialog, _showObtainedHints, _showOutOfLogic, _showYetToReceive;

    /// <summary>Whether the user is attempting to release.</summary>
    /// <remarks><para>
    /// <see langword="false"/> means this slot is not attempting to release.
    /// <see langword="null"/> means this slot has released.
    /// <see langword="true"/> means this slot is attempting to release.
    /// </para></remarks>
    bool? _isAttemptingToRelease;

    /// <summary>Contains the last amount of checks.</summary>
    int _hintIndex, _lastItemCount = int.MinValue, _lastLocationCount = int.MaxValue, _sentMessagesIndex;

    /// <summary>The current state of the text field.</summary>
    string _itemSearch = "", _locationSearch = "";

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
        const int Styles = 3;
        var open = true;
        var pushedColor = AppColor.TryParse(_info.Color, out var color);

        if (pushedColor)
        {
            ImGui.PushStyleColor(ImGuiCol.TabSelected, color / preferences.ActiveTabDim);
            ImGui.PushStyleColor(ImGuiCol.TabHovered, color / preferences.ActiveTabDim);
            ImGui.PushStyleColor(ImGuiCol.Tab, color / preferences.InactiveTabDim);
        }

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
        else if (!ImGui.Begin(_windowName, ref open, ImGuiWindowFlags.HorizontalScrollbar) || !open)
        {
            if (pushedColor)
                ImGui.PopStyleColor(Styles);

            ImGui.End();
            selected = false;
            return Close(preferences, open);
        }

        ImGui.SetWindowFontScale(preferences.UiScale);

        if (_session is null)
            ShowBuilder(preferences);
        else
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

    /// <summary>Copies the text if the mouse has been clicked.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="text">The text to copy.</param>
    /// <param name="button">The button to check for.</param>
    static void CopyIfClicked(Preferences preferences, string text, ImGuiMouseButton button = ImGuiMouseButton.Right)
    {
        if (!ImGui.IsItemHovered())
            return;

        if (ImGui.IsMouseDown(button))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, preferences[AppPalette.Reachable]);
            Tooltip(preferences, "Copied!");
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
    /// <param name="preferences">The user preferences.</param>
    /// <param name="text">The text to display.</param>
    static void Tooltip(Preferences preferences, string text)
    {
        if (!ImGui.BeginTooltip())
            return;

        ImGui.SetWindowFontScale(preferences.UiScale);
        ImGui.Text(text);
        ImGui.EndTooltip();
    }

    /// <summary>Gets the setter for the next item open.</summary>
    /// <returns>The function that invokes <see cref="ImGui.SetNextItemOpen(bool)"/>.</returns>
    static Action GetNextItemOpenSetter()
    {
        bool? ret = null;

        if (ImGui.Button("Expand all"))
            ret = true;

        ImGui.SameLine();

        if (ImGui.Button("Collapse all"))
            ret = false;

        return ret switch
        {
            null => Noop,
            true => static () => ImGui.SetNextItemOpen(true),
            false => static () => ImGui.SetNextItemOpen(false),
        };
    }

    /// <summary>Shows the components for entering slot information.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowBuilder(Preferences preferences)
    {
        if (!_connectingTask.IsCompleted)
        {
            ImGui.TextDisabled(_connectionMessage);
            return;
        }

        ImGui.SeparatorText("Slot");
        ImGui.SetNextItemWidth(preferences.Width(100));
        _ = ImGuiRenderer.InputText("Game", ref _yaml.Game, ushort.MaxValue, Preferences.TextFlags);
        ImGui.SetNextItemWidth(preferences.Width(100));
        var enter = ImGuiRenderer.InputText("Name", ref _yaml.Name, 16, Preferences.TextFlags);

        if (_errors is not null)
            foreach (var error in _errors.AsSpan())
                if (!string.IsNullOrEmpty(error))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, preferences[AppPalette.Trap]);
                    ImGui.TextWrapped(error);
                    CopyIfClicked(preferences, error);
                    ImGui.PopStyleColor();
                }

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

        if (_session.Items.AllItemsReceived.Count is var itemCount && _lastItemCount < itemCount)
        {
            _lastItemCount = _lastLocationCount;
            UpdateStatus();
        }

        if (_session.Locations.AllMissingLocations.Count is var locationCount && _lastLocationCount > locationCount)
        {
            _lastLocationCount = locationCount;
            UpdateStatus();
        }

        if (!ImGui.BeginTabBar("Tabs"))
            return;

        ShowChatTab(preferences);
        ShowPlayerTab(preferences);
        ShowLocationTab(gameTime, preferences);
        ShowItemTab(preferences);
        ShowHintTab(preferences);
        ShowSettings(preferences);
        ImGui.EndTabBar();
    }

    /// <summary>Shows the messaging client.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowChatTab(Preferences preferences)
    {
        const ImGuiInputTextFlags Flags = Preferences.TextFlags | ImGuiInputTextFlags.AllowTabInput;
        Debug.Assert(_session is not null);
        var isForced = preferences.MoveToChatTab && IsReleasing;
        var flags = isForced ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        var ret = !ImGuiRenderer.BeginTabItem("Chat", ref Unsafe.NullRef<bool>(), flags);

        if (isForced)
        {
            Release(preferences);
            ClearChecked();
        }

        if (ret)
            return;

        if (!ImGui.BeginChild("Chat"))
        {
            ImGui.EndChild();
            return;
        }

        ImGui.TextDisabled("TIP: Right click text or checkboxes to copy them!");
        ShowLog(preferences);
        ImGui.SeparatorText("Message");
        ImGui.SetNextItemWidth(preferences.Width(250));
        ImGui.SetKeyboardFocusHere();

        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
            MoveSentMessageIndex(1);

        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
            MoveSentMessageIndex(-1);

        ref var latestMessage = ref CollectionsMarshal.AsSpan(_sentMessages)[^1];
        var enter = ImGui.InputText("##Message", ref latestMessage, ushort.MaxValue, Flags);
        ImGui.SameLine();

        if (ImGui.Button("Send") || enter)
        {
            _session.Say(latestMessage);
            _sentMessages.Add("");
            _sentMessagesIndex = _sentMessages.Count - 1;
        }

        ImGui.SameLine();
        ImGui.TextDisabled("(?)");

        if (ImGui.IsItemHovered())
            Tooltip(preferences, HelpMessage1);

        ImGui.SameLine();
        ImGui.TextDisabled("(?)");

        if (ImGui.IsItemHovered())
            Tooltip(preferences, HelpMessage2);

        ImGui.EndChild();
        ImGui.EndTabItem();
    }

    /// <summary>Shows the player client.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowPlayerTab(Preferences preferences)
    {
        const ImGuiTableFlags Flags = ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchSame;
        Debug.Assert(_session is not null);

        if (!ImGui.BeginTabItem("Players"))
            return;

        if (!ImGui.BeginChild("Players"))
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
        ImGui.EndTabItem();
    }

    /// <summary>Shows the list of locations</summary>
    /// <param name="gameTime">The time elapsed since.</param>
    /// <param name="preferences">The user preferences.</param>
    void ShowLocationTab(GameTime gameTime, Preferences preferences)
    {
        Debug.Assert(_session is not null);

        if (!ImGui.BeginTabItem("Locations"))
            return;

        if (!ImGui.BeginChild("Locations"))
        {
            ImGui.EndChild();
            return;
        }

        if (_showConfirmationDialog)
            ShowConfirmationDialog(gameTime, preferences);
        else
            ShowLocations(preferences);

        ImGui.EndChild();
        ImGui.EndTabItem();
    }

    /// <summary>Shows the list of items.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowItemTab(Preferences preferences)
    {
        Debug.Assert(_session is not null);

        if (!ImGui.BeginTabItem("Items"))
            return;

        if (!ImGui.BeginChild("Items"))
        {
            ImGui.EndChild();
            return;
        }

        if (_evaluator is null)
            ShowNonManualItems(preferences);
        else
            ShowManualItems(preferences);

        ImGui.EndChild();
        ImGui.EndTabItem();
    }

    /// <summary>Shows the list of hints.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowHintTab(Preferences preferences)
    {
        Debug.Assert(_session is not null);

        if (!ImGui.BeginTabItem("Hints"))
        {
            _hintTask = Task.FromResult<Hint[]?>(null);
            return;
        }

        if (!ImGui.BeginChild("Hints"))
        {
            ImGui.EndChild();
            return;
        }

        var ro = _session.RoomState;
        ImGui.TextDisabled($"Hint cost percentage: {ro.HintCostPercentage}%% ({ro.HintCost} points)");
        ImGui.TextDisabled($"You can do {(ro.HintPoints / ro.HintCost).Conjugate("hint")} ({ro.HintPoints} points)");
        _ = ImGui.Checkbox("Show obtained hints", ref _showObtainedHints);
        ImGui.SetNextItemWidth(preferences.Width(150));
        _ = ImGui.Combo("Filter", ref _hintIndex, s_hintOptions, s_hintOptions.Length);

        if (LastHints is { } hints)
            foreach (var (itemFlags, message) in hints.Where(ShouldBeVisible)
               .Select(x => (x.ItemFlags, Message: Message(x)))
               .OrderBy(x => x.Message, FrozenSortedDictionary.Comparer))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ColorOf(itemFlags, preferences));
                ImGui.BulletText(message);
                CopyIfClicked(preferences, message);
                ImGui.PopStyleColor();
            }

        ImGui.EndChild();
        ImGui.EndTabItem();
    }

    /// <summary>Displays the color edit widget.</summary>
    void ShowSettings(Preferences preferences)
    {
        Debug.Assert(_session is not null);

        if (!ImGui.BeginTabItem("Settings"))
            return;

        if (!ImGui.BeginChild("Settings"))
        {
            ImGui.EndChild();
            return;
        }

        ImGui.SeparatorText("Diagnostics");

        if (ImGui.Button("Open APWorld Directory") && Evaluator.FindApWorld(_yaml, preferences) is { } apWorld)
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
        ImGui.EndTabItem();
    }

    /// <summary>Shows the message log.</summary>
    /// <param name="preferences">The user preferences</param>
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

    /// <summary>Shows the list of players.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowPlayers(Preferences preferences)
    {
        const string FlavorText = "Despite everything, it's still you.";
        Debug.Assert(_session is not null);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.None, preferences.UiScale * 10);
        ImGui.TableSetupColumn("Game", ImGuiTableColumnFlags.None, preferences.UiScale * 10);
        ImGui.TableSetupColumn("Slot", ImGuiTableColumnFlags.None, 2);
        ImGui.TableSetupColumn("Team", ImGuiTableColumnFlags.None, 2);
        ImGui.TableHeadersRow();

        foreach (var player in _session.Players.AllPlayers)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            var playerName = player.ToString();
            var isSelf = player.Slot == _session.Players.ActivePlayer.Slot;
            var selfColor = preferences[isSelf ? AppPalette.Useful : AppPalette.Neutral];
            ImGui.TextColored(selfColor, playerName);
            CopyIfClicked(preferences, playerName);

            if (isSelf && ImGui.IsItemHovered())
                Tooltip(preferences, FlavorText);

            var (gameName, isManual) = player.Game is ['M', 'a', 'n', 'u', 'a', 'l', '_', .. var rest]
                ? (rest.SplitOn('_')[..^1].ToString(), true)
                : (player.Game, false);

            ImGui.TableNextColumn();
            ImGui.TextColored(preferences[isManual ? AppPalette.Progression : AppPalette.Neutral], gameName);
            CopyIfClicked(preferences, gameName);

            if (isManual && ImGui.IsItemHovered())
                Tooltip(preferences, "Manual Game");

            var slotName = player.Slot.ToString();
            ImGui.TableNextColumn();
            ImGui.TextColored(selfColor, slotName);
            CopyIfClicked(preferences, slotName);

            if (isSelf && ImGui.IsItemHovered())
                Tooltip(preferences, FlavorText);

            ImGui.TableNextColumn();
            var teamName = player.Team.ToString();
            var isTeammate = player.Team == _session.Players.ActivePlayer.Team;
            ImGui.TextColored(preferences[isTeammate ? AppPalette.Neutral : AppPalette.Checked], teamName);
            CopyIfClicked(preferences, teamName);
        }
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
            _ = ImGui.Button("Confirm");

        if (ImGui.IsItemActive() || IsReleasing)
            ShowCountdownToRelease(gameTime, preferences, outOfLogic);
        else
            ClearChecked();
    }

    /// <summary>Shows locations.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowLocations(Preferences preferences)
    {
        Debug.Assert(_session is not null);
        _ = ImGui.Checkbox("Show Already Checked Locations", ref _showAlreadyChecked);
        var stuck = _evaluator is null ? ShowNonManualLocations(preferences) : ShowManualLocations(preferences);
        ImGui.EndChild();
        ImGui.Separator();

        switch (stuck)
        {
            case null:
                ImGui.TextColored(preferences[AppPalette.BK], "BK");
                break;
            case true:
                ImGui.TextColored(preferences[AppPalette.Released], "Done");

                if (_canGoal is false)
                    _canGoal = null;

                break;
        }

        var isAnyReleasable = _locations.Any(IsReleasable);

        if (isAnyReleasable && stuck is null or true)
            ImGui.SameLine();

        if (isAnyReleasable && ImGui.Button("Check"))
            _showConfirmationDialog = true;

        if (_canGoal is not null)
            return;

        if (isAnyReleasable || stuck is null or true)
            ImGui.SameLine();

        if (!ImGui.Button("Goal"))
            return;

        _canGoal = true;
        _session.SetGoalAchieved();
        _session.SetClientState(ArchipelagoClientState.ClientGoal);
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
        if (open || _session is null)
            return !open;
#pragma warning disable IDISP013
        _ = Task.Run(_session.Socket.DisconnectAsync).ConfigureAwait(false);
#pragma warning restore IDISP013
        preferences.Sync(ref _info);
        return !open;
    }

    /// <summary>Shows the location list in a non-manual context.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <returns>Whether this slot is completed. Never <see langword="null"/>.</returns>
    bool? ShowNonManualLocations(Preferences preferences)
    {
        Debug.Assert(_session is not null);
        ShowLocationSearch();

        if (!ImGui.BeginChild("Locations", preferences.ChildSize(100)))
        {
            ImGui.EndChild();
            return false;
        }

        ImGui.SetWindowFontScale(preferences.UiScale);
        var locationHelper = _session.Locations;
        var locations = _showAlreadyChecked ? locationHelper.AllLocations : locationHelper.AllMissingLocations;

        foreach (var location in locations)
            if (locationHelper.GetLocationNameFromId(location, _yaml.Game) is { } name && ShouldBeVisible(name))
                Checkbox(preferences, name, ApWorldReader.Uncategorized);

        return locationHelper.AllMissingLocations.Count is 0;
    }

    /// <summary>Shows the location list in a manual context.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <returns>Whether this slot is completed, or <see langword="null"/> if BK'ed.</returns>
    bool? ShowManualLocations(Preferences preferences)
    {
        Debug.Assert(_evaluator is not null);
        _ = ImGui.Checkbox("Show Out of Logic Locations", ref _showOutOfLogic);
        var setter = GetNextItemOpenSetter();
        ShowLocationSearch();

        if (!ImGui.BeginChild("Locations", preferences.ChildSize(100)))
        {
            ImGui.EndChild();
            return false;
        }

        ImGui.SetWindowFontScale(preferences.UiScale);
        bool? ret = true;

        foreach (var (category, locations) in _evaluator.CategoryToLocations)
        {
            if (_evaluator.HiddenCategories.Contains(category))
                continue;

            var count = 0;

            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
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

            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var location in locations)
                if (ShouldBeVisible(location))
                    Checkbox(preferences, location, category);
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
            if (this[key] is (var status, true))
            {
                outOfLogic |= status is LocationStatus.OutOfLogic;
                ImGui.PushStyleColor(ImGuiCol.Text, preferences[status]);
                ImGui.BulletText(key);
                ImGui.PopStyleColor();
                CopyIfClicked(preferences, key);
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
            ImGui.TextColored(preferences[AppPalette.Released], ReleaseMessage());
            ImGui.TextDisabled("Press left click to return to the previous screen");
            Release(preferences);

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                ClearChecked();

            return;
        }

        var color = preferences[outOfLogic ? AppPalette.ReleasingOutOfLogic : AppPalette.Releasing];
        _confirmationTimer = preferences.HoldToConfirm ? _confirmationTimer - gameTime.ElapsedGameTime : new(-1);
        ImGui.TextColored(color, ReleasingMessage());
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
                    ImGui.SameLine();

                var palette = part.PaletteColor switch
                {
                    PaletteColor.SlateBlue => AppPalette.Useful,
                    PaletteColor.Salmon => AppPalette.Trap,
                    PaletteColor.Plum => AppPalette.Progression,
                    _ => AppPalette.Neutral,
                };

                ImGui.TextColored(preferences[palette], part.Text.Replace("%", "%%"));

                if (palette is not AppPalette.Neutral && ImGui.IsItemHovered())
                    Tooltip(preferences, $"Item Class: {palette}");

                CopyIfClicked(preferences, message);
            }
        }
    }

    /// <summary>Shows the item search text field.</summary>
    void ShowItemSearch() =>
        _ = ImGuiRenderer.InputTextWithHint("##ItemSearch", "Search...", ref _itemSearch, ushort.MaxValue);

    /// <summary>Shows the location search text field.</summary>
    void ShowLocationSearch() =>
        _ = ImGuiRenderer.InputTextWithHint("##LocationSearch", "Search...", ref _locationSearch, ushort.MaxValue);

    /// <summary>Shows non-manual items</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowNonManualItems(Preferences preferences)
    {
        const string Default = ApWorldReader.Uncategorized;
        ShowItemSearch();
        GroupItems(Default, default).Where(x => x.IsMatch(_itemSearch)).Lazily(x => x.Show(preferences)).Enumerate();
    }

    /// <summary>Shows manual items.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowManualItems(Preferences preferences)
    {
        Debug.Assert(_evaluator is not null);
        _ = ImGui.Checkbox("Show pending items", ref _showYetToReceive);
        var setter = GetNextItemOpenSetter();
        ShowItemSearch();

        foreach (var (category, items) in _evaluator.CategoryToItems)
        {
            if (_evaluator.HiddenCategories.Contains(category))
                continue;

            var sum = GroupItems(category, items).Where(x => x.IsMatch(_itemSearch)).Sum(x => x.Count);

            if (sum is 0)
                continue;

            setter();

            if (!ImGui.CollapsingHeader($"{category} ({sum})###{category}:|ItemCategory"))
                continue;

            GroupItems(category, items).Where(x => x.IsMatch(_itemSearch)).Lazily(x => x.Show(preferences)).Enumerate();
        }
    }

    /// <summary>Convenience function for displaying a checkbox with a specific color.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <param name="location">The location that this checkbox represents.</param>
    /// <param name="category">The category that the location falls under, used to ensure IDs remain distinct.</param>
    void Checkbox(Preferences preferences, string location, string category)
    {
        ref var tuple = ref this[location];
        ImGui.PushStyleColor(ImGuiCol.Text, preferences[tuple.Status]);
        ImGui.Checkbox($"{location}###{location}:|{category}:|Location", ref tuple.Checked);
        CopyIfClicked(preferences, location);
        ImGui.PopStyleColor();
        tuple.Checked &= tuple.Status is not LocationStatus.Checked;
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

    /// <summary>Gets the message for having released locations.</summary>
    /// <returns>The message.</returns>
    string ReleaseMessage() => $"Released {_locations.Values.Count(x => x.Checked).Conjugate("location")}";

    /// <summary>Gets the message for releasing locations, showing how much time is left.</summary>
    /// <returns>The message.</returns>
    string ReleasingMessage() => $"{_confirmationTimer.TotalSeconds.Max(0):N2}s before release";
}
