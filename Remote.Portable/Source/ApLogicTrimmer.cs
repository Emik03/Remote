// SPDX-License-Identifier: MPL-2.0
namespace Remote.Portable;

/// <summary>
/// Provides methods to inspect values before one gets returned.
/// This is not a member method in order to allow <see keyword="null"/>.
/// </summary>
public static class ApLogicTrimmer
{
#if LOGIC_TRIM_CONSOLE_WRITE
    /// <summary>The maximum number of variables for assertions.</summary>
    /// <remarks><para>
    /// Increasing this number performs a more exhaustive search of logic with
    /// high amounts of variables, but this degrades performances rapidly.
    /// </para></remarks>
    const int MaxVariableCountForAssertions = 16;

    /// <summary>Contains every string that was printed before.</summary>
    static readonly HashSet<string> s_seen = new(FrozenSortedDictionary.Comparer);
#endif
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

    /// <summary>Returns itself, discarding the parameter. Prints both if compiled with a specific constant.</summary>
    /// <param name="left">The logic to return.</param>
    /// <param name="right">The logic to discard.</param>
    /// <param name="line">The caller line number to discard.</param>
    /// <returns>Itself.</returns>
#if !LOGIC_TRIM_CONSOLE_WRITE
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    [return: NotNullIfNotNull(nameof(left))]
#pragma warning disable MA0051
    internal static ApLogic? Check(this ApLogic? left, [UsedImplicitly] ApLogic? right, [CallerLineNumber] int line = 0)
#pragma warning restore MA0051
#if LOGIC_TRIM_CONSOLE_WRITE
    {
        const int Or = 120, And = Or + 32, Evaluator = 60;
        const string OrSymbol = "+", AndSymbol = "*", TrueSymbol = "{True}";
        left?.IsOptimized = true;

        static string Display(ApLogic? logic) => logic?.ToBooleanAlgebra(AndSymbol, OrSymbol) ?? TrueSymbol;

        Dictionary<int, string> dictionary = new()
        {
            [Evaluator + 1] = "Resolving (Left) as FALSE then Annulment Law in AND",
            [Evaluator + 2] = "Resolving (Right) as FALSE then Annulment Law in AND",
            [Evaluator + 9] = "Resolving (Left) as FALSE then Identity Law in OR",
            [Evaluator + 10] = "Resolving (Right) as FALSE then Identity Law in OR",
#if LOGIC_TRIM_NULL_CONSOLE_WRITE
            [Or + 1] = "Identity Law (Left) in OR",
            [Or + 2] = "Identity Law (Right) in OR",
#endif
            [Or + 5] = "Idempotent Law in AND",
            [Or + 8] = "Commutative Law then Idempotent Law in OR",
            [Or + 11] = "Idempotent Law in OR",
            [Or + 14] = "Commutative Law then Absorption Law in OR",
            [Or + 17] = "Absorption Law in OR",
#if LOGIC_TRIM_NULL_CONSOLE_WRITE
            [And + 1] = "Annulment Law (Left) in AND",
            [And + 2] = "Annulment Law (Right) in AND",
#endif
            [And + 5] = "Idempotent Law in AND",
            [And + 8] = "Commutative Law then Absorption Law in AND",
            [And + 11] = "Absorption Law in AND",
            [And + 14] = "Commutative Law then Idempotent Law in AND",
            [And + 17] = "Idempotent Law in AND",
        };

        if (!dictionary.TryGetValue(line, out var value))
            return left;

        var key = $"""
                   Found optimization: {value}
                   Preserving: {left?.ToString() ?? TrueSymbol}
                   Discarding: {right?.ToString() ?? TrueSymbol}
                   In boolean algebra…
                   Preserving: {Display(left)}
                   Discarding: {Display(right)}

                   """;

        if (!s_seen.Add(key))
            return left;

        Console.WriteLine(key);

        if (left is null || right is null || line < Or)
            return left;

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

        return left;
    }
#else
    {
        left?.IsOptimized = true;
        return left;
    }
#endif
    /// <summary>Returns itself, discarding the parameter. Prints both if compiled with a specific constant.</summary>
    /// <param name="left">The logic to return.</param>
    /// <param name="tuple">The tuple to discard.</param>
    /// <param name="evaluator">The evaluator to discard.</param>
    /// <param name="line">The caller line number to discard.</param>
    /// <returns>Itself.</returns>
#if !LOGIC_TRIM_CONSOLE_WRITE
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    [return: NotNullIfNotNull(nameof(left))]
    internal static ApLogic? Check(
        this ApLogic? left,
        [UsedImplicitly] in (ApLogic? Left, ApLogic? Right) tuple,
        [UsedImplicitly] ApEvaluator evaluator,
        [CallerLineNumber] int line = 0
    )
#if LOGIC_TRIM_CONSOLE_WRITE // ReSharper disable once ExplicitCallerInfoArgument
        =>
            Check(left, evaluator.In(tuple.Right), line);
#else
    {
        left?.IsOptimized = true;
        return left;
    }
#endif

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
}
