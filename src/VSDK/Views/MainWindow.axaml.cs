// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace VSDK.Views;

public partial class MainWindow : Window
{
    private readonly LauncherService _service;
    public MainWindow() : this(Program.Service()) { }
    internal MainWindow(LauncherService service)
    {
        _service = service;
        AvaloniaXamlLoader.Load(this);
        this.FindControl<SelectableTextBlock>("InstallPath")!.Text = service.Paths.InstallRoot;
        Check();
    }
    private void Check()
    {
        try { SetStatus(_service.Inspect()); this.FindControl<Button>("OpenVertexButton")!.IsEnabled = true; }
        catch (Exception error) { SetStatus(error.Message); this.FindControl<Button>("OpenVertexButton")!.IsEnabled = false; }
    }
    private void SetStatus(string message) => this.FindControl<TextBlock>("Status")!.Text = message;
    private void CheckClicked(object? sender, RoutedEventArgs args) => Check();
    private void OpenVertexClicked(object? sender, RoutedEventArgs args) => OpenVertex();
    private void OpenVertex(string? project = null)
    { try { _service.OpenVertex(project); SetStatus("Vertex opened. Choose File → New project to start a map."); } catch (Exception error) { SetStatus(error.Message); } }
    private async void OpenProjectClicked(object? sender, RoutedEventArgs args)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Open Vertex project", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Vertex project") { Patterns = ["*.vertexproject"] }] });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) OpenVertex(path);
    }
    private async void DocumentationClicked(object? sender, RoutedEventArgs args)
    {
        try { if (!await Launcher.LaunchUriAsync(new Uri(LauncherService.DocumentationUrl))) SetStatus("The documentation could not be opened."); }
        catch (Exception error) { SetStatus(error.Message); }
    }
}
