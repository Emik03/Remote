// SPDX-License-Identifier: MPL-2.0
namespace Remote;

/// <summary>Exposes a simple method for displaying notification toasts.</summary>
public static class DesktopNotification
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
    public static void Notify(string title, string body)
    {
        if (s_notifier is not null)
            _ = Task.Run(() => s_notifier.ShowNotification(new() { Body = body, Title = title }));
    }
}
