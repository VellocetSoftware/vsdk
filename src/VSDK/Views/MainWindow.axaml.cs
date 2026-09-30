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
    private void SetBusy(bool busy)
    {
        this.FindControl<StackPanel>("Locations")!.IsEnabled = !busy;
        this.FindControl<StackPanel>("ResourcePanel")!.IsEnabled = !busy;
        this.FindControl<Button>("Configure")!.IsEnabled = !busy;
        this.FindControl<Button>("OpenVertexButton")!.IsEnabled = !busy;
        this.FindControl<Button>("CancelSetup")!.IsVisible = busy;
    }
    private async Task CheckAsync()
    {
        try
        {
            _status = Program.InitialSetup ?? await _service.SetupStatusAsync();
            Title = _status.Name + " SDK";
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
            var types = await _service.ResourceTypesAsync();
            this.FindControl<StackPanel>("ResourcePanel")!.IsVisible = types.Length > 0;
            this.FindControl<ComboBox>("ResourceType")!.ItemsSource = types;
            this.FindControl<ComboBox>("ResourceType")!.SelectedIndex = types.Length > 0 ? 0 : -1;
            SetStatus(_status.Ready ? "Setup is complete. You can change these locations if needed." :
                "Set up the engine to compile maps. Standalone resources can be authored immediately.");
        }
        catch (Exception error) { SetStatus(error.Message); }
    }

    private async void ConfigureClicked(object? sender, RoutedEventArgs args)
    {
        if (_setup != null) return;
        _setup = new CancellationTokenSource();
        SetBusy(true);
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
            SetBusy(false);
        }
    }

    private void CancelClicked(object? sender, RoutedEventArgs args) => _setup?.Cancel();
    private async void ChooseResourceClicked(object? sender, RoutedEventArgs args)
    {
        if (this.FindControl<ComboBox>("ResourceType")!.SelectedItem is not ResourceType type) return;
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Open " + type.Manifest, AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(type.Name) { Patterns = [type.Manifest] }] });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) SetResource(path);
    }

    private void SetResource(string path)
    {
        this.FindControl<TextBox>("ResourcePath")!.Text = path;
        this.FindControl<TextBox>("ResourceOutput")!.Text = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(path))!, "Builds");
    }

    private async void CreateResourceClicked(object? sender, RoutedEventArgs args)
    {
        if (this.FindControl<ComboBox>("ResourceType")!.SelectedItem is not ResourceType type) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new() { Title = "Choose a parent folder for the new " + type.Name, AllowMultiple = false });
        if (folders.Count == 0 || folders[0].TryGetLocalPath() is not { } parent || _setup != null) return;
        _setup = new CancellationTokenSource();
        SetBusy(true);
        try
        {
            var id = this.FindControl<TextBox>("NewResourceId")!.Text ?? "";
            if (id.Length is < 1 or > 96 || id.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' or '.')) || id is "." or "..")
                throw new InvalidOperationException("Choose a lowercase resource ID using letters, numbers, underscores, dots or hyphens.");
            var result = await _service.ResourceAsync("create", type, Path.Combine(parent, id),
                null, false, null, SetStatus, _setup.Token);
            SetResource(result.Trim());
            SetStatus("Resource created. Edit the manifest and source files, then build.");
        }
        catch (OperationCanceledException) { SetStatus("Resource creation canceled."); }
        catch (Exception error) { SetStatus(error.Message); }
        finally { _setup.Dispose(); _setup = null; SetBusy(false); }
    }

    private async void ResourceActionClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: string action } || _setup != null ||
            this.FindControl<ComboBox>("ResourceType")!.SelectedItem is not ResourceType type) return;
        var source = this.FindControl<TextBox>("ResourcePath")!.Text;
        if (string.IsNullOrWhiteSpace(source)) { SetStatus("Create or open a resource first."); return; }
        _setup = new CancellationTokenSource();
        SetBusy(true);
        try
        {
            var output = action is "build" or "test" ? this.FindControl<TextBox>("ResourceOutput")!.Text : null;
            if (action is "build" or "test" && string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Choose a build output folder.");
            SetStatus("Running " + action + "…");
            await _service.ResourceAsync(action, type, source, output,
                action is "build" or "test" && this.FindControl<CheckBox>("ResourceRelease")!.IsChecked == true,
                this.FindControl<TextBox>("GamePath")!.Text, SetStatus, _setup.Token);
            if (action == "edit") SetStatus("Save your source changes before building.");
        }
        catch (OperationCanceledException) { SetStatus("Resource operation canceled."); }
        catch (Exception error) { SetStatus(error.Message); }
        finally
        {
            _setup.Dispose(); _setup = null;
            SetBusy(false);
        }
    }
    private void OpenVertexClicked(object? sender, RoutedEventArgs args) => OpenVertex();
    private void OpenVertex()
    {
        try { _service.OpenVertex(Program.Map, this.FindControl<TextBox>("GamePath")!.Text); Close(); }
        catch (Exception error) { SetStatus(error.Message); }
    }
    private async void ChooseExecutableClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: string field }) return;
        string? path;
        if (OperatingSystem.IsMacOS()) path = App.PickMacExecutable("Choose executable or app");
        else
        {
            var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Choose executable", AllowMultiple = false });
            path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }
        if (path != null) this.FindControl<TextBox>(field)!.Text = path;
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
