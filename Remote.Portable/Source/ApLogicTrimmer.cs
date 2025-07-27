// SPDX-License-Identifier: MPL-2.0
namespace Remote.Portable;

/// <summary>
/// Provides methods to inspect values before one gets returned.
/// This is not a member method in order to allow <see keyword="null"/>.
/// </summary>
public static class ApLogicTrimmer
{
    /// <summary>The maximum number of variables for assertions.</summary>
    /// <remarks><para>
    /// Increasing this number performs a more exhaustive search of logic with
    /// high amounts of variables, but this degrades performances rapidly.
    /// </para></remarks>
    const int MaxVariableCountForAssertions = 16;

    /// <summary>Gets the truthy symbol.</summary>
    const string TrueSymbol = "{True}";

    /// <summary>The environment variables.</summary>
    static readonly bool s_debugLogicTrim = IsTrue("REMOTE_DEBUG_LOGIC_TRIM"),
        s_debugLogicTrimNull = IsTrue("REMOTE_DEBUG_LOGIC_TRIM_NULL");

    /// <summary>Contains every string that was printed before.</summary>
    static readonly HashSet<string> s_seen = new(FrozenSortedDictionary.Comparer);

    /// <summary>Tests whether this logic is fulfilled by the set of inputs.</summary>
    /// <typeparam name="T">The type of collection for logic.</typeparam>
    /// <param name="that">The logic to test.</param>
    /// <param name="lookup">The collection for logic.</param>
    /// <param name="inputs">The inputs.</param>
    /// <returns>Whether this logic is fulfilled by the set of inputs.</returns>
    public static bool Test<T>([NotNullWhen(false)] this ApLogic? that, T lookup, ReadOnlySpan<bool> inputs)
        where T : IList<ApLogic> =>
        that switch
        {
            null => true,
            { IsGrouping: true, Grouping: var g } => g.Test(lookup, inputs),
            { IsAnd: true, And: var (l, r) } => l.Test(lookup, inputs) & r.Test(lookup, inputs),
            { IsOr: true, Or: var (l, r) } => l.Test(lookup, inputs) | r.Test(lookup, inputs),
            _ => TestUnary(that, lookup, inputs),
        };

    /// <summary>Determines if the environment variable is true.</summary>
    /// <param name="variable">The environment variable.</param>
    /// <returns>Whether the parameter <paramref name="variable"/> is a truthy environment variable.</returns>
    public static bool IsTrue(string variable) =>
        Environment.GetEnvironmentVariable(variable) is var env && env.AsSpan().Trim() is ['1'] ||
        bool.TryParse(env, out var result) && result;

    /// <summary>Returns itself, discarding the parameter. Prints both if compiled with a specific constant.</summary>
    /// <param name="left">The logic to return.</param>
    /// <param name="right">The logic to discard.</param>
    /// <param name="line">The caller line number to discard.</param>
    /// <returns>Itself.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(left))]
    internal static ApLogic? Check(this ApLogic? left, [UsedImplicitly] ApLogic? right, [CallerLineNumber] int line = 0)
    {
        left?.IsOptimized = true;

        if (s_debugLogicTrim)
            Debug(left, right, line);

        return left;
    }

    /// <summary>Returns itself, discarding the parameter. Prints both if compiled with a specific constant.</summary>
    /// <param name="left">The logic to return.</param>
    /// <param name="tuple">The tuple to discard.</param>
    /// <param name="evaluator">The evaluator to discard.</param>
    /// <param name="line">The caller line number to discard.</param>
    /// <returns>Itself.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(left))]
    internal static ApLogic? Check(
        this ApLogic? left,
        [UsedImplicitly] in (ApLogic? Left, ApLogic? Right) tuple,
        [UsedImplicitly] ApEvaluator evaluator,
        [CallerLineNumber] int line = 0
    ) => // ReSharper disable once ExplicitCallerInfoArgument
        Check(left, s_debugLogicTrim ? evaluator.In(tuple.Right) : null, line);

    /// <summary>Performs assertions and printing.</summary>
    /// <param name="left">The logic to return.</param>
    /// <param name="right">The logic to discard.</param>
    /// <param name="line">The caller line number to look up.</param>
    static void Debug(ApLogic? left, ApLogic? right, int line)
    {
        const int Or = 120, And = Or + 32, Evaluator = 60;

        Dictionary<int, string?> dictionary = new()
        {
            [Evaluator + 1] = "Resolving (Left) as FALSE then Annulment Law in AND",
            [Evaluator + 2] = "Resolving (Right) as FALSE then Annulment Law in AND",
            [Evaluator + 9] = "Resolving (Left) as FALSE then Identity Law in OR",
            [Evaluator + 10] = "Resolving (Right) as FALSE then Identity Law in OR",
            [Or + 1] = s_debugLogicTrimNull ? "Identity Law (Left) in OR" : null,
            [Or + 2] = s_debugLogicTrimNull ? "Identity Law (Right) in OR" : null,
            [Or + 5] = "Idempotent Law in AND",
            [Or + 8] = "Commutative Law then Idempotent Law in OR",
            [Or + 11] = "Idempotent Law in OR",
            [Or + 14] = "Commutative Law then Absorption Law in OR",
            [Or + 17] = "Absorption Law in OR",
            [And + 1] = s_debugLogicTrimNull ? "Annulment Law (Left) in AND" : null,
            [And + 2] = s_debugLogicTrimNull ? "Annulment Law (Right) in AND" : null,
            [And + 5] = "Idempotent Law in AND",
            [And + 8] = "Commutative Law then Absorption Law in AND",
            [And + 11] = "Absorption Law in AND",
            [And + 14] = "Commutative Law then Idempotent Law in AND",
            [And + 17] = "Idempotent Law in AND",
        };

        if (!dictionary.TryGetValue(line, out var value) ||
            value is null ||
            $"""
             Found optimization: {value}
             Preserving: {left?.ToString() ?? TrueSymbol}
             Discarding: {right?.ToString() ?? TrueSymbol}
             In boolean algebra…
             Preserving: {Display(left)}
             Discarding: {Display(right)}

             """ is var key &&
            !s_seen.Add(key))
            return;

        Console.WriteLine(key);

        if (left is null || right is null || line < Or)
            return;

        IList<ApLogic> list = [];
        var combined = line >= And ? ApLogic.OfAnd(left, right) : ApLogic.OfOr(left, right);
        _ = combined.Test(list, []);
        var originalCount = list.Count;
        _ = left.Test(list, []);
        Trace.Assert(originalCount == list.Count);

        Enumerable.Range(0, list.Count.Min(MaxVariableCountForAssertions))
           .ToArray()
           .PowerSet()
           .Select(x => Enumerable.Range(0, list.Count).Select(x.Contains).ToArray())
           .Where(x => combined.Test(list, x) != left.Test(list, x))
           .Lazily(_ => Trace.Assert(originalCount == list.Count))
           .Lazily(x => Trace.Fail($"{x.Select(x => x ? 'X' : '_').Concat()}\n{Display(left)}\n{Display(combined)}"))
           .Enumerate();
    }

    /// <inheritdoc cref="Test{T}"/>
    static bool TestUnary<T>(this ApLogic that, T lookup, ReadOnlySpan<bool> inputs)
        where T : IList<ApLogic>
    {
        static bool Index(ReadOnlySpan<bool> inputs, int i) => (uint)i < (uint)inputs.Length && inputs[i];

        var i = 0;

        for (; i < lookup.Count && lookup[i] is var logic; i++)
            if (that == logic)
                return Index(inputs, i);

        lookup.Add(that);
        return Index(inputs, i);
    }

    /// <summary>Displays the logic.</summary>
    /// <param name="logic">The logic to display.</param>
    /// <returns>The displayed logic.</returns>
    static string Display(ApLogic? logic)
    {
        const string OrSymbol = "+", AndSymbol = "*";
        return logic?.ToBooleanAlgebra(AndSymbol, OrSymbol) ?? TrueSymbol;
    }
}
