// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Reflection;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace VSDK.Views;

// ReSharper disable once PartialTypeWithSinglePart
public partial class MainWindow : Window
{
    private static readonly IBrush DarkTextBrush = new SolidColorBrush(Color.Parse("#060606"));
    private static readonly IBrush MutedTextBrush = new SolidColorBrush(Color.Parse("#A2A2A2"));
    private static readonly IBrush RedTextBrush = new SolidColorBrush(Color.Parse("#FF3030"));
    private static readonly IBrush SoftWhiteBrush = new SolidColorBrush(Color.Parse("#E9E9E9"));
    private static readonly IBrush WhiteTextBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));

    private readonly TextBlock _checkCountTextBlock;
    private readonly TextBlock _checkScoreTextBlock;
    private readonly StackPanel _checksPanel;
    private readonly TextBlock _contentEntryCountTextBlock;
    private readonly TextBlock _contentSchemaTextBlock;
    private readonly Button _copyPackagePathButton;
    private readonly Border _feedbackBorder;
    private readonly TextBlock _feedbackTextBlock;
    private readonly Border _headerStatusBadge;
    private readonly TextBlock _headerStatusTextBlock;
    private readonly Border _heroStatusBadge;
    private readonly TextBlock _heroStatusTextBlock;
    private readonly SelectableTextBlock _installRootTextBlock;
    private readonly ToggleButton _issuesOnlyToggleButton;
    private readonly TextBlock _lastCheckedTextBlock;
    private readonly LauncherService _launcherService;

    private readonly DispatcherTimer _refreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(5)
    };

    private readonly TextBlock _sdkVersionTextBlock;
    private readonly TextBlock _statusTitleTextBlock;
    private readonly TextBlock _summaryTextBlock;
    private readonly TextBlock _unityVersionTextBlock;
    private readonly TextBlock _versionTextBlock;
    private LauncherStatusSnapshot? _currentStatus;

    public MainWindow()
        : this(new LauncherService(new LauncherPaths(AppContext.BaseDirectory)))
    {
    }

    internal MainWindow(LauncherService launcherService)
    {
        _launcherService = launcherService;

        InitializeComponent();
        _versionTextBlock = RequireControl<TextBlock>("VersionTextBlock");
        _headerStatusBadge = RequireControl<Border>("HeaderStatusBadge");
        _headerStatusTextBlock = RequireControl<TextBlock>("HeaderStatusTextBlock");
        _statusTitleTextBlock = RequireControl<TextBlock>("StatusTitleTextBlock");
        _summaryTextBlock = RequireControl<TextBlock>("SummaryTextBlock");
        _heroStatusBadge = RequireControl<Border>("HeroStatusBadge");
        _heroStatusTextBlock = RequireControl<TextBlock>("HeroStatusTextBlock");
        _checkScoreTextBlock = RequireControl<TextBlock>("CheckScoreTextBlock");
        _feedbackBorder = RequireControl<Border>("FeedbackBorder");
        _feedbackTextBlock = RequireControl<TextBlock>("FeedbackTextBlock");
        _checkCountTextBlock = RequireControl<TextBlock>("CheckCountTextBlock");
        _issuesOnlyToggleButton = RequireControl<ToggleButton>("IssuesOnlyToggleButton");
        _checksPanel = RequireControl<StackPanel>("ChecksPanel");
        _sdkVersionTextBlock = RequireControl<TextBlock>("SdkVersionTextBlock");
        _unityVersionTextBlock = RequireControl<TextBlock>("UnityVersionTextBlock");
        _contentSchemaTextBlock = RequireControl<TextBlock>("ContentSchemaTextBlock");
        _contentEntryCountTextBlock = RequireControl<TextBlock>("ContentEntryCountTextBlock");
        _installRootTextBlock = RequireControl<SelectableTextBlock>("InstallRootTextBlock");
        _copyPackagePathButton = RequireControl<Button>("CopyPackagePathButton");
        _lastCheckedTextBlock = RequireControl<TextBlock>("LastCheckedTextBlock");

        InitializeHeader();
        RefreshStatusDisplay();
        _refreshTimer.Tick += RefreshTimerTick;
        Closed += MainWindowClosed;
        _refreshTimer.Start();
    }

    private void InitializeHeader()
    {
        _versionTextBlock.Text = $"APP {GetVersionString()}";
        _installRootTextBlock.Text = _launcherService.Paths.InstallRoot;
        _lastCheckedTextBlock.Text = "LAST CHECK —";
    }

    private void MainWindowClosed(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
    }

    private void RefreshTimerTick(object? sender, EventArgs e)
    {
        RefreshStatusDisplay();
    }

    private void RefreshButtonClicked(object? sender, RoutedEventArgs e)
    {
        RefreshStatusDisplay();
        ShowFeedback("Status refreshed from the installed distribution.");
    }

    private void IssuesOnlyToggleClicked(object? sender, RoutedEventArgs e)
    {
        RenderChecks();
    }

    private async void OpenDocumentationClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var opened = topLevel is not null &&
                         await topLevel.Launcher.LaunchUriAsync(new Uri(LauncherService.DocumentationUrl));
            ShowFeedback(opened
                ? "Opened the Vellocet SDK wiki in your default browser."
                : "The developer wiki could not be opened on this system.", !opened);
        }
        catch (Exception ex)
        {
            ShowFeedback($"The developer wiki could not be opened: {ex.Message}", true);
        }
    }

    private async void OpenInstallFolderClicked(object? sender, RoutedEventArgs e)
    {
        var installRoot = _launcherService.Paths.InstallRoot;
        try
        {
            var directory = new DirectoryInfo(installRoot);
            if (!directory.Exists)
            {
                ShowFeedback($"Install folder does not exist: {installRoot}", true);
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            var opened = topLevel is not null &&
                         await topLevel.Launcher.LaunchDirectoryInfoAsync(directory);
            ShowFeedback(opened
                ? "Opened the VSDK install folder."
                : "The VSDK install folder could not be opened on this system.", !opened);
        }
        catch (Exception ex)
        {
            ShowFeedback($"The VSDK install folder could not be opened: {ex.Message}", true);
        }
    }

    private async void CopyInstallRootClicked(object? sender, RoutedEventArgs e)
    {
        await CopyTextAsync(_launcherService.Paths.InstallRoot, "Install path copied.");
    }

    private async void CopyPackagePathClicked(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentStatus?.PackageManifestPath))
        {
            ShowFeedback("SDKPackage/package.json is not available to copy.", true);
            return;
        }

        await CopyTextAsync(_currentStatus.PackageManifestPath, "Package Manager path copied.");
    }

    private async void CopyDiagnosticsClicked(object? sender, RoutedEventArgs e)
    {
        if (_currentStatus is null)
        {
            ShowFeedback("Diagnostics are not available yet.", true);
            return;
        }

        await CopyTextAsync(_currentStatus.Diagnostics, "Diagnostics copied.");
    }

    private async Task CopyTextAsync(string text, string successMessage)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                ShowFeedback("The system clipboard is unavailable.", true);
                return;
            }

            await clipboard.SetTextAsync(text);
            await clipboard.FlushAsync();
            ShowFeedback(successMessage);
        }
        catch (Exception ex)
        {
            ShowFeedback($"Could not copy to the clipboard: {ex.Message}", true);
        }
    }

    private void RefreshStatusDisplay()
    {
        try
        {
            var status = _launcherService.GetStatusSnapshot();
            _currentStatus = status;

            _statusTitleTextBlock.Text = status.IsReady
                ? "Distribution ready"
                : "Distribution needs attention";
            _summaryTextBlock.Text = status.Summary;
            _checkScoreTextBlock.Text = status.IsReady
                ? $"{status.PassedCheckCount}/{status.Checks.Count} required checks passed. Unity setup can continue."
                : $"{status.FailedCheckCount} of {status.Checks.Count} required checks need attention.";

            var statusText = status.IsReady ? "READY" : "ACTION REQUIRED";
            SetStatusBadge(_headerStatusBadge, _headerStatusTextBlock, status.IsReady, statusText);
            SetStatusBadge(_heroStatusBadge, _heroStatusTextBlock, status.IsReady, statusText);

            _sdkVersionTextBlock.Text = FormatValue(status.PackageVersion);
            _unityVersionTextBlock.Text = FormatValue(status.RequiredUnityVersion);
            _contentSchemaTextBlock.Text = status.ContentSchemaVersion is > 0
                ? $"v{status.ContentSchemaVersion}"
                : "—";
            _contentEntryCountTextBlock.Text = status.ContentEntryCount > 0
                ? status.ContentEntryCount.ToString("N0", CultureInfo.InvariantCulture)
                : "—";
            _installRootTextBlock.Text = status.InstallRoot;
            _copyPackagePathButton.IsEnabled = !string.IsNullOrWhiteSpace(status.PackageManifestPath);
            _checkCountTextBlock.Text = $"{status.Checks.Count} TOTAL / {status.FailedCheckCount} ISSUES";
            _lastCheckedTextBlock.Text = $"LAST CHECK {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}";

            RenderChecks();
        }
        catch (Exception ex)
        {
            ShowFeedback($"VSDK could not refresh distribution status: {ex.Message}", true);
        }
    }

    private void RenderChecks()
    {
        _checksPanel.Children.Clear();
        if (_currentStatus is null)
            return;

        var checks = _issuesOnlyToggleButton.IsChecked == true
            ? _currentStatus.Checks.Where(check => !check.Passed).ToArray()
            : _currentStatus.Checks;

        if (checks.Count == 0)
        {
            _checksPanel.Children.Add(new Border
            {
                Classes = { "check-row" },
                Child = new TextBlock
                {
                    Text = "No unresolved distribution checks.",
                    Foreground = MutedTextBrush,
                    FontSize = 13
                }
            });
            return;
        }

        foreach (var check in checks)
            _checksPanel.Children.Add(BuildCheckRow(check));
    }

    private static Border BuildCheckRow(LauncherCheck check)
    {
        var markerText = new TextBlock
        {
            Text = check.Passed ? "OK" : "!",
            Foreground = DarkTextBrush,
            FontSize = 9,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var marker = new Border
        {
            Classes = { "check-marker", check.Passed ? "passed" : "failed" },
            Margin = new Thickness(0, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = markerText
        };

        var label = new TextBlock
        {
            Text = check.Label,
            Foreground = WhiteTextBrush,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 18, 0),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

        var detail = new SelectableTextBlock
        {
            Text = check.Detail,
            Foreground = check.Passed ? MutedTextBrush : SoftWhiteBrush,
            FontSize = 12,
            LineHeight = 18,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("40,190,*")
        };
        grid.Children.Add(marker);
        grid.Children.Add(label);
        grid.Children.Add(detail);
        Grid.SetColumn(label, 1);
        Grid.SetColumn(detail, 2);

        return new Border
        {
            Classes = { "check-row" },
            Child = grid
        };
    }

    private static string FormatValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    private static void SetStatusBadge(Border badge, TextBlock textBlock, bool isReady, string text)
    {
        badge.Classes.Remove("ready");
        badge.Classes.Remove("issue");
        badge.Classes.Add(isReady ? "ready" : "issue");
        textBlock.Text = text;
        textBlock.Foreground = DarkTextBrush;
    }

    private void ShowFeedback(string message, bool isError = false)
    {
        _feedbackBorder.Classes.Remove("error");
        if (isError)
            _feedbackBorder.Classes.Add("error");

        _feedbackTextBlock.Text = message;
        _feedbackTextBlock.Foreground = isError ? RedTextBrush : MutedTextBrush;
        _feedbackBorder.IsVisible = true;
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
        return version is null ? "dev" : version.ToString(3);
    }
}
