// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Avalonia;

namespace VSDK;

internal static class Program
{
    private static string? _sdk, _vertex;
    internal static LauncherService Service() => new(new LauncherPaths(AppContext.BaseDirectory, _sdk, _vertex));
    [STAThread]
    public static void Main(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is not ("--sdk" or "--vertex")) continue;
            if (i + 1 == args.Length) throw new ArgumentException("Missing path after " + args[i]);
            if (args[i++] == "--sdk") _sdk = args[i]; else _vertex = args[i];
        }
        if (args.Contains("--check"))
        {
            try { Console.WriteLine(Service().Inspect()); }
            catch (Exception error) { Console.Error.WriteLine(error.Message); Environment.ExitCode = 1; }
            return;
        }
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
