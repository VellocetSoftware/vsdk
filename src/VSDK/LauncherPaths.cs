// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Runtime.InteropServices;

namespace VSDK;

internal sealed class LauncherPaths
{
    public LauncherPaths(string executableDirectory, string? sdk = null, string? vertex = null)
    {
        var current = new DirectoryInfo(executableDirectory);
        for (var i = 0; sdk == null && i < 8 && current != null; i++, current = current.Parent)
        {
            sdk = FindManifest(current.FullName);
        }
        if (sdk != null && Directory.Exists(sdk))
            sdk = FindManifest(sdk) ?? throw new FileNotFoundException("No SDK manifest found in " + sdk);
        Manifest = Path.GetFullPath(sdk ?? Path.Combine(executableDirectory, "Game.vertexsdk"));
        InstallRoot = Path.GetDirectoryName(Manifest)!;
        var platform = OperatingSystem.IsMacOS() ? "osx" : OperatingSystem.IsWindows() ? "win" : "linux";
        var architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        var editor = Path.TrimEndingDirectorySeparator(Path.GetFullPath(vertex ?? Path.Combine(InstallRoot, "Editor", platform + "-" + architecture,
            OperatingSystem.IsMacOS() ? "Vertex.app" : OperatingSystem.IsWindows() ? "Vertex.exe" : "Vertex")));
        VertexApplication = OperatingSystem.IsMacOS() && editor.EndsWith(".app", StringComparison.OrdinalIgnoreCase) ? editor : null;
        VertexExecutable = VertexApplication == null ? editor : Path.Combine(editor, "Contents", "MacOS", "Vertex");
    }
    private static string? FindManifest(string directory)
    {
        var manifests = Directory.GetFiles(directory, "*.vertexsdk").ToList();
        var workspace = Path.Combine(directory, "Vertex");
        if (Directory.Exists(workspace)) manifests.AddRange(Directory.GetFiles(workspace, "*.vertexsdk"));
        if (manifests.Count > 1)
            throw new InvalidDataException("Multiple SDK manifests found; select one with --sdk: " + directory);
        return manifests.SingleOrDefault();
    }

    public string Manifest { get; }
    public string InstallRoot { get; }
    public string VertexExecutable { get; }
    public string? VertexApplication { get; }
}
