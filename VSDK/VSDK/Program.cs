using System.Reflection;
using Avalonia;

namespace VSDK;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect();

        builder = ConfigureDeveloperTools(builder);

        return builder
            .WithInterFont()
            .LogToTrace();
    }

    private static AppBuilder ConfigureDeveloperTools(AppBuilder builder)
    {
#if DEBUG
        try
        {
            var extensionType = Type.GetType("Avalonia.DeveloperToolsExtensions, AvaloniaUI.DiagnosticsSupport");
            var method = extensionType?.GetMethod(
                "WithDeveloperTools",
                BindingFlags.Public | BindingFlags.Static,
                null,
                [typeof(AppBuilder)],
                null);

            if (method?.Invoke(null, [builder]) is AppBuilder configured) return configured;
        }
        catch
        {
            // Developer tools are optional for local debugging.
        }
#endif
        return builder;
    }
}