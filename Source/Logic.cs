// SPDX-License-Identifier: MPL-2.0
namespace Remote;
#pragma warning disable CS9113
/// <summary>
/// Represents the set of requirements in order for a location or region to be considered reachable.
/// Traversal to determine whether this instance is fulfilled can be seen in <see cref="Evaluator"/>.
/// </summary>
[Choice] // ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class Logic(
    Logic? grouping,
    (Logic Left, Logic Right) and,
    (Logic Left, Logic Right) or,
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
    struct RentedTokenArray(Token[] tokens) : ICollection<Token>, IDisposable, IReadOnlyList<Token>
    {
        /// <summary>Initializes a new instance of the <see cref="RentedTokenArray"/> struct.</summary>
        /// <param name="capacity">The minimum length of the array.</param>
        public RentedTokenArray(int capacity)
            : this(ArrayPool<Token>.Shared.Rent(capacity)) { }

        /// <inheritdoc />
        public readonly Token this[int index] => tokens[index];

        /// <summary>Gets the segment of what was written.</summary>
        public readonly ArraySegment<Token> Segment => new(tokens, 0, Count);

        /// <inheritdoc />
        readonly bool ICollection<Token>.IsReadOnly => false;

        /// <inheritdoc cref="IReadOnlyCollection{Token}.Count"/>
        public int Count { get; private set; }

        /// <inheritdoc />
        public void Add(Token item)
        {
            if (Count < tokens.Length)
                tokens[Count++] = item;
        }

        /// <inheritdoc />
        public void Clear() => Count = 0;

        /// <inheritdoc />
        public readonly void CopyTo(Token[] array, int arrayIndex) => tokens.AsSpan().CopyTo(array.AsSpan(arrayIndex));

        /// <inheritdoc />
        public readonly void Dispose() => ArrayPool<Token>.Shared.Return(tokens);

        /// <inheritdoc />
        public readonly bool Contains(Token item) => tokens.Contains(item);

        /// <inheritdoc />
        public bool Remove(Token item)
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
        public readonly IEnumerator<Token> GetEnumerator() => tokens.AsEnumerable().GetEnumerator();
    }

    /// <summary>Whether to show errors in <see cref="MessageBox.Show"/>.</summary>
    static bool s_displayErrors = true;

    /// <summary>Gets the value determines whether this instance is a yaml function.</summary>
    public bool IsYamlFunction =>
        Function.Name.Span is "YamlCompare" or "YamlDisabled" or "YamlEnabled" || Grouping?.IsYamlFunction is true;

    /// <summary>Makes a requirement that either of the instances should be fulfilled.</summary>
    /// <param name="l">The left-hand side.</param>
    /// <param name="r">The right-hand side.</param>
    /// <returns>The new <see cref="Logic"/> instance.</returns>
    [return: NotNullIfNotNull(nameof(l)), NotNullIfNotNull(nameof(r))]
    [Pure]
    public static Logic? operator |(Logic? l, Logic? r) =>
        l is null ? r : // Identity Law
        r is null ? l :
        l.StructuralEquals(r) ? l : // Idempotent Law
        //    Input  -> Commutative Law -> Idempotent Law
        // A + B + A ->    A + A + B    ->     A + B
        l is { IsOr: true, Or: var (oll, olr) } && (oll.StructuralEquals(r) || olr.StructuralEquals(r)) ? l :
        //    Input  -> Idempotent Law
        // A + A + B ->     A + B
        r is { IsOr: true, Or: var (orl, orr) } && (orl.StructuralEquals(r) || orr.StructuralEquals(r)) ? r :
        //    Input    ->  Commutative Law  -> Absorption Law
        // (A * B) + A ->    A + (A * B)    ->       A
        l is { IsAnd: true, And: var (all, alr) } && (all.StructuralEquals(r) || alr.StructuralEquals(r)) ? r :
        //    Input    -> Absorption Law
        // A + (A * B) ->       A
        r is { IsAnd: true, And: var (arl, arr) } && (arl.StructuralEquals(r) || arr.StructuralEquals(r)) ? l :
        OfOr(l, r);

    /// <summary>Makes a requirement that both of the instances should be fulfilled.</summary>
    /// <param name="l">The left-hand side.</param>
    /// <param name="r">The right-hand side.</param>
    /// <returns>The new <see cref="Logic"/> instance.</returns>
    [return: NotNullIfNotNull(nameof(l)), NotNullIfNotNull(nameof(r))]
    [Pure]
    public static Logic? operator &(Logic? l, Logic? r) =>
        l is null ? r : // Annulment Law
        r is null ? l :
        l.StructuralEquals(r) ? l : // Idempotent Law
        //    Input    ->  Commutative Law -> Absorption Law
        // (A + B) * A ->    A * (A + B)   ->       A
        l is { IsOr: true, Or: var (oll, olr) } && (oll.StructuralEquals(r) || olr.StructuralEquals(r)) ? r :
        //    Input    ->  Absorption Law
        // A * (A + B) ->        A
        r is { IsOr: true, Or: var (orl, orr) } && (orl.StructuralEquals(r) || orr.StructuralEquals(r)) ? l :
        //   Input   -> Commutative Law -> Idempotent Law
        // A * B * A ->    A * A * B    ->     A * B
        l is { IsAnd: true, And: var (all, alr) } && (all.StructuralEquals(r) || alr.StructuralEquals(r)) ? l :
        //   Input   -> Idempotent Law
        // A * A * B ->     A * B
        r is { IsAnd: true, And: var (arl, arr) } && (arl.StructuralEquals(r) || arr.StructuralEquals(r)) ? r :
        OfAnd(l, r);

    /// <summary>Parses the sequence of tokens into the <see cref="Logic"/> object.</summary>
    /// <typeparam name="T">The type of list of tokens.</typeparam>
    /// <param name="tokens">The list of tokens.</param>
    /// <returns>The <see cref="Logic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    public static Logic? Parse<T>(T tokens)
        where T : IReadOnlyList<Token>
    {
        var i = 0;
        var ret = Binary(tokens, ref i);
        return tokens[i].IsEOL && i + 1 == tokens.Count ? ret : Error(tokens, i);
    }

    /// <summary>Parses the <see cref="ReadOnlyMemory{T}"/> directly to the <see cref="Logic"/> object.</summary>
    /// <param name="str">The sequences of characters to parse.</param>
    /// <returns>The <see cref="Logic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    public static Logic? TokenizeAndParse(string str) => TokenizeAndParse(str.AsMemory());

    /// <summary>Parses the <see cref="ReadOnlyMemory{T}"/> directly to the <see cref="Logic"/> object.</summary>
    /// <param name="memory">The sequences of characters to parse.</param>
    /// <returns>The <see cref="Logic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    public static Logic? TokenizeAndParse(ReadOnlyMemory<char> memory)
    {
        // Most compact valid logic possible is a repeated sequence of '|a:1%|OR|a:1%|OR|a:1%|OR|a:1%|…'.
        const int MinimumValidLogic = 8;
        RentedTokenArray array = new(memory.Length - (memory.Length + 1) / MinimumValidLogic);
        Token.Tokenize(memory, ref array);
        var ret = Parse(array.Segment);
#pragma warning disable IDISP017
        array.Dispose();
#pragma warning restore IDISP017
        return ret;
    }

    /// <summary>Determines whether logic contains structurally the same data.</summary>
    /// <param name="other">The logic to compare to.</param>
    /// <returns>Whether both instances are equal.</returns>
    public bool StructuralEquals(Logic other) =>
        _discriminator == other._discriminator &&
        (_and is not ({ } left, { } right) || // Commutative Law
            left.StructuralEquals(other._and.Left) && right.StructuralEquals(other._and.Right) ||
            left.StructuralEquals(other._and.Right) && right.StructuralEquals(other._and.Left)) &&
        _item.Span.Equals(other._item.Span, StringComparison.Ordinal) &&
        _itemCount.Item.Span.Equals(other._itemCount.Item.Span, StringComparison.Ordinal) &&
        _itemCount.Count.Span.Equals(other._itemCount.Count.Span, StringComparison.Ordinal);

    /// <summary>Converts this instance back into the <see cref="string"/> representation.</summary>
    /// <param name="and">
    /// The string to insert between two <see cref="Logic"/> instances to indicate an <c>AND</c> operation.
    /// </param>
    /// <param name="or">
    /// The string to insert between two <see cref="Logic"/> instances to indicate an <c>OR</c> operation.
    /// </param>
    /// <returns>The <see cref="string"/> representation that can be used to reconstruct this instance.</returns>
    public string BooleanAlgebra(string and, string or) => BooleanAlgebra(and, or, []);

    /// <summary>Converts this instance back into the <see cref="string"/> representation.</summary>
    /// <returns>The <see cref="string"/> representation that can be used to reconstruct this instance.</returns>
    public string Deparse() =>
        Map(
            x => $"({x?.Deparse()})",
            x => $"{x.Left.Deparse()} AND {x.Right.Deparse()}",
            x => $"{x.Left.Deparse()} OR {x.Right.Deparse()}",
            x => $"|{x}|",
            x => $"|@{x}|",
            x => $"|{x.Item}:{x.Count}|",
            x => $"|@{x.Category}:{x.Count}|",
            x => $"|{x.Item}:{x.Percent}%|",
            x => $"|@{x.Category}:{x.Percent}%|",
            x => $"{{{x.Name}({x.Args})}}"
        );

    /// <summary>Converts this instance back into the <see cref="string"/> representation.</summary>
    /// <returns>The <see cref="string"/> representation that can be used to reconstruct this instance.</returns>
    public string DeparseDisplay() =>
        Map(
            x => $"({x?.DeparseDisplay()})",
            x => $"({x.Left.DeparseDisplay()} AND {x.Right.DeparseDisplay()})",
            x => $"({x.Left.DeparseDisplay()} OR {x.Right.DeparseDisplay()})",
            x => $"|{x}|",
            x => $"|@{x}|",
            x => $"|{x.Item}:{x.Count}|",
            x => $"|@{x.Category}:{x.Count}|",
            x => $"|{x.Item}:{x.Percent}%|",
            x => $"|@{x.Category}:{x.Percent}%|",
            x => $"{{{x.Name}({x.Args})}}"
        );

    /// <summary>Consumes tokens for binary operations.</summary>
    /// <typeparam name="T">The type of list of tokens.</typeparam>
    /// <param name="tokens">The list of tokens.</param>
    /// <param name="i">The current index.</param>
    /// <returns>The <see cref="Logic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    static Logic? Binary<T>(T tokens, ref int i)
        where T : IReadOnlyList<Token>
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
    /// <returns>The <see cref="Logic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    // ReSharper disable DuplicatedSequentialIfBodies
    static Logic? Unary<T>(T tokens, ref int i)
        where T : IReadOnlyList<Token>
    {
        if (tokens[i].IsPipe)
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
    /// <returns>The <see cref="Logic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    static Logic? Pipe<T>(T tokens, ref int i)
        where T : IReadOnlyList<Token>
    {
        var isCategory = tokens[++i].IsAt && i++ is var _;

        if (tokens[i++] is not { IsIdent: true, Ident: var identifier })
            return Error(tokens, i);

        if (tokens[i].IsPipe && i++ is var _)
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
    /// <returns>The <see cref="Logic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    static Logic? Curly<T>(T tokens, ref int i)
        where T : IReadOnlyList<Token>
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
    /// <param name="hasConsumedMismatchedToken">
    /// Whether the mismatched token was consumed, in which case the previous token should be highlighted.
    /// </param>
    /// <param name="line">The line number to show where the error occured from.</param>
    /// <returns>The <see cref="Logic"/> object if parsing was successful, otherwise <see langword="null"/>.</returns>
    static Logic? Error<T>(T tokens, int i, bool hasConsumedMismatchedToken = true, [CallerLineNumber] int line = 0)
        where T : IReadOnlyList<Token>
    {
        async Task? DisplayErrorAsync()
        {
            const int Lookaround = 3;
            const string Title = "Logic Parse Error";
            IEnumerable<string> buttons = ["Dismiss all", "Step to next error"];

            var description =
                $"Locations will have improper logic.\nUnable to parse \"{tokens[i - (hasConsumedMismatchedToken ? 1 : 0)]
                }\" (index {i - (hasConsumedMismatchedToken ? 1 : 0)
                }) from line {line}.\nLine: {tokens.Select(x => x.Detokenize()).Conjoin("")
                }\n\nTokens:\n\n{tokens.Index()
                   .Skip(i - Lookaround)
                   .Select(x => $"{x.Index}: {x.Item}{(x.Index == i ? " <<<<" : "")}")
                   .Take(Lookaround * 2 + 1)
                   .Conjoin('\n')
                }\n";

            if (await MessageBox.Show(Title, description, buttons) is not 1)
                s_displayErrors = false;
        }

        if (s_displayErrors)
            return null;
#pragma warning disable MA0134
        _ = Task.Run(DisplayErrorAsync).ConfigureAwait(false);
#pragma warning restore MA0134
        return null;
    }

    /// <summary>Converts this instance back into the <see cref="string"/> representation.</summary>
    /// <param name="and">
    /// The string to insert between two <see cref="Logic"/> instances to indicate an <c>AND</c> operation.
    /// </param>
    /// <param name="or">
    /// The string to insert between two <see cref="Logic"/> instances to indicate an <c>OR</c> operation.
    /// </param>
    /// <param name="list">The assignments for variables.</param>
    /// <returns>The <see cref="string"/> representation that can be used to reconstruct this instance.</returns>
    string BooleanAlgebra(string and, string or, List<Logic> list)
    {
        if (Grouping is { } g)
            return g.BooleanAlgebra(and, or, list);

        if (IsAnd)
            return $"({_and.Left.BooleanAlgebra(and, or, list)}{and}{_and.Right.BooleanAlgebra(and, or, list)})";

        if (IsOr)
            return $"({_and.Left.BooleanAlgebra(and, or, list)}{or}{_and.Right.BooleanAlgebra(and, or, list)})";

        var index = list.FindIndex(StructuralEquals) is not -1 and var variable ? variable : list.Count;

        if (index == list.Count)
            list.Add(this);

        return new([(char)((index %= 52) > 26 ? index - 26 + 'a' : index + 'A')]);
    }
}
