// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;

namespace VSDK.Views;

internal sealed record LibrarySdk(string Manifest)
{
    public Bitmap? Icon { get; } = LoadIcon(Manifest);

    private static Bitmap? LoadIcon(string manifest)
    {
        var path = Path.Combine(Path.GetDirectoryName(manifest)!, "library-icon.png");
        if (!File.Exists(path)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            // Optional artwork must not prevent opening the SDK.
            return null;
        }
    }

    public string Name => Path.GetFileNameWithoutExtension(Manifest).Replace('_', ' ') + " SDK";
    public string ActionName => "Manage " + Name;
}

public partial class LibraryWindow : Window
{
    private readonly LauncherService _service;

    public LibraryWindow() : this(Program.Service()) { }

    internal LibraryWindow(LauncherService service)
    {
        _service = service;
        InitializeComponent();
        var sdks = service.Paths.Manifests.Select(path => new LibrarySdk(path)).ToArray();
        SdkList.ItemsSource = sdks;
        Closed += (_, _) =>
        {
            foreach (var sdk in sdks) sdk.Icon?.Dispose();
        };
        EmptyLibrary.IsVisible = service.Paths.Manifests.Length == 0;
        OpenEditor.IsEnabled = File.Exists(service.Paths.VertexExecutable);
        if (!OpenEditor.IsEnabled) ShowStatus("Vertex is missing. Verify the SDK installation in Steam.");
    }

    private void OpenEditorClicked(object? sender, RoutedEventArgs args)
    {
        try { _service.OpenEditor(); ShowStatus("Opened Vertex."); }
        catch (Exception error) { ShowStatus(error.Message); }
    }

    private async void ManageClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { DataContext: LibrarySdk sdk }) return;
        try { await new MainWindow(Program.Service(sdk.Manifest)).ShowDialog(this); }
        catch (Exception error) { ShowStatus(error.Message); }
    }

    private void ShowStatus(string message)
    {
        LibraryStatus.Text = message;
        LibraryStatus.IsVisible = true;
    }
}
