// SPDX-License-Identifier: MPL-2.0
namespace Remote;

/// <summary>Gets the exception for a death link.</summary>
sealed class DeathLinkException : Exception
{
    /// <inheritdoc />
    public DeathLinkException() { }

    /// <inheritdoc />
    public DeathLinkException(string message)
        : base(message) { }

    /// <inheritdoc />
    public DeathLinkException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>Initializes a new instance of the <see cref="DeathLinkException"/> class.</summary>
    /// <param name="deathLink">The death link to display.</param>
    public DeathLinkException(DeathLink deathLink)
        : base($"It's not a bug, it's a feature! {MessageOf(deathLink)}") =>
        Data[nameof(DeathLink)] = deathLink;

    /// <summary>Gets the message for <see cref="DeathLink"/>.</summary>
    /// <param name="deathLink">The death link.</param>
    /// <returns>The message representing the parameter <paramref name="deathLink"/>.</returns>
    public static string MessageOf(DeathLink deathLink) =>
        deathLink.Cause ??
        (Random.Shared.Next(0, 1000) is 0
            ? "death.fell.accident.water"
            : $"{deathLink.Source} fell out of this world.");
}
