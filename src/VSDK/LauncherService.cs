// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Diagnostics;
using System.Text.Json;

namespace VSDK;

internal sealed class LauncherService(LauncherPaths paths)
{
    internal const string DocumentationUrl = "https://developer.vellocetsoftware.com/wiki/Vellocet_SDK";
    public LauncherPaths Paths { get; } = paths;

    public async Task<SetupStatus> SetupStatusAsync(string? engine = null, CancellationToken cancellation = default)
    {
        Inspect();
        var output = await RunSetupAsync("--inspect-sdk", engine, null, null, cancellation);
        return JsonSerializer.Deserialize<SetupStatus>(output) ?? throw new InvalidDataException("Vertex returned no setup information.");
    }

    public Task ConfigureAsync(string? engine, string? game, Action<string> progress, CancellationToken cancellation) =>
        RunSetupAsync("--setup-sdk", engine, game, progress, cancellation);

    private async Task<string> RunSetupAsync(string action, string? engine, string? game, Action<string>? progress,
        CancellationToken cancellation)
    {
        var arguments = new List<string> { action, Paths.Manifest };
        if (!string.IsNullOrWhiteSpace(engine)) arguments.AddRange(["--engine", engine]);
        if (!string.IsNullOrWhiteSpace(game)) arguments.AddRange(["--game", game]);
        return await RunAsync(arguments, progress, cancellation);
    }

    public async Task<ResourceType[]> ResourceTypesAsync() =>
        JsonSerializer.Deserialize<ResourceType[]>(await RunAsync(["--sdk-resource", Paths.Manifest, "list"], null, default)) ?? [];

    public Task<string> ResourceAsync(string action, ResourceType type, string source, string? output,
        bool release, string? game, Action<string> progress, CancellationToken cancellation)
    {
        var arguments = new List<string> { "--sdk-resource", Paths.Manifest, action, type.Id, source };
        if (output != null) arguments.Add(output);
        if (release) arguments.Add("--release");
        if (action == "test" && !string.IsNullOrWhiteSpace(game)) arguments.AddRange(["--game", game]);
        return RunAsync(arguments, progress, cancellation);
    }

    private async Task<string> RunAsync(IEnumerable<string> arguments, Action<string>? progress, CancellationToken cancellation)
    {
        var start = new ProcessStartInfo(Paths.VertexExecutable)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Vertex could not start setup.");
        var errors = process.StandardError.ReadToEndAsync(cancellation);
        var output = new System.Text.StringBuilder();
        try
        {
            while (await process.StandardOutput.ReadLineAsync(cancellation) is { } line)
            {
                output.AppendLine(line);
                progress?.Invoke(line);
            }
            await process.WaitForExitAsync(cancellation);
            var error = await errors;
            if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Vertex setup failed." : error.Trim());
            return output.ToString();
        }
        finally
        {
            if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(CancellationToken.None); }
        }
    }

    public string Inspect()
    {
        if (!File.Exists(Paths.Manifest)) throw new FileNotFoundException("Reinstall the game SDK: its Vertex connection is missing.", Paths.Manifest);
        using var sdk = JsonDocument.Parse(File.ReadAllBytes(Paths.Manifest));
        var root = sdk.RootElement;
        if (root.GetProperty("formatVersion").GetInt32() != 1) throw new InvalidDataException("Update the game SDK launcher to read this SDK version.");
        var projectRoot = Paths.InstallRoot;
        if (root.TryGetProperty("projectRoot", out var configuredRoot))
        {
            var directory = new DirectoryInfo(projectRoot);
            if (configuredRoot.GetString() != ".." ||
                !root.TryGetProperty("editorProject", out var editor) || editor.ValueKind != JsonValueKind.String ||
                directory.Name != "Vertex" || directory.Parent == null ||
                (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Repository SDK connections must be developer installations inside Vertex.");
            projectRoot = directory.Parent.FullName;
        }
        var gamePath = Resolve(projectRoot, root.GetProperty("game").GetProperty("path").GetString());
        using var profile = JsonDocument.Parse(File.ReadAllBytes(gamePath));
        var game = profile.RootElement;
        if (root.GetProperty("game").GetProperty("id").GetString() != game.GetProperty("id").GetString() ||
            root.GetProperty("game").GetProperty("version").GetString() != game.GetProperty("version").GetString())
            throw new InvalidDataException("The installed game profile does not match the SDK. Reinstall the SDK.");
        var compiler = Resolve(projectRoot, root.GetProperty("compilerProject").GetString());
        var version = game.GetProperty("engineVersion").GetString();
        if (!Directory.Exists(compiler)) throw new InvalidDataException("The SDK compiler is missing. Reinstall the SDK.");
        if (!root.TryGetProperty("editorProject", out _) && !File.Exists(Resolve(projectRoot, root.GetProperty("compilerInputs").GetString())))
            throw new InvalidDataException("The SDK compiler inventory is missing. Reinstall the SDK.");
        if (!File.Exists(Paths.VertexExecutable)) throw new FileNotFoundException("Vertex is missing for this computer. Reinstall the matching game SDK distribution.", Paths.VertexExecutable);
        return $"{game.GetProperty("name").GetString()} SDK ready · {game.GetProperty("engineId").GetString()} {version}";
    }

    public void OpenVertex(string? map = null, string? game = null)
    {
        Inspect();
        var start = new ProcessStartInfo(Paths.VertexApplication == null ? Paths.VertexExecutable : "/usr/bin/open")
            { UseShellExecute = false, WorkingDirectory = Paths.InstallRoot };
        if (Paths.VertexApplication is { } application)
        {
            start.RedirectStandardError = true;
            start.ArgumentList.Add("-n"); start.ArgumentList.Add("-a"); start.ArgumentList.Add(application);
            start.ArgumentList.Add("--args");
        }
        start.ArgumentList.Add("--sdk"); start.ArgumentList.Add(Paths.Manifest);
        if (!string.IsNullOrWhiteSpace(game)) { start.ArgumentList.Add("--game"); start.ArgumentList.Add(game); }
        if (map != null) { start.ArgumentList.Add("--map"); start.ArgumentList.Add(Path.GetFullPath(map)); }
        using var process = Process.Start(start) ?? throw new IOException("Vertex could not start.");
        if (Paths.VertexApplication != null)
        {
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new IOException("macOS could not open Vertex. " + error.Trim());
        }
    }

    private static string Resolve(string directory, string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains('\\') ||
            relative.Split('/').Any(segment => segment is "" or "." or "..")) throw new InvalidDataException("The SDK contains an invalid installation path.");
        return Path.GetFullPath(Path.Combine(directory, relative));
    }
}

internal sealed record SetupStatus(string Name, string Engine, string Version, string EngineExecutable,
    string? GameExecutable, bool Developer, bool Ready, bool CanPlay, string? InstallationUrl,
    EngineRequirement[]? Requirements = null);

internal sealed record EngineRequirement(string Name, bool Installed, string Detail);

internal sealed record ResourceType(string Id, string Name, string Manifest)
{
    public override string ToString() => Name;
}
