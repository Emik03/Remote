// SPDX-License-Identifier: MPL-2.0
namespace Remote.Portable;

/// <summary>Represents the <see cref="Vector4"/> that can be parsed as a hex color.</summary>
/// <param name="Vector">The color as <see cref="Vector4"/>.</param>
[StructLayout(LayoutKind.Auto)] // ReSharper disable once StructCanBeMadeReadOnly
public record struct RemoteColor(Vector4 Vector)
    : IEqualityOperators<RemoteColor, RemoteColor, bool>, ISpanParsable<RemoteColor>
{
    /// <inheritdoc cref="byte.MaxValue"/>
    const float Max = byte.MaxValue;

    /// <summary>Initializes a new instances of the <see cref="RemoteColor"/> struct.</summary>
    /// <param name="color">The color to convert.</param>
    public RemoteColor(Color color)
        : this(new Vector4(color.R / Max, color.G / Max, color.B / Max, color.A / Max)) { }

    /// <summary>Gets the value for the default black.</summary>
    public static RemoteColor Black => new(Color.FromArgb(unchecked((int)0xFF191A21)));

    /// <summary>Gets the value for the default background.</summary>
    public static RemoteColor Background => new(Color.FromArgb(unchecked((int)0xFF282A36)));

    /// <summary>Gets the value for the default current line.</summary>
    public static RemoteColor CurrentLine => new(Color.FromArgb(unchecked((int)0xFF44475A)));

    /// <summary>Gets the value for the default foreground.</summary>
    public static RemoteColor Foreground => new(Color.FromArgb(unchecked((int)0xFFF8F8F2)));

    /// <summary>Gets the value for the default comment.</summary>
    public static RemoteColor Comment => new(Color.FromArgb(unchecked((int)0xFF6272A4)));

    /// <summary>Gets the value for the default cyan.</summary>
    public static RemoteColor Cyan => new(Color.FromArgb(unchecked((int)0xFF8BE9FD)));

    /// <summary>Gets the value for the default green.</summary>
    public static RemoteColor Green => new(Color.FromArgb(unchecked((int)0xFF50FA7B)));

    /// <summary>Gets the value for the default orange.</summary>
    public static RemoteColor Orange => new(Color.FromArgb(unchecked((int)0xFFFFB86C)));

    /// <summary>Gets the value for the default pink.</summary>
    public static RemoteColor Pink => new(Color.FromArgb(unchecked((int)0xFFFF79C6)));

    /// <summary>Gets the value for the default purple.</summary>
    public static RemoteColor Purple => new(Color.FromArgb(unchecked((int)0xFFBD93F9)));

    /// <summary>Gets the value for the default red.</summary>
    public static RemoteColor Red => new(Color.FromArgb(unchecked((int)0xFFFF5555)));

    /// <summary>Gets the value for the default yellow.</summary>
    public static RemoteColor Yellow => new(Color.FromArgb(unchecked((int)0xFFF1FA8C)));

    /// <summary>Implicitly creates <seealso cref="RemoteColor"/> from <see cref="uint"/>.</summary>
    /// <param name="color">The color to get.</param>
    /// <returns>The <see cref="RemoteColor"/> from <see cref="uint"/>.</returns>
    [CLSCompliant(false)]
    public static implicit operator RemoteColor(uint color) => new(FromU32(color));

    /// <summary>Implicitly gets <see cref="Vector"/>.</summary>
    /// <param name="color">The color to get.</param>
    /// <returns>The <see cref="Vector4"/> from <see cref="Vector"/>.</returns>
    public static implicit operator Vector4(RemoteColor color) => color.Vector;

    /// <summary>Divides the color.</summary>
    /// <param name="color">The color.</param>
    /// <param name="divisor">The number to divide with.</param>
    /// <returns>The divided color.</returns>
    public static RemoteColor operator /(RemoteColor color, float divisor) =>
        new(
            new Vector4(
                color.Vector.X / divisor,
                color.Vector.Y / divisor,
                color.Vector.Z / divisor,
                color.Vector.W
            )
        );

    /// <inheritdoc cref="TryParse(string, IFormatProvider, out RemoteColor)"/>
    public static bool TryParse([NotNullWhen(true)] string? s, out RemoteColor result) =>
        TryParse(s.AsSpan(), CultureInfo.InvariantCulture, out result);

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out RemoteColor result) =>
        TryParse(s.AsSpan(), provider, out result);

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out RemoteColor result)
    {
        var ret = uint.TryParse(RemoveHeader(s), NumberStyles.HexNumber, provider, out var u);
        result = new(FromU32(u));
        return ret;
    }

    /// <inheritdoc cref="Parse(string, IFormatProvider)"/>
    public static RemoteColor Parse(string? s) => Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public static RemoteColor Parse(string? s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <inheritdoc />
    public static RemoteColor Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
        new(FromU32(uint.Parse(RemoveHeader(s), NumberStyles.HexNumber, provider)));

    /// <inheritdoc />
    public readonly override string ToString() =>
        $"#{(byte)(Vector.X * byte.MaxValue)
            :X2}{(byte)(Vector.Y * byte.MaxValue)
            :X2}{(byte)(Vector.Z * byte.MaxValue)
            :X2}{(byte)(Vector.W * byte.MaxValue)
            :X2}";

    /// <summary>Removes the <c>0x</c> header from hex strings.</summary>
    /// <param name="span">The span to trim.</param>
    /// <returns>The trimmed span.</returns>
    static ReadOnlySpan<char> RemoveHeader(ReadOnlySpan<char> span) =>
        span switch
        {
            ['0', 'x', .. var rest] => rest,
            ['#', .. var rest] => rest,
            _ => span,
        };

    /// <summary>Converts the unsigned packed integer into <see cref="Vector4"/>.</summary>
    /// <param name="u">The unsigned packed integer to convert.</param>
    /// <returns>The <see cref="Vector4"/> from the parameter <paramref name="u"/>.</returns>
    static Vector4 FromU32(uint u) =>
        new((byte)(u >> 24) / Max, (byte)(u >> 16) / Max, (byte)(u >> 8) / Max, (byte)u / Max);
}
