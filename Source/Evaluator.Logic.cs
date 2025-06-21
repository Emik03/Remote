// SPDX-License-Identifier: MPL-2.0
namespace Remote;

/// <inheritdoc cref="Evaluator"/>
public sealed partial record Evaluator
{
    /// <summary>The default category name.</summary>
    public const string Uncategorized = "(No Category)";

    /// <summary>Gets <see cref="ItemCount"/> with a <see cref="ReadOnlySpan{T}"/> lookup.</summary>
    public FrozenDictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> ItemCountSpan =>
        ItemCount.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>Gets <see cref="CategoryCount"/> with a <see cref="ReadOnlySpan{T}"/> lookup.</summary>
    public FrozenDictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> CategoryCountSpan =>
        CategoryCount.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>Gets <see cref="Yaml"/> with a <see cref="ReadOnlySpan{T}"/> lookup.</summary>
    public FrozenDictionary<string, int>.AlternateLookup<ReadOnlySpan<char>> YamlSpan =>
        Yaml.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>Determines whether the logic is fulfilled.</summary>
    /// <param name="logic">The logic to check.</param>
    /// <returns>Whether the parameter <paramref name="logic"/> is fulfilled.</returns>
    public bool In([NotNullWhen(false)] Logic? logic) =>
        logic?.Map(
            In,
            OnAnd,
            OnOr,
            OnItem,
            OnCategory,
            OnItemCount,
            OnCategoryCount,
            OnItemPercent,
            OnCategoryPercent,
            OnFunction
        ) is true or null;

    /// <summary>Determines whether the name of the location is in-logic.</summary>
    /// <param name="location">The location to check.</param>
    /// <returns>Whether the parameter <paramref name="location"/> is the name of a location that is in-logic.</returns>
    public bool InLogic(string location) => !LocationsToLogic.TryGetValue(location, out var logic) || In(logic);

    /// <summary>Determines whether both spans of characters are equal in contents.</summary>
    /// <param name="next">The left-hand side.</param>
    /// <param name="captured">The right-hand side.</param>
    /// <returns>Whether both spans of characters are equal in contents.</returns>
    static bool Eq(string next, ReadOnlyMemory<char> captured) => next.Equals(captured.Span, StringComparison.Ordinal);

    /// <summary>Determines whether the <c>AND</c> condition is fulfilled.</summary>
    /// <param name="tuple">The tuple to deconstruct.</param>
    /// <returns>Whether the <c>AND</c> condition is fulfilled.</returns>
    bool OnAnd((Logic Left, Logic Right) tuple) => In(tuple.Left) && In(tuple.Right);

    /// <summary>Determines whether the <c>OR</c> condition is fulfilled.</summary>
    /// <param name="tuple">The tuple to deconstruct.</param>
    /// <returns>Whether the <c>OR</c> condition is fulfilled.</returns>
    bool OnOr((Logic Left, Logic Right) tuple) => In(tuple.Left) || In(tuple.Right);

    /// <summary>Determines whether any of an item is obtained.</summary>
    /// <param name="item">The item to check.</param>
    /// <returns>Whether the item is obtained.</returns>
    bool OnItem(ReadOnlyMemory<char> item) =>
        IsOptAll && IsItemDisabled(item.Span) || Helper.AllItemsReceived.Any(x => Eq(x.ItemName, item));

    /// <summary>Determines whether any of a category is obtained.</summary>
    /// <param name="category">The category to check.</param>
    /// <returns>Whether any of a category is obtained.</returns>
    bool OnCategory(ReadOnlyMemory<char> category) =>
        IsOptAll && IsCategoryDisabled(category.Span) is true ||
        Helper.AllItemsReceived.Any(IsItemInCategory(category));

    /// <summary>Determines whether the specific quantity of an item is obtained.</summary>
    /// <param name="tuple">The tuple to deconstruct.</param>
    /// <returns>Whether the specific quantity of an item is obtained.</returns>
    bool OnItemCount((ReadOnlyMemory<char> Item, ReadOnlyMemory<char> Count) tuple) =>
        IsOptAll && IsItemDisabled(tuple.Item.Span) ||
        tuple.Count.Span.Into<int>() is var i &&
        (i is 0 || Helper.AllItemsReceived.Where(x => Eq(x.ItemName, tuple.Item)).Skip(i - 1).Any());

    /// <summary>Determines whether the specific quantity of a category is obtained.</summary>
    /// <param name="tuple">The tuple to deconstruct.</param>
    /// <returns>Whether the specific quantity of a category is obtained.</returns>
    bool OnCategoryCount((ReadOnlyMemory<char> Category, ReadOnlyMemory<char> Count) tuple) =>
        tuple.Count.Span.Into<int>().Min(OptCategoryCount(tuple.Category)) is var i &&
        (i is 0 || Helper.AllItemsReceived.Where(IsItemInCategory(tuple.Category)).Skip(i - 1).Any());

    /// <summary>Determines whether the specific percentage of an item is obtained.</summary>
    /// <param name="tuple">The tuple to deconstruct.</param>
    /// <returns>Whether the specific percentage of an item is obtained.</returns>
    bool OnItemPercent((ReadOnlyMemory<char> Item, ReadOnlyMemory<char> Percent) tuple) =>
        IsOptAll && IsItemDisabled(tuple.Item.Span) ||
        tuple.Percent.Span.Into<double>() <=
        Helper.AllItemsReceived.Count(x => Eq(x.ItemName, tuple.Item)) / (double)ItemCountSpan[tuple.Item.Span];

    /// <summary>Determines whether the specific percentage of a category is obtained.</summary>
    /// <param name="tuple">The tuple to deconstruct.</param>
    /// <returns>Whether the specific percentage of a category is obtained.</returns>
    bool OnCategoryPercent((ReadOnlyMemory<char> Category, ReadOnlyMemory<char> Percent) tuple) =>
        tuple.Percent.Span.Into<double>() <=
        Helper.AllItemsReceived.Count(IsItemInCategory(tuple.Category)) /
        (double)CategoryCountSpan[tuple.Category.Span].Min(OptCategoryCount(tuple.Category));

    /// <summary>Determines whether the function returns <see langword="true"/>.</summary>
    /// <param name="tuple">The tuple to deconstruct.</param>
    /// <returns>Whether the function returns <see langword="true"/>.</returns>
    bool OnFunction((ReadOnlyMemory<char> Name, ReadOnlyMemory<char> Args) tuple) =>
        tuple.Name.Span.Trim() switch
        {
            nameof(YamlEnabled) => YamlEnabled(tuple.Args),
            nameof(YamlDisabled) => YamlDisabled(tuple.Args),
            nameof(YamlCompare) => YamlCompare(tuple.Args),
            nameof(OptOne) => OptOne(tuple.Args),
            nameof(OptAll) => OptAll(tuple.Args),
            nameof(ItemValue) => ItemValue(tuple.Args),
            _ => true,
        };

    /// <summary>Determines whether the yaml option is enabled.</summary>
    /// <param name="yamlOption">The yaml option to check.</param>
    /// <returns>Whether the yaml option is enabled.</returns>
    bool YamlEnabled(ReadOnlyMemory<char> yamlOption) => YamlSpan[yamlOption.Span] is not 0;

    /// <summary>Determines whether the yaml option is disabled.</summary>
    /// <param name="yamlOption">The yaml option to check.</param>
    /// <returns>Whether the yaml option is disabled.</returns>
    bool YamlDisabled(ReadOnlyMemory<char> yamlOption) => YamlSpan[yamlOption.Span] is 0;

    /// <summary>Determines whether the comparison of the yaml value returns <paramref langword="true"/>.</summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <returns>Whether the comparison of the yaml value returns <paramref langword="true"/>.</returns>
    bool YamlCompare(ReadOnlyMemory<char> expression) =>
        expression.SplitWhitespace().GetReversedEnumerator() is var e &&
        e.MoveNext() &&
        e.Current is var count &&
        int.TryParse(count.Span, out var c) &&
        e.MoveNext() &&
        e.Current is var comparator &&
        YamlSpan[e.Body.Span] is var yaml &&
        comparator.Span switch
        {
            "=" or "==" => yaml == c,
            "!=" => yaml != c,
            ">" => yaml > c,
            ">=" => yaml >= c,
            "<" => yaml < c,
            "<=" => yaml <= c,
            _ => true,
        };

    /// <summary>Determines whether the item is disabled by yaml options, or the requirement itself is met.</summary>
    /// <param name="item">The item to check.</param>
    /// <returns>Whether the item is disabled by yaml options, or the requirement itself is met.</returns>
    bool OptOne(ReadOnlyMemory<char> item) =>
        IsItemDisabled(item.SplitOn(':').First.Span) || In(Logic.TokenizeAndParse(item));

    /// <summary>Evaluates <see cref="In"/> but with <see cref="IsOptAll"/> enabled.</summary>
    /// <param name="memory">The string of characters to parse.</param>
    /// <returns>The returned value from <see cref="In"/>.</returns>
    bool OptAll(ReadOnlyMemory<char> memory) =>
        (IsOptAll ? this : this with { IsOptAll = true }).In(Logic.TokenizeAndParse(memory));

    /// <summary>Determines whether the amount of a phantom item is sufficiently obtained.</summary>
    /// <param name="phantomItem">The phantom item to check.</param>
    /// <returns>Whether the amount of a phantom item is sufficiently obtained.</returns>
    bool ItemValue(ReadOnlyMemory<char> phantomItem) =>
        phantomItem.SplitOn(':').GetReversedEnumerator() is var e &&
        e.MoveNext() &&
        e.Current is var count &&
        e.Body is var item &&
        int.TryParse(count.Span, out var c) &&
        Helper.AllItemsReceived
           .Any(
                x => ItemValues.TryGetValue(x.ItemName, out var value) &&
                    value.Any(x => Eq(x.PhantomItem, item) && (c -= x.Count) <= 0)
            );

    /// <summary>Determines whether the item has been disabled by yaml options.</summary>
    /// <param name="item">The item to check.</param>
    /// <returns>Whether the item has been disabled by yaml options.</returns>
    bool IsItemDisabled(ReadOnlySpan<char> item)
    {
        if (!ItemToCategories.TryGetValue(item, out var categories))
            return false;

        var ret = false;

        foreach (var category in categories)
            switch (IsCategoryDisabled(category))
            {
                case false: return false;
                case null: continue;
                case true:
                    ret = true;
                    break;
            }

        return ret;
    }

    /// <summary>Determines whether the category has been disabled by yaml options.</summary>
    /// <remarks><para>
    /// <see langword="false"/> means the setting is explicitly enabled.
    /// <see langword="null"/> means the setting is implicitly enabled, without outright specifying.
    /// <see langword="true"/> means the setting is disabled.
    /// </para></remarks>
    /// <param name="category">The category to check.</param>
    /// <returns>Whether the category has been disabled by yaml options.</returns>
    bool? IsCategoryDisabled(ReadOnlySpan<char> category)
    {
        if (!CategoryToYaml.TryGetValue(category, out var yaml))
            return null;

        foreach (var y in yaml)
            if (YamlSpan.TryGetValue(y, out var i) && i > 0)
                return false;

        return true;
    }

    /// <summary>Counts the maximum amount in a category, factoring in whether the category is disabled.</summary>
    /// <param name="category">The category to check.</param>
    /// <returns>The number of items enabled in <paramref name="category"/>.</returns>
    int OptCategoryCount(ReadOnlyMemory<char> category)
    {
        if (!IsOptAll || !CategoryToItems.TryGetValue(category, out var items))
            return int.MaxValue;

        var sum = 0;

        foreach (var item in items)
            if (ItemCount.TryGetValue(item, out var count) && !IsItemDisabled(item))
                sum += count;

        return sum;
    }

    /// <summary>Returns the function to evaluate whether the specific item is in the provided category.</summary>
    /// <param name="category">The category to check against.</param>
    /// <returns>
    /// The function to evaluate whether the specific item is in a category
    /// that matches the name of the parameter <paramref name="category"/>.
    /// </returns>
    Func<ItemInfo, bool> IsItemInCategory(ReadOnlyMemory<char> category) =>
        item => ItemToCategories.TryGetValue(item.ItemName, out var value) && value.Any(x => Eq(x, category));
}
