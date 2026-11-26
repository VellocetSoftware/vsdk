// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Runtime.InteropServices;

namespace Vellocet.Central;

internal sealed class LauncherPaths
{
    public LauncherPaths(string executableDirectory, string? sdk = null, string? vertex = null)
    {
        var library = FindLibrary(executableDirectory);
        if (sdk != null && Directory.Exists(sdk))
        {
            var directory = Path.GetFullPath(sdk);
            var apps = Path.Combine(directory, "Apps");
            if (Directory.Exists(apps))
            {
                library = apps;
                Manifests = LibraryManifests(apps);
            }
            else Manifests = LocalManifests(directory);
            if (Manifests.Length == 0) throw new FileNotFoundException("No SDK manifest found in " + sdk);
        }
        else if (sdk != null) Manifests = [Path.GetFullPath(sdk)];
        else if (library != null) Manifests = LibraryManifests(library);
        else
        {
            Manifests = [];
            var current = new DirectoryInfo(executableDirectory);
            for (var i = 0; Manifests.Length == 0 && i < 8 && current != null; i++, current = current.Parent)
                Manifests = LocalManifests(current.FullName);
        }

        var sdkFolder = Manifests.Length == 1 ? Directory.GetParent(Manifests[0]) : null;
        if (sdkFolder?.Parent?.Name == "Apps") library = sdkFolder.Parent.FullName;
        var editors = library == null ? Path.Combine(sdkFolder?.FullName ?? executableDirectory, "Editor")
            : Path.Combine(library, "Vertex");
        var platform = OperatingSystem.IsMacOS() ? "osx" : OperatingSystem.IsWindows() ? "win" : "linux";
        var architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        var editor = Path.TrimEndingDirectorySeparator(Path.GetFullPath(vertex ?? Path.Combine(editors, platform + "-" + architecture,
            OperatingSystem.IsMacOS() ? "Vertex.app" : OperatingSystem.IsWindows() ? "Vertex.exe" : "Vertex")));
        VertexApplication = OperatingSystem.IsMacOS() && editor.EndsWith(".app", StringComparison.OrdinalIgnoreCase) ? editor : null;
        VertexExecutable = VertexApplication == null ? editor : Path.Combine(editor, "Contents", "MacOS", "Vertex");
    }

    private static string? FindLibrary(string directory)
    {
        for (var current = new DirectoryInfo(directory); current?.Parent != null; current = current.Parent)
            if (current.Name == "Launcher")
            {
                var apps = Path.Combine(current.Parent.FullName, "Apps");
                return Directory.Exists(apps) ? apps : null;
            }
        return null;
    }

    private static string[] LibraryManifests(string library) =>
        Directory.EnumerateDirectories(library).SelectMany(folder => Directory.GetFiles(folder, "*.vertexsdk"))
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();

    private static string[] LocalManifests(string directory)
    {
        var manifests = Directory.GetFiles(directory, "*.vertexsdk").ToList();
        var workspace = Path.Combine(directory, "Vertex");
        if (Directory.Exists(workspace)) manifests.AddRange(Directory.GetFiles(workspace, "*.vertexsdk"));
        return manifests.ToArray();
    }

    public string[] Manifests { get; }
    public string Manifest => Manifests.Length switch
    {
        1 => Manifests[0],
        0 => throw new FileNotFoundException("No game SDK found. Verify the SDK installation in Steam."),
        _ => throw new InvalidDataException("Multiple SDK manifests found. Select one with --sdk.")
    };
    public string InstallRoot => Path.GetDirectoryName(Manifest)!;
    public string VertexExecutable { get; }
    public string? VertexApplication { get; }
}
