// SPDX-License-Identifier: MPL-2.0
#if !ANDROID
AppDomain.CurrentDomain.AssemblyResolve +=
    (_, a) => a.Name.StartsWith("Remote.Resources")
        ? Assembly.LoadFile(
            Path.Join(
                Path.GetDirectoryName(typeof(RemoteGame).Assembly.Location),
                $"{nameof(Remote)}.{nameof(Remote.Resources)}.dll"
            )
        )
        : null;

using RemoteGame game = new();
game.Run();
#endif
