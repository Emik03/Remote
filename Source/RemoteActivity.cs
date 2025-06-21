// SPDX-License-Identifier: MPL-2.0
#if ANDROID
namespace Remote;

using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using static Android.Content.PM.ConfigChanges;

/// <summary>The activity that runs <see cref="RemoteGame"/>.</summary>
[Activity(
     MainLauncher = true,
     Icon = "@drawable/icon",
     Label = "@string/app_name",
     AlwaysRetainTaskState = true,
     LaunchMode = LaunchMode.SingleInstance,
     ScreenOrientation = ScreenOrientation.FullUser,
     ConfigurationChanges = Orientation | Keyboard | KeyboardHidden | ScreenSize
 ), CLSCompliant(false)]
public sealed class RemoteActivity : AndroidGameActivity
{
    RemoteGame? _game;

    /// <inheritdoc />
    protected override void OnDestroy()
    {
        base.OnDestroy();
        _game?.Dispose();
        _game = null;
    }

    /// <inheritdoc />
    protected override void OnCreate(Bundle? bundle)
    {
        base.OnCreate(bundle);
        _game ??= new();
        SetContentView(_game.Services.GetService<View>());
    }
}
#endif
