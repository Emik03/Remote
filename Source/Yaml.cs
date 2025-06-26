// SPDX-License-Identifier: MPL-2.0
namespace Remote;

using DocumentStart = YamlDotNet.Core.Events.DocumentStart;
using Parser = YamlDotNet.Core.Parser;
using StreamStart = YamlDotNet.Core.Events.StreamStart;

/// <summary>Represents the yaml file in an Archipelago that contains exactly one player.</summary>
[Serializable]
public sealed class Yaml : IDictionary<string, object?>
{
    /// <summary>The field name.</summary>
    const string DescriptionField = "description", GameField = "game", GoalField = "goal", NameField = "name";

    /// <summary>Contains all of the yaml options. Booleans are coerced as integers.</summary>
    readonly Dictionary<string, int> _options = []; // ReSharper disable ReplaceWithFieldKeyword

    /// <summary>Contains the field that is contained within a slot.</summary>
    string _description = "", _game = "", _goal = "", _name = "", _path = "";

    /// <summary>Retrieves the value from the yaml option.</summary>
    /// <param name="span">The yaml option to get.</param>
    public int? this[ReadOnlySpan<char> span]
    {
        get => _options.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(span, out var value) ? value : null;
        set => _options.GetAlternateLookup<ReadOnlySpan<char>>().TryAdd(span, value ?? 0);
    }

    /// <inheritdoc />
    object? IDictionary<string, object?>.this[string key]
    {
        get =>
            key switch
            {
                DescriptionField => Description,
                GameField => Game,
                GoalField => Goal,
                NameField => Name,
                _ => _options[key],
            };
        set =>
            _ = key switch
            {
                DescriptionField => Description = value as string ?? Description,
                GameField => Game = value as string ?? Game,
                GoalField => Goal = GetMostProbableGoal(value) ?? Goal,
                NameField => Name = value as string ?? Name,
                _ when value is IDictionary<object, object> d => Add(d),
                _ when value is string s && int.TryParse(s, out var i) && (_options[key] = i) is var _ => s,
                _ => "",
            };
    }

    /// <inheritdoc />
    bool ICollection<KeyValuePair<string, object?>>.IsReadOnly => false;

    /// <inheritdoc />
    int ICollection<KeyValuePair<string, object?>>.Count => _options.Count + 4;

    /// <summary>Gets the reference to the description <see langword="string"/>.</summary>
    public ref string Description => ref _description;

    /// <summary>Gets the reference to the game <see langword="string"/>.</summary>
    public ref string Game => ref _game;

    /// <summary>Gets the reference to the goal <see langword="string"/>.</summary>
    public ref string Goal => ref _goal;

    /// <summary>Gets the reference to the name <see langword="string"/>.</summary>
    public ref string Name => ref _name;

    /// <summary>Gets the file path that was used to create this instance.</summary>
    public ref string Path => ref _path;

    /// <inheritdoc />
    ICollection<string> IDictionary<string, object?>.Keys =>
        [DescriptionField, GameField, GoalField, NameField, .._options.Keys];

    /// <inheritdoc />
    ICollection<object?> IDictionary<string, object?>.Values =>
        [Description, Game, Goal, Name, .._options.Values];

    /// <summary>Gets all of the yaml options. Booleans are coerced as integers.</summary>
    public IReadOnlyDictionary<string, int> Options => _options;

    /// <summary>Downcasts <see cref="_options"/>.</summary>
    IEnumerable<KeyValuePair<string, object?>> DowncastOptions =>
        _options.Select(x => new KeyValuePair<string, object?>(x.Key, x.Value));

    /// <summary>Gets the enumerable for the string fields.</summary>
    IEnumerable<KeyValuePair<string, object?>> Keys =>
        [new(DescriptionField, Description), new(GameField, Game), new(GoalField, Goal), new(NameField, Name)];

    /// <summary>
    /// Deserializes the file into the sequence of <see cref="Yaml"/> instances, each representing a player.
    /// </summary>
    /// <param name="path">The path containing a yaml file to deserialize.</param>
    /// <returns>The sequence of <see cref="Yaml"/> instances.</returns>
    public static IEnumerable<Yaml> FromFile(string path)
    {
        Parser parser = new(new StreamReader(File.OpenRead(path)));
        parser.Consume<StreamStart>();
        Deserializer deserializer = new();
        ICollection<Yaml> list = [];

        while (parser.Accept<DocumentStart>(out _))
        {
            var yaml = deserializer.Deserialize<Yaml>(parser);
            yaml.Path = path;
            list.Add(yaml);
        }

        return list;
    }

    /// <inheritdoc />
    void IDictionary<string, object?>.Add(string key, object? value) =>
        ((IDictionary<string, object?>)this)[key] = value;

    /// <inheritdoc />
    void ICollection<KeyValuePair<string, object?>>.Add(KeyValuePair<string, object?> item) =>
        ((IDictionary<string, object?>)this)[item.Key] = item.Value;

    /// <inheritdoc />
    void ICollection<KeyValuePair<string, object?>>.Clear()
    {
        Description = "";
        Goal = "";
        Game = "";
        Name = "";
        _options.Clear();
    }

    /// <inheritdoc />
    void ICollection<KeyValuePair<string, object?>>.CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
    {
        array[arrayIndex] = new(DescriptionField, Description);
        array[arrayIndex + 1] = new(GameField, Game);
        array[arrayIndex + 2] = new(GoalField, Goal);
        array[arrayIndex + 3] = new(NameField, Name);
        var i = arrayIndex + 3;

        foreach (var (key, value) in _options)
            array[++i] = new(key, value);
    }

    /// <summary>Extracts the keys from the <see cref="JsonObject"/> and copies its default values.</summary>
    /// <param name="obj">The <see cref="JsonObject"/> to copy from.</param>
    public void CopyFrom(JsonObject? obj)
    {
        if (obj?.TryGetPropertyValue("user", out var node) is false or null || node is not JsonObject options)
            return;

        foreach (var (key, value) in options)
            this[key] = value is JsonObject v && v.TryGetPropertyValue("default", out var def)
                ? def?.GetValueKind() switch
                {
                    JsonValueKind.True => 1,
                    JsonValueKind.False => 0,
                    JsonValueKind.Number => def.GetValue<int>(),
                    _ => null,
                }
                : null;
    }

    /// <inheritdoc />
    bool ICollection<KeyValuePair<string, object?>>.Contains(KeyValuePair<string, object?> item) =>
        ((IDictionary<string, object?>)this)[item.Key] is { } l ? l.Equals(item.Value) : item.Value is not null;

    /// <inheritdoc />
    bool ICollection<KeyValuePair<string, object?>>.Remove(KeyValuePair<string, object?> item) =>
        item.Key switch
        {
            DescriptionField => (Description = "") is var _,
            GameField => (Goal = "") is var _,
            GoalField => (Goal = "") is var _,
            NameField => (Name = "") is var _,
            _ => _options.Remove(item.Key),
        };

    /// <inheritdoc />
    bool IDictionary<string, object?>.ContainsKey(string key) =>
        key is DescriptionField or GameField or GoalField or NameField || _options.ContainsKey(key);

    /// <inheritdoc />
    bool IDictionary<string, object?>.Remove(string key) =>
        key switch
        {
            DescriptionField => (Description = "") is var _,
            GameField => (Goal = "") is var _,
            GoalField => (Goal = "") is var _,
            NameField => (Name = "") is var _,
            _ => _options.Remove(key),
        };

    /// <inheritdoc />
    bool IDictionary<string, object?>.TryGetValue(string key, [MaybeNullWhen(false)] out object value) =>
        key switch
        {
            DescriptionField => (value = Description) is var _,
            GameField => (value = Goal) is var _,
            GoalField => (value = Goal) is var _,
            NameField => (value = Name) is var _,
            _ when _options.TryGetValue(key, out var v) && (value = v) is var _ => true,
            _ => (value = null) is var _,
        };

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => Keys.Concat(DowncastOptions).GetEnumerator();

    /// <inheritdoc />
    IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator() =>
        Keys.Concat(DowncastOptions).GetEnumerator();

    /// <summary>Gets the goal that is most probable.</summary>
    /// <remarks><para>
    /// I am unaware of how to get the goal, so instead I simply look at the most likely one.
    /// </para></remarks>
    /// <param name="value">The value to deconstruct.</param>
    /// <returns>The most likely goal.</returns>
    static string? GetMostProbableGoal(object? value) =>
        value as string ??
        (value as IDictionary<object, object>)
      ?.MaxBy(x => x.Value is string s && int.TryParse(s, out var i) ? i : -1)
       .Key as string;

    /// <summary>Adds values from the dictionary into itself.</summary>
    /// <param name="dictionary">The dictionary to copy values from.</param>
    /// <returns>Always <see langword="null"/>.</returns>
    string? Add(IDictionary<object, object> dictionary)
    {
        foreach (var (k, v) in dictionary)
            if (k is string s)
                ((IDictionary<string, object?>)this)[s] = v;

        return null;
    }
}
