// SPDX-License-Identifier: MPL-2.0
#if !ANDROID
static void Hook() => ApLogic.OnError += MessageBox.Show;

static void Unhook() => ApLogic.OnError -= MessageBox.Show;

static Assembly? LoadFile(string name, ResolveEventArgs args) =>
    args.Name.StartsWith(name)
        ? Assembly.LoadFile(Path.Join(Path.GetDirectoryName(typeof(RemoteGame).Assembly.Location), $"{name}.dll"))
        : null;

AppDomain.CurrentDomain.AssemblyResolve += (_, a) => LoadFile("Remote.Portable", a) ?? LoadFile("Remote.Resources", a);

AppDomain.CurrentDomain.UnhandledException += (_, e) => File.WriteAllText(
    Path.Join(Path.GetTempPath(), $"{nameof(Remote)}.{Client.Now}.crash.txt"),
    e.ExceptionObject.ToString()
);

Process? process;

{
    Hook();
    using RemoteGame game = new();
    game.Run();
    process = game.ChildProcess;
    Unhook();
}

if (process is not null)
{
    await process.WaitForExitAsync();
    process.Dispose();
}

#endif
