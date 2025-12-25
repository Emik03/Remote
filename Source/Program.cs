// SPDX-License-Identifier: MPL-2.0
#if !ANDROID
static Assembly? LoadFile(string name, ResolveEventArgs args) =>
    args.Name.StartsWith(name)
        ? Assembly.LoadFile(Path.Join(Path.GetDirectoryName(typeof(RemoteGame).Assembly.Location), $"{name}.dll"))
        : null;

static Process? Run()
{
    Hook();
    using RemoteGame game = new();
    game.Run();
    var process = game.ChildProcess;
    Unhook();
    return process;
}

static void Hook() => ApLogic.OnError += MessageBox.Show;

static void Unhook() => ApLogic.OnError -= MessageBox.Show;

AppDomain.CurrentDomain.AssemblyResolve += (_, a) => LoadFile("Remote.Portable", a) ?? LoadFile("Remote.Resources", a);

AppDomain.CurrentDomain.UnhandledException += (_, e) => File.WriteAllText(
    Path.Join(Path.GetTempPath(), $"{nameof(Remote)}.{Client.Now}.crash.txt"),
    e.ExceptionObject.ToString()
);

if (Run() is { } process)
{
    await process.WaitForExitAsync().ConfigureAwait(false);
    process.Dispose();
}
#endif
