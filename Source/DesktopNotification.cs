// SPDX-License-Identifier: MPL-2.0
namespace Remote;

/// <summary>Exposes a simple method for displaying notification toasts.</summary>
public static partial class DesktopNotification
{
    /// <summary>The notification manager.</summary>
    static readonly FreeDesktopNotificationManager? s_notifier =
        OperatingSystem.IsFreeBSD() || OperatingSystem.IsLinux() ? new() : null;

    /// <summary>Initializes the notifier.</summary>
    static DesktopNotification()
    {
        if (s_notifier is not null)
            _ = Task.Run(s_notifier.Initialize);
    }

    /// <summary>Displays the notification.</summary>
    /// <param name="title">The title.</param>
    /// <param name="body">The body.</param>
    /// <param name="isWarning">Determines whether or not to display the warning icon.</param>
    public static void Notify(string title, string body, bool isWarning) =>
#pragma warning disable AsyncifyInvocation
        _ = Task.Run(
            s_notifier is not null ? () => s_notifier.ShowNotification(new() { Body = body, Title = title }) :
            OperatingSystem.IsMacOS() ? () => Mac(title, body) :
            OperatingSystem.IsWindows() ? () => Windows(title, body, isWarning) :
            () => MessageBox.Show(title, body, ["OK"])
        );
#pragma warning restore AsyncifyInvocation
    /// <summary>Creates the desktop notification for Windows.</summary>
    /// <param name="title">The title.</param>
    /// <param name="body">The body.</param>
    static async Task Mac(string title, string body)
    {
        var script = $"display notification \"{MacEscape(body)}\" with title \"{MacEscape(title)}\"";
        using var process = Process.Start("osascript", ["-e", script]);
        await process.WaitForExitAsync().ConfigureAwait(false);
    }

    /// <summary>Creates the desktop notification for Windows.</summary>
    /// <param name="title">The title.</param>
    /// <param name="body">The body.</param>
    /// <param name="isWarning">Determines whether or not to display the warning icon.</param>
    static async Task Windows(string title, string body, bool isWarning)
    {
        // Yes, I hate this just as much as you do. I mean, we're really in it now. I'm doing it like this because:
        // - Trying to reference DesktopNotifications.Windows causes runtime problems about failing to find assemblies.
        // - Trying to dynamically instantiate types from System.Drawing also causes the same runtime problems.
        // - Trying to reference System.Windows.Forms is a bad idea because it's part of the BCL (Base Class Library),
        //   which varies based on the exact runtime used, effectively locking the project into an exact runtime.
        // - Trying to reference Microsoft.Toolkit.Uwp.Notifications causes compiler errors about it being windows-only.
        // - Trying to reference Microsoft.Windows.SDK.NET would always end up failing to link during runtime.
        // Massive thanks to https://superuser.com/a/1523925 because I don't normally write powershell scripts!
        var script =
            $"""
             [void] [System.Reflection.Assembly]::LoadWithPartialName("System.Windows.Forms")
             $objNotifyIcon = New-Object System.Windows.Forms.NotifyIcon
             $objNotifyIcon.Icon = [System.Drawing.SystemIcons]::{(isWarning ? "Warning" : "Information")}
             $objNotifyIcon.BalloonTipIcon = "{(isWarning ? "Warn" : "Info")}"
             $objNotifyIcon.BalloonTipText = "{WindowsEscape(body)}"
             $objNotifyIcon.BalloonTipTitle = "{WindowsEscape(title)}"
             $objNotifyIcon.Visible = $True
             $objNotifyIcon.ShowBalloonTip(10000)
             """;

        using var process = Process.Start("powershell", ["-NoNewWindow", script]);
        await process.WaitForExitAsync().ConfigureAwait(false);
    }

    /// <summary>Gets the escaped string.</summary>
    /// <param name="text">The text to escape.</param>
    /// <returns>The escaped version of the parameter <paramref name="text"/>.</returns>
    static string MacEscape(string text) => OsaScript().Replace(text, "\\$&").SplitSpanWhitespace().ToString(" ");

    /// <summary>Gets the escaped string.</summary>
    /// <param name="text">The text to escape.</param>
    /// <returns>The escaped version of the parameter <paramref name="text"/>.</returns>
    static string WindowsEscape(string text) => Powershell().Replace(text, "`$&").SplitSpanWhitespace().ToString(" ");

    /// <summary>Gets the regex of special characters that need to be escaped.</summary>
    /// <returns>The special characters that need to be escaped.</returns>
    [GeneratedRegex("""[\\"]""")]
    private static partial Regex OsaScript();

    /// <summary>Gets the regex of special characters that need to be escaped.</summary>
    /// <returns>The special characters that need to be escaped.</returns>
    [GeneratedRegex(@"[*\\~;(%?.:@/]")]
    private static partial Regex Powershell();
}
