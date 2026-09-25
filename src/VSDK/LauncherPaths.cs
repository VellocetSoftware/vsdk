// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Runtime.InteropServices;

namespace VSDK;

internal sealed class LauncherPaths
{
    public LauncherPaths(string executableDirectory, string? sdk = null, string? vertex = null)
    {
        var current = new DirectoryInfo(executableDirectory);
        for (var i = 0; i < 8 && current?.Parent != null && !File.Exists(Path.Combine(current.FullName, "Grimwar.vertexsdk")); i++) current = current.Parent;
        Manifest = Path.GetFullPath(sdk ?? Path.Combine(current?.FullName ?? executableDirectory, "Grimwar.vertexsdk"));
        InstallRoot = Path.GetDirectoryName(Manifest)!;
        var platform = OperatingSystem.IsMacOS() ? "osx" : OperatingSystem.IsWindows() ? "win" : "linux";
        var architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        VertexExecutable = Path.GetFullPath(vertex ?? Path.Combine(InstallRoot, "Editor", platform + "-" + architecture,
            OperatingSystem.IsWindows() ? "Vertex.exe" : "Vertex"));
    }
    public string Manifest { get; }
    public string InstallRoot { get; }
    public string VertexExecutable { get; }
}
