// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace VSDK.Views;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "The setup operation disposes its cancellation source in finally; closing cancels that operation.")]
public partial class MainWindow : Window
{
    private readonly LauncherService _service;
    private CancellationTokenSource? _setup;
    private SetupStatus? _status;
    public MainWindow() : this(Program.Service()) { }
    internal MainWindow(LauncherService service)
    {
        _service = service;
        AvaloniaXamlLoader.Load(this);
        this.FindControl<SelectableTextBlock>("InstallPath")!.Text = service.Paths.InstallRoot;
        Opened += async (_, _) => await CheckAsync();
        Closing += (_, _) => _setup?.Cancel();
    }

    private void SetStatus(string message) => this.FindControl<TextBlock>("Status")!.Text = message;
    private async Task CheckAsync()
    {
        try
        {
            _status = Program.InitialSetup ?? await _service.SetupStatusAsync();
            Title = _status.Name + " SDK setup";
            this.FindControl<TextBlock>("Heading")!.Text = Title;
            this.FindControl<TextBlock>("EngineRequirement")!.Text = $"{_status.Engine} {_status.Version} is required to compile maps.";
            this.FindControl<TextBox>("EnginePath")!.Text = _status.EngineExecutable;
            this.FindControl<TextBox>("GamePath")!.Text = _status.GameExecutable;
            this.FindControl<StackPanel>("GameSettings")!.IsVisible = _status.CanPlay;
            this.FindControl<Button>("InstallEngine")!.IsVisible = _status.InstallationUrl != null;
            this.FindControl<Button>("InstallEngine")!.Content = "Install " + _status.Engine;
            this.FindControl<TextBlock>("Workflow")!.Text = _status.Developer
                ? "This connection uses your game project. Save maps to the editor and play them there."
                : "Author in Vertex, then compile and play maps in your installed game. Your SDK supplies the assets and compiler.";
            this.FindControl<Button>("Configure")!.IsEnabled = true;
            this.FindControl<Button>("OpenVertexButton")!.IsEnabled = true;
            SetStatus(_status.Ready ? "Setup is complete. You can change these locations if needed." :
                "Finish setup once. Future SDK launches open Vertex directly. Activate your engine license and install the build module for your platform before continuing.");
        }
        catch (Exception error) { SetStatus(error.Message); }
    }

    private async void ConfigureClicked(object? sender, RoutedEventArgs args)
    {
        _setup = new CancellationTokenSource();
        this.FindControl<StackPanel>("Locations")!.IsEnabled = false;
        this.FindControl<Button>("Configure")!.IsEnabled = false;
        this.FindControl<Button>("OpenVertexButton")!.IsEnabled = false;
        this.FindControl<Button>("CancelSetup")!.IsVisible = true;
        try
        {
            SetStatus("Preparing the compiler. The first import can take several minutes…");
            await _service.ConfigureAsync(this.FindControl<TextBox>("EnginePath")!.Text,
                this.FindControl<TextBox>("GamePath")!.Text, SetStatus, _setup.Token);
            OpenVertex();
        }
        catch (OperationCanceledException) { SetStatus("Setup canceled. You can resume it later."); }
        catch (Exception error) { SetStatus(error.Message); }
        finally
        {
            _setup.Dispose(); _setup = null;
            this.FindControl<StackPanel>("Locations")!.IsEnabled = true;
            this.FindControl<Button>("Configure")!.IsEnabled = true;
            this.FindControl<Button>("OpenVertexButton")!.IsEnabled = true;
            this.FindControl<Button>("CancelSetup")!.IsVisible = false;
        }
    }

    private void CancelClicked(object? sender, RoutedEventArgs args) => _setup?.Cancel();
    private void OpenVertexClicked(object? sender, RoutedEventArgs args) => OpenVertex();
    private void OpenVertex()
    {
        try { _service.OpenVertex(Program.Map); Close(); }
        catch (Exception error) { SetStatus(error.Message); }
    }
    private async void ChooseExecutableClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: string field }) return;
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Choose executable or app", AllowMultiple = false });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
            this.FindControl<TextBox>(field)!.Text = path;
    }
    private async void InstallEngineClicked(object? sender, RoutedEventArgs args)
    {
        try
        {
            if (_status?.InstallationUrl is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                await Launcher.LaunchUriAsync(uri);
        }
        catch (Exception error) { SetStatus(error.Message); }
    }
    private async void DocumentationClicked(object? sender, RoutedEventArgs args)
    {
        try { await Launcher.LaunchUriAsync(new Uri(LauncherService.DocumentationUrl)); }
        catch (Exception error) { SetStatus(error.Message); }
    }
}
