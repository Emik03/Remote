// SPDX-License-Identifier: MPL-2.0
namespace Remote;

/// <summary>Represents the token from a larger <c>requires</c> strings.</summary>
[Choice.EOL.And.Or.LeftParen.RightParen.Pipe.At.Colon.All.Half.Percent.LeftCurly.RightCurly.Ident<ReadOnlyMemory<char>>]
public readonly partial struct Token
{
    /// <summary>The enumeration of different states that the tokenizer can go through.</summary>
    enum State
    {
        /// <summary>The tokenizer is currently reading a token.</summary>
        ReadingToken,

        /// <summary>The tokenizer is currently reading an identifier.</summary>
        ReadingIdentifier,

        /// <summary>The tokenizer is currently reading a quantity in the context of an identifier.</summary>
        ReadingIdentifierQuantity,

        /// <summary>The tokenizer is currently reading a function.</summary>
        ReadingFunction,

        /// <summary>The tokenizer is currently reading arguments in the context of a function.</summary>
        ReadingFunctionArguments,
    }

    /// <summary>Tokenizes the <see cref="ReadOnlyMemory{T}"/> and writes them into the list of tokens.</summary>
    /// <typeparam name="T">The type of list of tokens to write to.</typeparam>
    /// <param name="memory">The sequences of characters to parse.</param>
    /// <param name="tokens">The list of tokens to write to.</param>
    /// <exception cref="ArgumentOutOfRangeException">A state thought to be unreachable was reached.</exception>
    public static void Tokenize<T>(ReadOnlyMemory<char> memory, T tokens)
        where T : ICollection<Token>
    {
        int? start = null;
        var span = memory.Span;
        var state = State.ReadingToken;

        for (var i = 0; i < span.Length; i++)
            switch (state)
            {
                case State.ReadingToken:
                    ProcessToken(memory, tokens, ref state, ref start, i);
                    break;
                case State.ReadingIdentifier:
                    ProcessIdentifier(memory, tokens, ref state, ref start, i);
                    break;
                case State.ReadingIdentifierQuantity:
                    ProcessIdentifierQuantity(memory, tokens, ref state, ref start, i);
                    break;
                case State.ReadingFunction:
                    ProcessFunction(memory, tokens, ref state, ref start, i);
                    break;
                case State.ReadingFunctionArguments:
                    ProcessFunctionArguments(memory, tokens, ref state, ref start, i);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(memory), state, null);
            }

        tokens.Add(OfEOL());
    }

    /// <summary>Tokenizes the <see cref="ReadOnlyMemory{T}"/> into the list of tokens.</summary>
    /// <param name="memory">The sequences of characters to parse.</param>
    /// <returns>The list of tokens parsed.</returns>
    public static IReadOnlyList<Token> Tokenize(ReadOnlyMemory<char> memory)
    {
        List<Token> tokens = [];
        Tokenize(memory, tokens);
        return tokens;
    }

    /// <summary>Converts this instance back into the <see cref="string"/> representation.</summary>
    /// <returns>The <see cref="string"/> representation that can be used to reconstruct this instance.</returns>
    public string Detokenize() =>
        Map(
            () => "",
            () => " AND ",
            () => " OR ",
            () => "(",
            () => ")",
            () => "|",
            () => "@",
            () => ":",
            () => "ALL",
            () => "HALF",
            () => "%",
            () => "{",
            () => "}",
            x => x.ToString()
        );

    /// <summary>Processes a token.</summary>
    /// <typeparam name="T">The type of list of tokens to write to.</typeparam>
    /// <param name="memory">The sequence of characters to tokenize.</param>
    /// <param name="tokens">The list of tokens to write to.</param>
    /// <param name="state">The current state.</param>
    /// <param name="start">The index to remember that indicates the start of a buffer.</param>
    /// <param name="i">The index to be looking in <paramref name="memory"/>.</param>
    static void ProcessToken<T>(ReadOnlyMemory<char> memory, T tokens, ref State state, ref int? start, int i)
        where T : ICollection<Token>
    {
        switch (memory.Span[i])
        {
            case var c when char.IsWhiteSpace(c):
                AddAndOr(memory, tokens, ref start, i);
                break;
            case '|':
                AddAndOr(memory, tokens, ref start, i);
                tokens.Add(OfPipe());
                state = State.ReadingIdentifier;
                break;
            case '{':
                AddAndOr(memory, tokens, ref start, i);
                tokens.Add(OfLeftCurly());
                state = State.ReadingFunction;
                break;
            case '(':
                AddAndOr(memory, tokens, ref start, i);
                tokens.Add(OfLeftParen());
                break;
            case ')':
                AddAndOr(memory, tokens, ref start, i);
                tokens.Add(OfRightParen());
                break;
            default:
                start ??= i;
                break;
        }
    }

    /// <summary>Processes an identifier.</summary>
    /// <typeparam name="T">The type of list of tokens to write to.</typeparam>
    /// <param name="memory">The sequence of characters to tokenize.</param>
    /// <param name="tokens">The list of tokens to write to.</param>
    /// <param name="state">The current state.</param>
    /// <param name="start">The index to remember that indicates the start of a buffer.</param>
    /// <param name="i">The index to be looking in <paramref name="memory"/>.</param>
    static void ProcessIdentifier<T>(ReadOnlyMemory<char> memory, T tokens, ref State state, ref int? start, int i)
        where T : ICollection<Token>
    {
        var span = memory.Span;

        switch (span[i])
        {
            case '@' when span[i - 1] is '|':
                AddIdentifier(memory, tokens, ref start, i);
                tokens.Add(OfAt());
                break;
            case ':':
                AddIdentifier(memory, tokens, ref start, i);
                tokens.Add(OfColon());
                state = State.ReadingIdentifierQuantity;
                break;
            case '|':
                AddIdentifier(memory, tokens, ref start, i);
                tokens.Add(OfPipe());
                state = State.ReadingToken;
                break;
            default:
                start ??= i;
                break;
        }
    }

    /// <summary>Processes a quantity in the context of an identifier.</summary>
    /// <typeparam name="T">The type of list of tokens to write to.</typeparam>
    /// <param name="memory">The sequence of characters to tokenize.</param>
    /// <param name="tokens">The list of tokens to write to.</param>
    /// <param name="state">The current state.</param>
    /// <param name="start">The index to remember that indicates the start of a buffer.</param>
    /// <param name="i">The index to be looking in <paramref name="memory"/>.</param>
    static void ProcessIdentifierQuantity<T>(
        ReadOnlyMemory<char> memory,
        T tokens,
        ref State state,
        ref int? start,
        int i
    )
        where T : ICollection<Token>
    {
        switch (memory.Span[i])
        {
            case var c when char.IsWhiteSpace(c): break;
            case '%':
                AddIdentifierQuantity(memory, tokens, ref start, i);
                tokens.Add(OfPercent());
                break;
            case '|':
                AddIdentifierQuantity(memory, tokens, ref start, i);
                tokens.Add(OfPipe());
                state = State.ReadingToken;
                break;
            default:
                start ??= i;
                break;
        }
    }

    /// <summary>Processes a function.</summary>
    /// <typeparam name="T">The type of list of tokens to write to.</typeparam>
    /// <param name="memory">The sequence of characters to tokenize.</param>
    /// <param name="tokens">The list of tokens to write to.</param>
    /// <param name="state">The current state.</param>
    /// <param name="start">The index to remember that indicates the start of a buffer.</param>
    /// <param name="i">The index to be looking in <paramref name="memory"/>.</param>
    static void ProcessFunction<T>(ReadOnlyMemory<char> memory, T tokens, ref State state, ref int? start, int i)
        where T : ICollection<Token>
    {
        switch (memory.Span[i])
        {
            case var c when char.IsWhiteSpace(c): break;
            case '}':
                state = State.ReadingToken;
                tokens.Add(OfRightCurly());
                break;
            case '(':
                AddIdentifier(memory, tokens, ref start, i);
                state = State.ReadingFunctionArguments;
                tokens.Add(OfLeftParen());
                break;
            default:
                start ??= i;
                break;
        }
    }

    /// <summary>Processes arguments in the context of a function.</summary>
    /// <typeparam name="T">The type of list of tokens to write to.</typeparam>
    /// <param name="memory">The sequence of characters to tokenize.</param>
    /// <param name="tokens">The list of tokens to write to.</param>
    /// <param name="state">The current state.</param>
    /// <param name="start">The index to remember that indicates the start of a buffer.</param>
    /// <param name="i">The index to be looking in <paramref name="memory"/>.</param>
    static void ProcessFunctionArguments<T>(
        ReadOnlyMemory<char> memory,
        T tokens,
        ref State state,
        ref int? start,
        int i
    )
        where T : ICollection<Token>
    {
        switch (memory.Span[i])
        {
            case ')':
                AddIdentifier(memory, tokens, ref start, i);
                state = State.ReadingFunction;
                tokens.Add(OfRightParen());
                break;
            default:
                start ??= i;
                break;
        }
    }

    /// <summary>Adds AND or OR.</summary>
    /// <typeparam name="T">The type of list of tokens to write to.</typeparam>
    /// <param name="memory">The sequence of characters to tokenize.</param>
    /// <param name="tokens">The list of tokens to write to.</param>
    /// <param name="start">The index to remember that indicates the start of a buffer.</param>
    /// <param name="i">The index to be looking in <paramref name="memory"/>.</param>
    static void AddAndOr<T>(ReadOnlyMemory<char> memory, T tokens, ref int? start, int i)
        where T : ICollection<Token>
    {
        if (start is not { } s)
            return;

        var identifier = memory.Span[s..i];

        if (identifier.EqualsIgnoreCase("AND"))
            tokens.Add(OfAnd());
        else if (identifier.EqualsIgnoreCase("OR"))
            tokens.Add(OfOr());

        start = null;
    }

    /// <summary>Adds an identifier.</summary>
    /// <typeparam name="T">The type of list of tokens to write to.</typeparam>
    /// <param name="memory">The sequence of characters to tokenize.</param>
    /// <param name="tokens">The list of tokens to write to.</param>
    /// <param name="start">The index to remember that indicates the start of a buffer.</param>
    /// <param name="i">The index to be looking in <paramref name="memory"/>.</param>
    static void AddIdentifier<T>(ReadOnlyMemory<char> memory, T tokens, ref int? start, int i)
        where T : ICollection<Token>
    {
        if (start is not { } s)
            return;

        tokens.Add(OfIdent(memory[s..i]));
        start = null;
    }

    /// <summary>Adds the quantity within the context of an identifier.</summary>
    /// <typeparam name="T">The type of list of tokens to write to.</typeparam>
    /// <param name="memory">The sequence of characters to tokenize.</param>
    /// <param name="tokens">The list of tokens to write to.</param>
    /// <param name="start">The index to remember that indicates the start of a buffer.</param>
    /// <param name="i">The index to be looking in <paramref name="memory"/>.</param>
    static void AddIdentifierQuantity<T>(ReadOnlyMemory<char> memory, T tokens, ref int? start, int i)
        where T : ICollection<Token>
    {
        if (start is not { } s)
            return;

        var identifier = memory.Span[s..i];

        if (identifier.EqualsIgnoreCase("HALF"))
            tokens.Add(OfHalf());
        else if (identifier.EqualsIgnoreCase("ALL"))
            tokens.Add(OfAll());
        else
            tokens.Add(OfIdent(memory[s..i]));

        start = null;
    }
}
