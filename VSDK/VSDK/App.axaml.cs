using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VSDK.Views;

namespace VSDK;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var paths = new LauncherPaths(AppContext.BaseDirectory);
            var launcherService = new LauncherService(paths);

            desktop.MainWindow = new MainWindow(launcherService);
        }

        base.OnFrameworkInitializationCompleted();
    }
}