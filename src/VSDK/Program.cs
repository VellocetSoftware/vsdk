// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Avalonia;

namespace VSDK;

internal static class Program
{
    private static string? _sdk, _vertex;
    internal static string? Map { get; private set; }
    internal static SetupStatus? InitialSetup { get; private set; }
    internal static LauncherService Service() => new(new LauncherPaths(AppContext.BaseDirectory, _sdk, _vertex));
    [STAThread]
    public static void Main(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is not ("--sdk" or "--vertex" or "--map")) continue;
            if (i + 1 == args.Length) throw new ArgumentException("Missing path after " + args[i]);
            var option = args[i++];
            if (option == "--sdk") _sdk = args[i];
            else if (option == "--map") Map = args[i];
            else _vertex = args[i];
        }
        if (args.Contains("--check"))
        {
            try
            {
                var status = Service().SetupStatusAsync().GetAwaiter().GetResult();
                Console.WriteLine($"{status.Name} SDK · {status.Engine} {status.Version} · " +
                    (status.Ready ? "setup complete" : "setup required"));
            }
            catch (Exception error) { Console.Error.WriteLine(error.Message); Environment.ExitCode = 1; }
            return;
        }
        try
        {
            InitialSetup = Service().SetupStatusAsync().GetAwaiter().GetResult();
            if (InitialSetup.Ready && Map != null && !args.Contains("--setup"))
            {
                Service().OpenVertex(Map);
                return;
            }
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect();

#if DEBUG
        builder = builder.WithDeveloperTools();
#endif

        return builder
            .LogToTrace();
    }
}
