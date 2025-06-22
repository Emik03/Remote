// SPDX-License-Identifier: MPL-2.0
namespace Remote;

/// <inheritdoc cref="Client"/>
public sealed partial class Client
{
    /// <summary>Whether to show the dialog.</summary>
    bool _showAlreadyChecked, _showConfirmationDialog, _showOutOfLogic, _showYetToReceive;

    /// <summary>Whether the user is attempting to release.</summary>
    /// <remarks><para>
    /// <see langword="false"/> means this slot is not attempting to release.
    /// <see langword="null"/> means this slot has released.
    /// <see langword="true"/> means this slot is attempting to release.
    /// </para></remarks>
    bool? _isAttemptingToRelease;

    /// <summary>Contains the last amount of checks.</summary>
    int _lastItemCount = int.MinValue, _lastLocationCount = int.MaxValue;

    /// <summary>The current state of the text field.</summary>
    string _message = "", _itemSearch = "", _locationSearch = "";

    /// <summary>The amount of time before release.</summary>
    TimeSpan _confirmationTimer;

    /// <summary>Gets a value indicating whether locations are being released.</summary>
    bool IsReleasing => _confirmationTimer < TimeSpan.Zero;

    /// <summary>Calls <see cref="ImGui"/> a lot.</summary>
    /// <param name="gameTime">The time elapsed since.</param>
    /// <param name="preferences">The user preferences.</param>
    /// <returns>Whether this window is closed, and should be dequeued to allow the GC to free this instance.</returns>
    [CLSCompliant(false)]
    public bool Draw(GameTime gameTime, Preferences preferences)
    {
        var open = true;

        if (!ImGui.Begin(_windowName, ref open, ImGuiWindowFlags.HorizontalScrollbar) || !open)
        {
            ImGui.End();
            return !open;
        }

        ImGui.SetWindowFontScale(preferences.UiScale);

        if (_session is null)
            ShowBuilder(preferences);
        else
            ShowConnected(gameTime, preferences);

        ImGui.End();
        return false;
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
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, preferences[AppPalette.Trap]);
                    ImGui.TextWrapped(error);
                    ImGui.PopStyleColor();
                }

        ImGui.SeparatorText("Create");

        if (ImGui.Button("Connect") || enter)
            _ = Connect(preferences);
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

        ShowTextClient(preferences);
        ShowLocationClient(gameTime, preferences);
        ShowItemClient(preferences);
        ImGui.EndTabBar();
    }

    /// <summary>Shows the messaging client.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowTextClient(Preferences preferences)
    {
        const ImGuiInputTextFlags Flags = Preferences.TextFlags | ImGuiInputTextFlags.AllowTabInput;
        Debug.Assert(_session is not null);

        if (!ImGui.BeginTabItem("Text Client"))
            return;

        ShowPlayers(preferences);
        ShowLog(preferences);
        ImGui.SeparatorText("Message");
        ImGui.SetNextItemWidth(preferences.Width(250));
        var enter = ImGuiRenderer.InputText("##Message", ref _message, ushort.MaxValue, Flags);
        ImGui.SameLine();

        if (ImGui.Button("Send") || enter)
        {
            _session.Say(_message);
            _message = "";
        }

        ImGui.SameLine();
        ImGui.TextDisabled("(?)");

        if (ImGui.IsItemHovered())
            Tooltip(preferences, HelpMessage1);

        ImGui.SameLine();
        ImGui.TextDisabled("(?)");

        if (ImGui.IsItemHovered())
            Tooltip(preferences, HelpMessage2);

        ImGui.EndTabItem();
    }

    /// <summary>Shows the list of locations</summary>
    /// <param name="gameTime">The time elapsed since.</param>
    /// <param name="preferences">The user preferences.</param>
    void ShowLocationClient(GameTime gameTime, Preferences preferences)
    {
        Debug.Assert(_session is not null);

        if (!ImGui.BeginTabItem("Location Client"))
            return;

        if (_showConfirmationDialog)
            ShowConfirmationDialog(gameTime, preferences);
        else
            ShowLocations(preferences);

        ImGui.EndTabItem();
    }

    /// <summary>Shows the list of items.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowItemClient(Preferences preferences)
    {
        Debug.Assert(_session is not null);

        if (!ImGui.BeginTabItem("Item Client"))
            return;

        ImGui.SeparatorText("Filter");

        if (_evaluator is null)
            ShowNonManualItems(preferences);
        else
            ShowManualItems(preferences);

        ImGui.EndTabItem();
    }

    /// <summary>Shows the list of players.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowPlayers(Preferences preferences)
    {
        Debug.Assert(_session is not null);
        ImGui.SeparatorText("Players");

        if (!ImGui.BeginChild("Players", new(0, preferences.UiScale * 50)))
        {
            ImGui.EndChild();
            return;
        }

        foreach (var (team, players) in _session.Players.Players)
        {
            if (players is [])
                continue;

            ImGui.Text($"Team {team}:");

            if (ImGui.IsItemHovered())
                Tooltip(preferences, $"Contains {players.Count.Conjugate("player")}");

            ImGui.SameLine();

            for (var i = 0; i < players.Count && players[i] is var player; i++)
            {
                ImGui.Text(player.ToString());

                if (ImGui.IsItemHovered())
                    Tooltip(preferences, $"Running {player.Game} on slot #{player.Slot}");

                if (i + 1 < players.Count)
                    ImGui.SameLine();
            }
        }

        ImGui.EndChild();
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

        ShowMessages(preferences, 0);

        if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
            ImGui.SetScrollHereY(1);

        ImGui.EndChild();
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

        if (ImGui.Button("Cancel"))
            _showConfirmationDialog = false;

        ImGui.SameLine();
        _ = ImGui.Button("Release");

        if (ImGui.IsItemActive())
            ShowCountdownToRelease(gameTime, preferences, outOfLogic);
        else
            ClearChecked();
    }

    /// <summary>Shows locations.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowLocations(Preferences preferences)
    {
        Debug.Assert(_session is not null);
        ImGui.SeparatorText("Filter");
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

        if (isAnyReleasable && ImGui.Button("Release"))
            _showConfirmationDialog = true;

        if (_canGoal is not null)
            return;

        if (isAnyReleasable)
            ImGui.SameLine();

        if (!ImGui.Button("Goal"))
            return;

        _canGoal = true;
        _session.SetGoalAchieved();
        _session.SetClientState(ArchipelagoClientState.ClientGoal);
    }

    /// <summary>Shows the location list in a non-manual context.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <returns>Whether this slot is completed. Never <see langword="null"/>.</returns>
    bool? ShowNonManualLocations(Preferences preferences)
    {
        Debug.Assert(_session is not null);
        ShowLocationSearch();
        ImGui.SeparatorText("List");
        ImGui.BeginChild("Locations", preferences.ChildSize());
        var locationHelper = _session.Locations;
        var locations = _showAlreadyChecked ? locationHelper.AllLocations : locationHelper.AllMissingLocations;

        foreach (var location in locations)
            if (locationHelper.GetLocationNameFromId(location, _yaml.Game) is { } name && ShouldBeVisible(name))
                Checkbox(preferences, name, Evaluator.Uncategorized);

        return locationHelper.AllMissingLocations.Count is 0;
    }

    /// <summary>Shows the location list in a manual context.</summary>
    /// <param name="preferences">The user preferences.</param>
    /// <returns>Whether this slot is completed, or <see langword="null"/> if BK'ed.</returns>
    bool? ShowManualLocations(Preferences preferences)
    {
        Debug.Assert(_evaluator is not null);
        _ = ImGui.Checkbox("Show Out of Logic Locations", ref _showOutOfLogic);
        ShowLocationSearch();
        ImGui.SeparatorText("List");
        ImGui.BeginChild("Locations", preferences.ChildSize());
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

            if (count is 0 || !ImGui.CollapsingHeader($"{category} ({count})###{category}:|LocationCategory"))
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
        if (!ImGui.BeginChild("Release", preferences.ChildSize(175)))
        {
            ImGui.EndChild();
            return false;
        }

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
                ImGui.TextColored(preferences[status], key);
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
            Release();
            return;
        }

        var color = preferences[outOfLogic ? AppPalette.ReleasingOutOfLogic : AppPalette.Releasing];
        _confirmationTimer -= gameTime.ElapsedGameTime;
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

            foreach (var part in parts)
            {
                if (!first || (first = false))
                    ImGui.SameLine();

                var color = preferences[part.PaletteColor switch
                {
                    PaletteColor.SlateBlue => AppPalette.Useful,
                    PaletteColor.Salmon => AppPalette.Trap,
                    PaletteColor.Plum => AppPalette.Progression,
                    _ => AppPalette.Neutral,
                }];

                ImGui.PushStyleColor(ImGuiCol.Text, color);
                ImGui.TextWrapped(part.Text);
                ImGui.PopStyleColor();
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
        ShowItemSearch();
        ImGui.SeparatorText("List");

        foreach (var (item, count) in GroupItems(Evaluator.Uncategorized, default))
            if (item.IsMatch(_itemSearch))
                item.Show(preferences, count);
    }

    /// <summary>Shows manual items.</summary>
    /// <param name="preferences">The user preferences.</param>
    void ShowManualItems(Preferences preferences)
    {
        Debug.Assert(_evaluator is not null);
        _ = ImGui.Checkbox("Show pending items", ref _showYetToReceive);
        ShowItemSearch();
        ImGui.SeparatorText("List");

        foreach (var (category, items) in _evaluator.CategoryToItems)
        {
            if (_evaluator.HiddenCategories.Contains(category))
                continue;

            var sum = GroupItems(category, items).Where(x => x.Item.IsMatch(_itemSearch)).Sum(x => x.Count);

            if (sum is 0 || !ImGui.CollapsingHeader($"{category} ({sum})###{category}:|ItemCategory"))
                continue;

            foreach (var (item, count) in GroupItems(category, items))
                if (item.IsMatch(_itemSearch))
                    item.Show(preferences, count);
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
