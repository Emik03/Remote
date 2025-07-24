// SPDX-License-Identifier: MPL-2.0
namespace Remote.Domains;
#pragma warning disable CS9113
/// <summary>
/// Represents the set of requirements in order for a location or region to be considered reachable.
/// Traversal to determine whether this instance is fulfilled can be seen in <see cref="ApEvaluator"/>.
/// </summary>
[Choice(false)] // ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class ApLogic(
    ApLogic? grouping,
    (ApLogic? Left, ApLogic? Right) and,
    (ApLogic? Left, ApLogic? Right) or,
    ReadOnlyMemory<char> item,
    ReadOnlyMemory<char> category,
    (ReadOnlyMemory<char> Item, ReadOnlyMemory<char> Count) itemCount,
    (ReadOnlyMemory<char> Category, ReadOnlyMemory<char> Count) categoryCount,
    (ReadOnlyMemory<char> Item, ReadOnlyMemory<char> Percent) itemPercent,
    (ReadOnlyMemory<char> Category, ReadOnlyMemory<char> Percent) categoryPercent,
    (ReadOnlyMemory<char> Name, ReadOnlyMemory<char> Args) function
)
{
    /// <summary>Encapsulates the rented array from <see cref="ArrayPool{Token}.Shared"/>.</summary>
    /// <param name="tokens">The rented array.</param>
    struct RentedTokenArray(ApToken[] tokens) : ICollection<ApToken>, IDisposable, IReadOnlyList<ApToken>
    {
        /// <summary>Initializes a new instance of the <see cref="RentedTokenArray"/> struct.</summary>
        /// <param name="capacity">The minimum length of the array.</param>
        public RentedTokenArray(int capacity)
            : this(ArrayPool<ApToken>.Shared.Rent(capacity)) { }

        /// <inheritdoc />
        public readonly ApToken this[int index] => tokens[index];

        /// <summary>Gets the segment of what was written.</summary>
        public readonly ArraySegment<ApToken> Segment => new(tokens, 0, Count);

        /// <inheritdoc />
        readonly bool ICollection<ApToken>.IsReadOnly => false;

        /// <inheritdoc cref="IReadOnlyCollection{Token}.Count"/>
        public int Count { get; private set; }

        /// <inheritdoc />
        public void Add(ApToken item)
        {
            if (Count < tokens.Length)
                tokens[Count++] = item;
        }

        /// <inheritdoc />
        public void Clear() => Count = 0;

        /// <inheritdoc />
        public readonly void CopyTo(ApToken[] array, int arrayIndex) =>
            tokens.AsSpan().CopyTo(array.AsSpan(arrayIndex));

        /// <inheritdoc />
        public readonly void Dispose() => ArrayPool<ApToken>.Shared.Return(tokens);

        /// <inheritdoc />
        public readonly bool Contains(ApToken item) => tokens.Contains(item);

        /// <inheritdoc />
        public bool Remove(ApToken item)
        {
            var i = tokens.IndexOf(item);

            if (i is -1)
                return false;

            tokens.AsSpan(i--..Count--).CopyTo(tokens.AsSpan(i..Count));
            return true;
        }

        /// <inheritdoc />
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc />
        public readonly IEnumerator<ApToken> GetEnumerator() => tokens.AsEnumerable().GetEnumerator();
    }

    /// <summary>Whether to show errors.</summary>
    static bool s_displayErrors = true;

    /// <summary>Invoked when a parse error occurs.</summary>
#pragma warning disable CA1003
    public static event Func<string, string, IEnumerable<string>, Task<int?>>? OnError;
#pragma warning restore CA1003
    /// <summary>Determines whether this node is optimized.</summary>
    public bool IsOptimized { get; internal set; }

    /// <summary>Gets the value determining whether this instance is a yaml function.</summary>
    public bool IsYamlFunction =>
        this is { Function.Name.Span: "YamlCompare" or "YamlDisabled" or "YamlEnabled" } or
            { Grouping.IsYamlFunction: true };

    /// <summary>Gets the number of <see cref="ApLogic"/> instances, including itself.</summary>
    // ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
    public int Count => (_and.Left?.Count ?? 0) + (_and.Right?.Count ?? 0) + 1;

    /// <inheritdoc />
    public static bool operator ==(ApLogic? l, ApLogic? r) =>
        ReferenceEquals(l, r) ||
        (l is null
            ? r is null
            : r is not null &&
            l._itemCount.Item.Span.Equals(r._itemCount.Item.Span, StringComparison.Ordinal) &&
            l._itemCount.Count.Span.Equals(r._itemCount.Count.Span, StringComparison.Ordinal) &&
            (l._and.Left == r._and.Left && l._and.Right == r._and.Right || // Commutative Law
                l._and.Left == r._and.Right && l._and.Right == r._and.Left));

    /// <summary>Makes a requirement that either of the instances should be fulfilled.</summary>
    /// <param name="left">The left-hand side.</param>
    /// <param name="right">The right-hand side.</param>
    /// <returns>The new <see cref="ApLogic"/> instance.</returns>
    // ReSharper restore ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
    [return: NotNullIfNotNull(nameof(left)), NotNullIfNotNull(nameof(right))]
    [Pure] // ReSharper disable once ReturnTypeCanBeNotNullable
    public static ApLogic? operator |(ApLogic? left, ApLogic? right) =>
        left is null ? left.Check(right) : // Identity Law
        right is null ? right.Check(left) :
        left == right ? left.Check(right) : // Idempotent Law
        //    Input  -> Commutative Law -> Idempotent Law
        // A + B + A ->    A + A + B    ->     A + B
        left is { IsOr: true, Or: var (oll, olr) } && (oll == right || olr == right) ? left.Check(right) :
        //    Input  -> Idempotent Law
        // A + A + B ->     A + B
        right is { IsOr: true, Or: var (orl, orr) } && (orl == right || orr == right) ? right.Check(left) :
        //    Input    ->  Commutative Law  -> Absorption Law
        // (A * B) + A ->    A + (A * B)    ->       A
        left is { IsAnd: true, And: var (all, alr) } && (all == right || alr == right) ? right.Check(left) :
        //    Input    -> Absorption Law
        // A + (A * B) ->       A
        right is { IsAnd: true, And: var (arl, arr) } && (arl == right || arr == right) ? left.Check(right) :
        // This code was never in the bible.
        left is { IsOr: true, Or: var (olll, olrl) } && (olrl | right) is { IsOptimized: true } ll ? OfOr(ll, olll) :
        left is { IsOr: true, Or: var (ollr, olrr) } && (ollr | right) is { IsOptimized: true } rl ? OfOr(rl, olrr) :
        right is { IsOr: true, Or: var (orll, orrl) } && (left | orrl) is { IsOptimized: true } lr ? OfOr(orll, lr) :
        right is { IsOr: true, Or: var (orlr, orrr) } && (left | orlr) is { IsOptimized: true } rr ? OfOr(orrr, rr) :
        // We cannot optimize this.
        OfOr(left, right);

    /// <summary>Makes a requirement that both of the instances should be fulfilled.</summary>
    /// <param name="left">The left-hand side.</param>
    /// <param name="right">The right-hand side.</param>
    /// <returns>The new <see cref="ApLogic"/> instance.</returns>
    [return: NotNullIfNotNull(nameof(left)), NotNullIfNotNull(nameof(right))]
    [Pure] // ReSharper disable once ReturnTypeCanBeNotNullable
    public static ApLogic? operator &(ApLogic? left, ApLogic? right) =>
        left is null ? right.Check(left) : // Annulment Law
        right is null ? left.Check(right) :
        left == right ? left.Check(right) : // Idempotent Law
        //    Input    ->  Commutative Law -> Absorption Law
        // (A + B) * A ->    A * (A + B)   ->       A
        left is { IsOr: true, Or: var (oll, olr) } && (oll == right || olr == right) ? right.Check(left) :
        //    Input    ->  Absorption Law
        // A * (A + B) ->        A
        right is { IsOr: true, Or: var (orl, orr) } && (orl == right || orr == right) ? left.Check(right) :
        //   Input   -> Commutative Law -> Idempotent Law
        // A * B * A ->    A * A * B    ->     A * B
        left is { IsAnd: true, And: var (all, alr) } && (all == right || alr == right) ? left.Check(right) :
        //   Input   -> Idempotent Law
        // A * A * B ->     A * B
        right is { IsAnd: true, And: var (arl, arr) } && (arl == right || arr == right) ? right.Check(left) :
        // This code was never in the bible.
        left is { IsAnd: true, And: var (alll, alrl) } && (alll & right) is { IsOptimized: true } ll ? OfAnd(ll, alrl) :
        left is { IsAnd: true, And: var (allr, alrr) } && (allr & right) is { IsOptimized: true } rl ? OfAnd(rl, alrr) :
        right is { IsAnd: true, And: var (arll, arrl) } && (left & arll) is { IsOptimized: true } lr ? OfAnd(arrl, lr) :
        right is { IsAnd: true, And: var (arlr, arrr) } && (left & arlr) is { IsOptimized: true } rr ? OfAnd(arrr, rr) :
        // We cannot optimize this.
        OfAnd(left, right);

    /// <summary>Parses the sequence of tokens into the <see cref="ApLogic"/> object.</summary>
    /// <typeparam name="T">The type of list of tokens.</typeparam>
    /// <param name="tokens">The list of tokens.</param>
    /// <returns>The <see cref="ApLogic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    public static ApLogic? Parse<T>(T tokens)
        where T : IReadOnlyList<ApToken>
    {
        var i = 0;
        var ret = Binary(tokens, ref i);
        return i + 1 == tokens.Count && tokens[i].IsEOL ? ret : Error(tokens, i, false);
    }

    /// <summary>Parses the <see cref="ReadOnlyMemory{T}"/> directly to the <see cref="ApLogic"/> object.</summary>
    /// <param name="str">The sequences of characters to parse.</param>
    /// <returns>The <see cref="ApLogic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    public static ApLogic? TokenizeAndParse(string str) => TokenizeAndParse(str.AsMemory());

    /// <summary>Parses the <see cref="ReadOnlyMemory{T}"/> directly to the <see cref="ApLogic"/> object.</summary>
    /// <param name="memory">The sequences of characters to parse.</param>
    /// <returns>The <see cref="ApLogic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    public static ApLogic? TokenizeAndParse(ReadOnlyMemory<char> memory)
    {
        // Most compact valid logic possible is a repeated sequence of '|a:1%|OR|a:1%|OR|a:1%|OR|a:1%|…'.
        const int MinimumValidLogic = 8;
        RentedTokenArray array = new(memory.Length - (memory.Length + 1) / MinimumValidLogic);
        ApToken.Tokenize(memory, ref array);
        var ret = Parse(array.Segment);
#pragma warning disable IDISP017
        array.Dispose();
#pragma warning restore IDISP017
        return ret;
    }

    /// <summary>Converts this instance back into the <see cref="string"/> representation.</summary>
    /// <param name="and">
    /// The string to insert between two <see cref="ApLogic"/> instances to indicate an <c>AND</c> operation.
    /// </param>
    /// <param name="or">
    /// The string to insert between two <see cref="ApLogic"/> instances to indicate an <c>OR</c> operation.
    /// </param>
    /// <returns>The <see cref="string"/> representation that can be used to reconstruct this instance.</returns>
    public string ToBooleanAlgebra(string and, string or) => ToBooleanAlgebra(and, or, []);

    /// <summary>Converts this instance back into the <see cref="string"/> representation.</summary>
    /// <returns>The <see cref="string"/> representation that can be used to reconstruct this instance.</returns>
    public string ToMinimalString() => ToMinimalString(null);

    /// <summary>Converts this instance back into the <see cref="string"/> representation.</summary>
    /// <returns>The <see cref="string"/> representation that can be used to reconstruct this instance.</returns>
    public override string ToString() =>
        Map(
            x => $"({x})",
            x => $"{x.Left} AND {x.Right}",
            x => $"{x.Left} OR {x.Right}",
            x => $"|{x}|",
            x => $"|@{x}|",
            x => $"|{x.Item}:{x.Count}|",
            x => $"|@{x.Category}:{x.Count}|",
            x => $"|{x.Item}:{x.Percent}%|",
            x => $"|@{x.Category}:{x.Percent}%|",
            x => $"{{{x.Name}({x.Args})}}"
        );

    /// <summary>Gets the single node inside this <see cref="ApLogic"/> if exactly one exists.</summary>
    /// <returns>
    /// The resulting <see cref="ApLogic"/> object after parsing inside <c>OptOne</c>
    /// or <c>OptAll</c>, or <see cref="Grouping"/>, or <see langword="null"/>.
    /// </returns>
    public ApLogic? SingleOrDefault() =>
        Function is ({ Span: "OptOne" or "OptAll" }, var args) ? _and.Left ??= TokenizeAndParse(args) : Grouping;

    /// <summary>Consumes tokens for binary operations.</summary>
    /// <typeparam name="T">The type of list of tokens.</typeparam>
    /// <param name="tokens">The list of tokens.</param>
    /// <param name="i">The current index.</param>
    /// <returns>The <see cref="ApLogic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    static ApLogic? Binary<T>(T tokens, ref int i)
        where T : IReadOnlyList<ApToken>
    {
        if (Unary(tokens, ref i) is not { } left)
            return Error(tokens, i, false);

        if (tokens[i].IsAnd && i++ is var _)
            return Binary(tokens, ref i) is { } andRight ? OfAnd(left, andRight) : Error(tokens, i, false);

        if (!tokens[i].IsOr)
            return left;

        return i++ is var _ && Binary(tokens, ref i) is { } orRight
            ? OfOr(left, orRight)
            : Error(tokens, i, false);
    }

    /// <summary>Consumes tokens for unary operations.</summary>
    /// <typeparam name="T">The type of list of tokens.</typeparam>
    /// <param name="tokens">The list of tokens.</param>
    /// <param name="i">The current index.</param>
    /// <returns>The <see cref="ApLogic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    // ReSharper disable DuplicatedSequentialIfBodies
    static ApLogic? Unary<T>(T tokens, ref int i)
        where T : IReadOnlyList<ApToken>
    {
        if (tokens[i].IsPipe || i is 0 && i-- is var _)
            return Pipe(tokens, ref i);

        if (tokens[i].IsLeftCurly)
            return Curly(tokens, ref i);

        if (!tokens[i++].IsLeftParen)
            return Error(tokens, i);

        if (Binary(tokens, ref i) is not { } ret)
            return Error(tokens, i, false);

        return tokens[i++].IsRightParen ? OfGrouping(ret) : Error(tokens, i);
    }

    /// <summary>Parses the pipes and between.</summary>
    /// <typeparam name="T">The type of list of tokens.</typeparam>
    /// <param name="tokens">The list of tokens.</param>
    /// <param name="i">The current index.</param>
    /// <returns>The <see cref="ApLogic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    static ApLogic? Pipe<T>(T tokens, ref int i)
        where T : IReadOnlyList<ApToken>
    {
        var isPipeless = i < 0;
        var isCategory = tokens[++i].IsAt && i++ is var _;

        if (tokens[i++] is not { IsIdent: true, Ident: var identifier })
            return Error(tokens, i);

        if (isPipeless ? tokens[i].IsEOL : tokens[i].IsPipe && i++ is var _)
            return isCategory ? OfCategory(identifier) : OfItem(identifier);

        if (!tokens[i++].IsColon)
            return Error(tokens, i);

        if (tokens[i].IsAll && i++ is var _)
        {
            if (!tokens[i++].IsPipe)
                return Error(tokens, i);

            var all = "100".AsMemory();
            return isCategory ? OfCategoryPercent(identifier, all) : OfItemPercent(identifier, all);
        }

        if (tokens[i].IsHalf && i++ is var _)
        {
            if (!tokens[i++].IsPipe)
                return Error(tokens, i);

            var all = "50".AsMemory();
            return isCategory ? OfCategoryPercent(identifier, all) : OfItemPercent(identifier, all);
        }

        if (tokens[i++] is not { IsIdent: true, Ident: var amount })
            return Error(tokens, i);

        if (tokens[i].IsPipe && i++ is var _)
            return isCategory ? OfCategoryCount(identifier, amount) : OfItemCount(identifier, amount);

        if (!tokens[i++].IsPercent)
            return Error(tokens, i);

        if (tokens[i++].IsPipe)
            return isCategory ? OfCategoryPercent(identifier, amount) : OfItemPercent(identifier, amount);

        return Error(tokens, i);
    }

    /// <summary>Parses the curly braces and between.</summary>
    /// <typeparam name="T">The type of list of tokens.</typeparam>
    /// <param name="tokens">The list of tokens.</param>
    /// <param name="i">The current index.</param>
    /// <returns>The <see cref="ApLogic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    static ApLogic? Curly<T>(T tokens, ref int i)
        where T : IReadOnlyList<ApToken>
    {
        if (tokens[++i] is not { IsIdent: true, Ident: var name })
            return Error(tokens, i);

        if (!tokens[++i].IsLeftParen)
            return Error(tokens, i);

        if (tokens[++i] is not { IsIdent: true, Ident: var args })
            return Error(tokens, i);

        if (!tokens[++i].IsRightParen)
            return Error(tokens, i);

        if (!tokens[++i].IsRightCurly)
            return Error(tokens, i);

        i++;
        return OfFunction(name, args);
    }

    /// <summary>Shows the user that a parse error occured.</summary>
    /// <typeparam name="T">The type of list of tokens.</typeparam>
    /// <param name="tokens">The list of tokens.</param>
    /// <param name="i">The current index.</param>
    /// <param name="hasConsumedToken">
    /// Whether the mismatched token was consumed, in which case the previous token should be highlighted.
    /// </param>
    /// <param name="line">The line number to show where the error occured from.</param>
    /// <returns>The <see cref="ApLogic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    static ApLogic? Error<T>(T tokens, int i, bool hasConsumedToken = true, [CallerLineNumber] int line = 0)
        where T : IReadOnlyList<ApToken>
    {
        async Task? DisplayErrorAsync()
        {
            if (OnError is not { } onError)
                return;

            const int Lookaround = 3;
            const string Title = "Logic Parse Error";
            IEnumerable<string> buttons = ["Dismiss all", "Step to next error"];

            var description =
                $"Locations will have improper logic.\nUnable to parse \"{tokens[i - (hasConsumedToken ? 1 : 0)]
                }\" (index {i - (hasConsumedToken ? 1 : 0)
                }) from line {line}.\nLine: {tokens.Select(x => x.Detokenize()).Conjoin("")
                }\n\nTokens:\n\n{tokens.Index()
                   .Skip(i - Lookaround)
                   .Select(x => $"{x.Index}: {x.Item}{(x.Index == i ? " <<<<" : "")}")
                   .Take(Lookaround * 2 + 1)
                   .Conjoin('\n')
                }\n";

            if (await onError(Title, description, buttons) is not 1)
                s_displayErrors = false;
        }

        if (s_displayErrors || OnError is null)
            return null;
#pragma warning disable MA0134
        _ = Task.Run(DisplayErrorAsync).ConfigureAwait(false);
#pragma warning restore MA0134
        return null;
    }

    /// <summary>Converts this instance back into the <see cref="string"/> representation.</summary>
    /// <param name="and">
    /// The string to insert between two <see cref="ApLogic"/> instances to indicate an <c>AND</c> operation.
    /// </param>
    /// <param name="or">
    /// The string to insert between two <see cref="ApLogic"/> instances to indicate an <c>OR</c> operation.
    /// </param>
    /// <param name="list">The assignments for variables.</param>
    /// <returns>The <see cref="string"/> representation that can be used to reconstruct this instance.</returns>
    string ToBooleanAlgebra(string and, string or, List<ApLogic> list)
    {
        if (Grouping is { } g)
            return g.ToBooleanAlgebra(and, or, list);

        if (IsAnd)
            return $"({_and.Left?.ToBooleanAlgebra(and, or, list)}{and}{_and.Right?.ToBooleanAlgebra(and, or, list)})";

        if (IsOr)
            return $"({_and.Left?.ToBooleanAlgebra(and, or, list)}{or}{_and.Right?.ToBooleanAlgebra(and, or, list)})";

        var index = list.FindIndex(Equals) is not -1 and var variable ? variable : list.Count;

        if (index == list.Count)
            list.Add(this);

        return new([(char)((index %= 52) > 26 ? index - 26 + 'a' : index + 'A')]);
    }

    /// <summary>Converts this instance back into the <see cref="string"/> representation.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The <see cref="string"/> representation that can be used to reconstruct this instance.</returns>
    string ToMinimalString(bool? state) =>
        this switch
        {
            { IsGrouping: true, Grouping: var x } => x.ToMinimalString(state),
            { IsAnd: true, And: var (al, ar) } => state is false
                ? $"({al?.ToMinimalString(true)} AND {ar?.ToMinimalString(true)})"
                : $"{al?.ToMinimalString(true)} AND {ar?.ToMinimalString(true)}",
            { IsOr: true, Or: var (ol, or) } => state is true
                ? $"({ol?.ToMinimalString(false)} OR {or?.ToMinimalString(false)})"
                : $"{ol?.ToMinimalString(false)} OR {or?.ToMinimalString(false)}",
            _ => ToString(),
        };
}
