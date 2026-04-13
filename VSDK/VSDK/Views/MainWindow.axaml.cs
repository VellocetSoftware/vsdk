using System.Reflection;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace VSDK.Views;

// ReSharper disable once PartialTypeWithSinglePart
public partial class MainWindow : Window
{
    private readonly TextBox _checklistTextBox;
    private readonly TextBox _diagnosticsTextBox;
    private readonly TextBox _guideTextBox;
    private readonly TextBlock _installRootTextBlock;
    private readonly TextBlock _lastCheckedTextBlock;
    private readonly LauncherService _launcherService;

    private readonly DispatcherTimer _refreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(3)
    };

    private readonly TextBlock _summaryTextBlock;
    private readonly TextBlock _versionTextBlock;

    public MainWindow()
        : this(new LauncherService(new LauncherPaths(AppContext.BaseDirectory)))
    {
    }

    internal MainWindow(LauncherService launcherService)
    {
        _launcherService = launcherService;

        InitializeComponent();
        _versionTextBlock = RequireControl<TextBlock>("VersionTextBlock");
        _installRootTextBlock = RequireControl<TextBlock>("InstallRootTextBlock");
        _lastCheckedTextBlock = RequireControl<TextBlock>("LastCheckedTextBlock");
        _summaryTextBlock = RequireControl<TextBlock>("SummaryTextBlock");
        _checklistTextBox = RequireControl<TextBox>("ChecklistTextBox");
        _guideTextBox = RequireControl<TextBox>("GuideTextBox");
        _diagnosticsTextBox = RequireControl<TextBox>("DiagnosticsTextBox");

        InitializeHeader();
        RefreshStatusDisplay();
        _refreshTimer.Tick += RefreshTimerTick;
        Closed += MainWindowClosed;
        _refreshTimer.Start();
    }

    private void InitializeHeader()
    {
        _versionTextBlock.Text = $"Version: {GetVersionString()}";
        _installRootTextBlock.Text = $"Install Root: {_launcherService.Paths.InstallRoot}";
        _lastCheckedTextBlock.Text = "Last Checked: --";
    }

    private void MainWindowClosed(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
    }

    private void RefreshTimerTick(object? sender, EventArgs e)
    {
        RefreshStatusDisplay();
    }

    private void RefreshStatusDisplay()
    {
        var status = _launcherService.GetStatusSnapshot();
        _summaryTextBlock.Text = status.Summary;
        _summaryTextBlock.Foreground = status.IsReady ? Brushes.ForestGreen : Brushes.DarkOrange;
        _checklistTextBox.Text = status.Checklist;
        _guideTextBox.Text = status.Guide;
        _diagnosticsTextBox.Text = status.Diagnostics;
        _lastCheckedTextBlock.Text = $"Last Checked: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
    }

    private T RequireControl<T>(string name) where T : Control
    {
        return this.FindControl<T>(name) ??
               throw new InvalidOperationException($"Missing required control '{name}' of type {typeof(T).Name}.");
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private static string GetVersionString()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "dev" : version.ToString();
    }
}