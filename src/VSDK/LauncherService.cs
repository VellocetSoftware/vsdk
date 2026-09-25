// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Diagnostics;
using System.Text.Json;

namespace VSDK;

internal sealed class LauncherService(LauncherPaths paths)
{
    internal const string DocumentationUrl = "https://developer.vellocetsoftware.com/wiki/Vellocet_SDK";
    public LauncherPaths Paths { get; } = paths;

    public string Inspect()
    {
        if (!File.Exists(Paths.Manifest)) throw new FileNotFoundException("Reinstall the Grimwar SDK: its Vertex connection is missing.", Paths.Manifest);
        using var sdk = JsonDocument.Parse(File.ReadAllBytes(Paths.Manifest));
        var root = sdk.RootElement;
        if (root.GetProperty("formatVersion").GetInt32() != 1) throw new InvalidDataException("Update the Grimwar SDK launcher to read this SDK version.");
        var gamePath = Resolve(root.GetProperty("game").GetProperty("path").GetString());
        using var profile = JsonDocument.Parse(File.ReadAllBytes(gamePath));
        var game = profile.RootElement;
        if (root.GetProperty("game").GetProperty("id").GetString() != game.GetProperty("id").GetString() ||
            root.GetProperty("game").GetProperty("version").GetString() != game.GetProperty("version").GetString())
            throw new InvalidDataException("The installed game profile does not match the SDK. Reinstall the SDK.");
        var compiler = Resolve(root.GetProperty("compilerProject").GetString());
        var version = game.GetProperty("engineVersion").GetString();
        if (!Directory.Exists(compiler)) throw new InvalidDataException("The SDK compiler is missing. Reinstall the SDK.");
        if (!root.TryGetProperty("editorProject", out _) && !File.Exists(Resolve(root.GetProperty("compilerInputs").GetString())))
            throw new InvalidDataException("The SDK compiler inventory is missing. Reinstall the SDK.");
        if (!File.Exists(Paths.VertexExecutable)) throw new FileNotFoundException("Vertex is missing for this computer. Reinstall the matching Grimwar SDK distribution.", Paths.VertexExecutable);
        return $"{game.GetProperty("name").GetString()} SDK ready · {game.GetProperty("engineId").GetString()} {version}";
    }

    public void OpenVertex(string? project = null)
    {
        Inspect();
        var start = new ProcessStartInfo(Paths.VertexExecutable) { UseShellExecute = false, WorkingDirectory = Paths.InstallRoot };
        start.ArgumentList.Add("--sdk"); start.ArgumentList.Add(Paths.Manifest);
        if (project != null) { start.ArgumentList.Add("--project"); start.ArgumentList.Add(Path.GetFullPath(project)); }
        using var process = Process.Start(start) ?? throw new IOException("Vertex could not start.");
    }

    private string Resolve(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains('\\') ||
            relative.Split('/').Any(segment => segment is "" or "." or "..")) throw new InvalidDataException("The SDK contains an invalid installation path.");
        return Path.GetFullPath(Path.Combine(Paths.InstallRoot, relative));
    }
}
