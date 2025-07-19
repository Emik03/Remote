// SPDX-License-Identifier: MPL-2.0
#if !ANDROID
static Assembly? LoadFile(string name, ResolveEventArgs args) =>
    args.Name.StartsWith(name)
        ? Assembly.LoadFile(Path.Join(Path.GetDirectoryName(typeof(RemoteGame).Assembly.Location), $"{name}.dll"))
        : null;

AppDomain.CurrentDomain.AssemblyResolve += (_, a) => LoadFile("Remote.Domains", a) ?? LoadFile("Remote.Resources", a);

AppDomain.CurrentDomain.UnhandledException += (_, e) => File.WriteAllText(
    Path.Join(Path.GetTempPath(), $"{nameof(Remote)}.{DateTime.Now.ToString("s").Replace(':', '_')}.crash.txt"),
    e.ExceptionObject.ToString()
);

Process? process;

{
    using RemoteGame game = new();
    game.Run();
    process = game.ChildProcess;
}

if (process is not null)
{
    await process.WaitForExitAsync();
    process.Dispose();
}
#endif
