// SPDX-License-Identifier: MPL-2.0
namespace Remote;

using Color = System.Drawing.Color;
using Vector4 = System.Numerics.Vector4;
using XnaColor = Color;

/// <summary>Represents the <see cref="Vector4"/> that can be parsed as a hex color.</summary>
/// <param name="Vector">The color as <see cref="Vector4"/>.</param>
[StructLayout(LayoutKind.Auto)] // ReSharper disable once StructCanBeMadeReadOnly
public record struct AppColor(Vector4 Vector) : ISpanParsable<AppColor>
{
    /// <inheritdoc cref="byte.MaxValue"/>
    const float Max = byte.MaxValue;

    /// <summary>Initializes a new instances of the <see cref="AppColor"/> struct.</summary>
    /// <param name="color">The color to convert.</param>
    public AppColor(Color color)
        : this(new Vector4(color.R / Max, color.G / Max, color.B / Max, color.A / Max)) { }

    /// <summary>Gets the value for the default black.</summary>
    public static AppColor Black => new(Color.FromArgb(unchecked((int)0xFF191A21)));

    /// <summary>Gets the value for the default background.</summary>
    public static AppColor Background => new(Color.FromArgb(unchecked((int)0xFF282A36)));

    /// <summary>Gets the value for the default current line.</summary>
    public static AppColor CurrentLine => new(Color.FromArgb(unchecked((int)0xFF44475A)));

    /// <summary>Gets the value for the default foreground.</summary>
    public static AppColor Foreground => new(Color.FromArgb(unchecked((int)0xFFF8F8F2)));

    /// <summary>Gets the value for the default comment.</summary>
    public static AppColor Comment => new(Color.FromArgb(unchecked((int)0xFF6272A4)));

    /// <summary>Gets the value for the default cyan.</summary>
    public static AppColor Cyan => new(Color.FromArgb(unchecked((int)0xFF8BE9FD)));

    /// <summary>Gets the value for the default green.</summary>
    public static AppColor Green => new(Color.FromArgb(unchecked((int)0xFF50FA7B)));

    /// <summary>Gets the value for the default orange.</summary>
    public static AppColor Orange => new(Color.FromArgb(unchecked((int)0xFFFFB86C)));

    /// <summary>Gets the value for the default pink.</summary>
    public static AppColor Pink => new(Color.FromArgb(unchecked((int)0xFFFF79C6)));

    /// <summary>Gets the value for the default purple.</summary>
    public static AppColor Purple => new(Color.FromArgb(unchecked((int)0xFFBD93F9)));

    /// <summary>Gets the value for the default red.</summary>
    public static AppColor Red => new(Color.FromArgb(unchecked((int)0xFFFF5555)));

    /// <summary>Gets the value for the default yellow.</summary>
    public static AppColor Yellow => new(Color.FromArgb(unchecked((int)0xFFF1FA8C)));

    /// <summary>Gets itself as <see cref="XnaColor"/>.</summary>
    [CLSCompliant(false)]
    public readonly XnaColor XnaColor => new(Vector.X, Vector.Y, Vector.Z, Vector.W);

    /// <summary>Implicitly creates <seealso cref="AppColor"/> from <see cref="uint"/>.</summary>
    /// <param name="color">The color to get.</param>
    /// <returns>The <see cref="AppColor"/> from <see cref="uint"/>.</returns>
    [CLSCompliant(false)]
    public static implicit operator AppColor(uint color) => new(FromU32(color));

    /// <summary>Implicitly gets <see cref="Vector"/>.</summary>
    /// <param name="color">The color to get.</param>
    /// <returns>The <see cref="Vector4"/> from <see cref="Vector"/>.</returns>
    public static implicit operator Vector4(AppColor color) => color.Vector;

    /// <summary>Divides the color.</summary>
    /// <param name="color">The color.</param>
    /// <param name="divisor">The number to divide with.</param>
    /// <returns>The divided color.</returns>
    public static AppColor operator /(AppColor color, float divisor) => new(color.Vector / divisor);

    /// <inheritdoc cref="TryParse(string, IFormatProvider, out AppColor)"/>
    public static bool TryParse([NotNullWhen(true)] string? s, out AppColor result) =>
        TryParse(s.AsSpan(), CultureInfo.InvariantCulture, out result);

    /// <inheritdoc />
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out AppColor result) =>
        TryParse(s.AsSpan(), provider, out result);

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out AppColor result)
    {
        var ret = uint.TryParse(RemoveHeader(s), NumberStyles.HexNumber, provider, out var u);
        result = new(FromU32(u));
        return ret;
    }

    /// <inheritdoc cref="Parse(string, IFormatProvider)"/>
    public static AppColor Parse(string? s) => Parse(s.AsSpan(), CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public static AppColor Parse(string? s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <inheritdoc />
    public static AppColor Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
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
        new((byte)(u >> 24) / 255.0f, (byte)(u >> 16) / 255.0f, (byte)(u >> 8) / 255.0f, (byte)u / 255.0f);
}
