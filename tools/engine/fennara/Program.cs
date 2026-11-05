using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

try
{
    await new Provisioner(args).Run();
    return 0;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or HttpRequestException
    or JsonException or InvalidOperationException or ArgumentException or TaskCanceledException)
{
    Console.Error.WriteLine($"Fennara: {error.Message}");
    return 1;
}

sealed class Provisioner
{
    const string Releases = "https://api.github.com/repos/fennaraOfficial/fennara-godot-ai/releases";
    const string ManagedStart = "# BEGIN Ciallo Fennara";
    const string ManagedEnd = "# END Ciallo Fennara";
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    readonly string root, project, state, lockFile, app, platform, suffix, command, daemonUrl;
    readonly string? requested;
    readonly bool ci;
    readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(10) };
    readonly HttpClient local = new(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(3) };

    public Provisioner(string[] args)
    {
        Require(args.Length >= 3, "Expected checkout, platform and sync|status|update [version].");
        root = Path.GetFullPath(args[0]);
        project = Path.Combine(root, "Ciallo");
        state = Path.Combine(root, ".ciallo", "fennara");
        lockFile = Path.Combine(root, "tools", "engine", "dependencies.lock");
        platform = args[1] switch { "win-x64" => "windows-x86_64", "linux-x64" => "linux-x86_64",
            "osx-arm64" => "macos-arm64", _ => throw new ArgumentException("Unsupported Fennara platform.") };
        suffix = platform.StartsWith("windows") ? ".exe" : "";
        var options = args.Skip(3).Where(x => x != "--ci").ToArray();
        command = args[2];
        Require(command is "sync" or "status" or "update", "Use fennara sync|status|update [version].");
        Require(options.Length <= (command == "update" ? 1 : 0), "Only update accepts a version argument.");
        requested = options.FirstOrDefault()?.TrimStart('v');
        if (requested != null) RequireVersion(requested);
        ci = args.Contains("--ci");
        Require(!ci || command != "update", "CI restores the locked version; update it on a development machine.");
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        app = Environment.GetEnvironmentVariable("CIALLO_FENNARA_HOME") ?? (OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fennara")
            : OperatingSystem.IsMacOS() ? Path.Combine(user, "Library", "Application Support", "Fennara")
            : Path.Combine(Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(user, ".local", "share"), "fennara"));
        app = Path.GetFullPath(app);
        daemonUrl = Environment.GetEnvironmentVariable("CIALLO_FENNARA_DAEMON_URL") ?? "http://127.0.0.1:41287";
        Require(new Uri(daemonUrl).IsLoopback, "The Fennara control endpoint must be local.");
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Ciallo-dependency-setup/1.0");
    }

    public async Task Run()
    {
        Require(File.Exists(Path.Combine(project, "project.godot")), $"No Godot project at {project}.");
        Directory.CreateDirectory(state);
        var version = Git("--get", "fennara.version").Trim();
        if (command == "status") { Status(version); return; }
        using var checkoutLock = Exclusive(Path.Combine(state, "install.lock"));
        if (!ci) Directory.CreateDirectory(app);
        using var sharedLock = ci ? null : Exclusive(Path.Combine(app, ".ciallo-install.lock"));
        var journal = Path.Combine(ci ? state : app, ".ciallo-transaction.json");
        string[] scopes = [root, app];
        Transaction.CleanCompleted(Path.GetDirectoryName(journal)!, scopes);
        if (File.Exists(journal))
        {
            if (!ci && Transaction.NeedsRollback(journal)) await StopIdleDaemon();
            Transaction.Recover(journal, scopes);
        }
        var originalLock = File.ReadAllBytes(lockFile);
        var manifestUrl = Git("--get", "fennara.manifest-url").Trim();
        var manifestHash = Git("--get", "fennara.manifest-sha256").Trim();
        if (command == "update")
        {
            var api = Environment.GetEnvironmentVariable("CIALLO_FENNARA_RELEASES_URL") ?? Releases;
            var releaseUrl = api.TrimEnd('/') + (requested == null ? "/latest" : "/tags/v" + requested);
            ValidateUrl(releaseUrl);
            var release = JsonNode.Parse(await http.GetStringAsync(releaseUrl))!.AsObject();
            Require(release["draft"]!.GetValue<bool>() == false && release["prerelease"]!.GetValue<bool>() == false,
                "Only published stable releases are supported.");
            version = release["tag_name"]!.GetValue<string>().TrimStart('v');
            RequireVersion(version);
            Require(requested == null || version == requested, "Release tag does not match the requested version.");
            var asset = release["assets"]!.AsArray().Single(x => Text(x!, "name") == $"fennara-release-manifest-v{version}.json")!;
            manifestUrl = Text(asset, "browser_download_url");
            var digest = Text(asset, "digest");
            Require(digest.StartsWith("sha256:"), "Release manifest has no GitHub SHA-256 digest.");
            manifestHash = digest[7..];
        }
        RequireVersion(version);
        var manifestPath = await Download(manifestUrl, manifestHash);
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!;
        ValidateManifest(manifest, version);
        var stage = Path.Combine(state, "stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        using var transaction = new Transaction(journal, scopes);
        try
        {
            var addon = Path.Combine(stage, "addon");
            await UnpackAsset(manifest["assets"]!["addon"]!, manifestUrl, addon, true);
            var addonSource = Path.Combine(addon, "addons", "fennara");
            RequireFile(addonSource, "fennara.gdextension");
            RequireFile(addonSource, "runtime/game_capture_helper.gd");
            CheckVersion(addonSource, version);
            Require(Text(JsonNode.Parse(File.ReadAllText(Path.Combine(addonSource, "release.json")))!, "version") == version,
                "Addon release identity does not match its version.");
            RequireFile(addonSource, platform switch {
                "windows-x86_64" => "bin/libfennara.windows.editor.x86_64.dll",
                "linux-x86_64" => "bin/libfennara.linux.editor.x86_64.so",
                _ => "bin/libfennara.macos.editor.framework/libfennara.macos.editor" });
            transaction.Directory(addonSource, Path.Combine(project, "addons", "fennara"), preserveScriptUids: true);

            if (!ci)
            {
                var binaries = Path.Combine(stage, "local");
                await UnpackAsset(manifest["assets"]!["local"]![platform]!, manifestUrl, binaries);
                CheckVersion(binaries, version);
                var cli = Path.Combine(stage, "cli");
                await UnpackAsset(manifest["assets"]!["cli"]![platform]!, manifestUrl, cli);
                CheckVersion(cli, version);
                var versionDir = Path.Combine(app, "versions", version);
                foreach (var name in new[] { "fennara-mcp", "fennara-daemon", "fennara-mcp-runtime", "fennara-daemon-runtime" })
                {
                    var source = RequireFile(binaries, "bin/" + name + suffix);
                    transaction.File(source, Path.Combine(name.EndsWith("-runtime") ? versionDir : Path.Combine(app, "bin"), name + suffix));
                }
                transaction.File(RequireFile(cli, "bin/fennara" + suffix), Path.Combine(app, "bin", "fennara" + suffix));
                transaction.Directory(addonSource, Path.Combine(versionDir, "addon", "addons", "fennara"));
                await PrepareWebview(manifest, manifestUrl, stage, transaction);
                var current = new JsonObject {
                    ["version"] = version, ["release_track"] = "stable", ["release_tag"] = "v" + version,
                    ["release_channel"] = null, ["source_commit"] = null,
                    ["mcp_runtime"] = $"versions/{version}/fennara-mcp-runtime{suffix}",
                    ["daemon_runtime"] = $"versions/{version}/fennara-daemon-runtime{suffix}",
                    ["addon"] = $"versions/{version}/addon/addons/fennara"
                };
                transaction.Content(current.ToJsonString(JsonOptions) + "\n", Path.Combine(app, "current.json"));
            }

            // A changed install requires closed editors; an identical sync is safe while editing.
            if (!ci && transaction.HasChanges) await StopIdleDaemon();
            if (!ci) PrepareMcp(transaction);
            if (command == "update")
            {
                var nextLock = Path.Combine(stage, "dependencies.lock");
                File.WriteAllBytes(nextLock, originalLock);
                GitFile(nextLock, "fennara.version", version);
                GitFile(nextLock, "fennara.manifest-url", manifestUrl);
                GitFile(nextLock, "fennara.manifest-sha256", manifestHash);
                transaction.File(nextLock, lockFile);
            }
            Require(originalLock.AsSpan().SequenceEqual(File.ReadAllBytes(lockFile)), "Dependency lock changed during installation; retry.");
            transaction.Commit();
            Console.WriteLine($"Fennara ready: {version}{(ci ? " (CI addon only)" : "")}");
            if (command == "update") Console.WriteLine("Review dependencies.lock, then restart the editor and reconnect the MCP client.");
        }
        finally { Directory.Delete(stage, true); }
    }

    async Task PrepareWebview(JsonNode manifest, string manifestUrl, string stage, Transaction transaction)
    {
        foreach (var runtime in manifest["shared_runtimes"]!.AsArray().OfType<JsonObject>())
        {
            if (!runtime["enabled"]!.GetValue<bool>() || Text(runtime, "platform") != platform.Split('-')[0]) continue;
            Require(Text(runtime, "id") == "linux-cef" && Text(runtime, "kind") == "cef" && Text(runtime, "platform_arch") == "linux-x64",
                "Unsupported shared Fennara runtime; update the project installer.");
            var runtimeVersion = Text(runtime, "version");
            Require(Regex.IsMatch(runtimeVersion, "^[A-Za-z0-9.+-]+$"), "Invalid webview version.");
            var source = Path.Combine(stage, "cef");
            await UnpackAsset(runtime["archive"]!, manifestUrl, source);
            foreach (var file in runtime["required_files"]!.AsArray()) RequireFile(source, file!.GetValue<string>());
            File.WriteAllText(Path.Combine(source, "fennara-cef-runtime.json"), runtime.ToJsonString(JsonOptions) + "\n");
            var webview = Path.Combine(app, "webview", "cef", "linux-x64");
            transaction.Directory(source, Path.Combine(webview, runtimeVersion));
            transaction.Content(new JsonObject { ["runtime"] = "cef", ["platform"] = "linux", ["platform_arch"] = "linux-x64",
                ["version"] = runtimeVersion, ["dir"] = runtimeVersion }.ToJsonString(JsonOptions) + "\n", Path.Combine(webview, "current.json"));
        }
    }

    void PrepareMcp(Transaction transaction)
    {
        var path = Path.Combine(root, ".codex", "config.toml");
        var existing = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : "";
        var start = existing.IndexOf(ManagedStart, StringComparison.Ordinal);
        if (start >= 0)
        {
            var end = existing.IndexOf(ManagedEnd, start, StringComparison.Ordinal);
            Require(end >= 0, "Incomplete managed Fennara block in .codex/config.toml.");
            existing = existing.Remove(start, end + ManagedEnd.Length - start);
        }
        Require(!Regex.IsMatch(existing, @"(?m)^\s*\[mcp_servers\.(?:Fennara|fennara)(?:\.|\])"),
            "An unmanaged Fennara entry exists in .codex/config.toml; remove that entry before setup.");
        var block = $"{ManagedStart}\n[mcp_servers.Fennara]\ncommand = {Toml(Path.Combine(app, "bin", "fennara-mcp" + suffix))}\n" +
            $"args = [\"--project-path\", {Toml(project)}]\nstartup_timeout_sec = 30\ntool_timeout_sec = 300\n{ManagedEnd}\n";
        transaction.Content(existing.TrimEnd() + (existing.Trim().Length > 0 ? "\n\n" : "") + block, path);
    }

    async Task StopIdleDaemon()
    {
        try
        {
            using var health = await local.GetAsync(daemonUrl + "/health");
            health.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException error) when (error.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
        { return; }
        var token = System.IO.File.ReadAllText(Path.Combine(app, "daemon-control-token")).Trim();
        var key = DecodeBase64Url(token);
        Require(key.Length == 32, "Invalid Fennara daemon control token.");
        var nonce = RandomNumberGenerator.GetBytes(32);
        var challenge = JsonNode.Parse(await local.GetStringAsync(daemonUrl + "/control/challenge?nonce=" + Base64Url(nonce)))!;
        Require(CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(key, nonce), DecodeBase64Url(Text(challenge, "proof"))),
            "Fennara daemon identity verification failed.");
        using var statusRequest = new HttpRequestMessage(HttpMethod.Get, daemonUrl + "/status");
        statusRequest.Headers.Add("X-Fennara-Control-Token", token);
        using var statusResponse = await local.SendAsync(statusRequest);
        statusResponse.EnsureSuccessStatusCode();
        var status = JsonNode.Parse(await statusResponse.Content.ReadAsStringAsync())!;
        Require(status["connected_projects"]!.AsArray().Count == 0,
            "Close all Fennara-enabled Godot editors before changing the installation, then rerun this command.");
        using var stop = new HttpRequestMessage(HttpMethod.Post, daemonUrl + "/shutdown");
        stop.Headers.Add("X-Fennara-Control-Token", token);
        using var response = await local.SendAsync(stop);
        Require(response.IsSuccessStatusCode, "Fennara is in use and refused shutdown; stop runtime sessions and close editors first.");
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(250);
            try { using var check = await local.GetAsync(daemonUrl + "/health"); }
            catch (HttpRequestException error) when (error.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused }) { return; }
        }
        throw new IOException("Fennara daemon did not stop; installation was not changed.");
    }

    async Task<string> Download(string url, string hash)
    {
        ValidateUrl(url);
        Require(Regex.IsMatch(hash, "^[0-9a-f]{64}$"), "Invalid SHA-256 in dependency metadata.");
        var cache = Path.Combine(state, "downloads");
        Directory.CreateDirectory(cache);
        var target = Path.Combine(cache, hash);
        if (System.IO.File.Exists(target) && Hash(target) == hash) return target;
        var temp = target + ".download";
        Console.WriteLine($"Downloading {new Uri(url).Segments.Last()}");
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            ValidateUrl(response.RequestMessage!.RequestUri!.AbsoluteUri);
            await using (var output = System.IO.File.Create(temp)) await response.Content.CopyToAsync(output);
            Require(Hash(temp) == hash, $"SHA-256 mismatch: {url}");
            System.IO.File.Move(temp, target, true);
            return target;
        }
        finally { System.IO.File.Delete(temp); }
    }

    async Task UnpackAsset(JsonNode asset, string manifestUrl, string target, bool addon = false)
    {
        var name = Text(asset, "name");
        Require(Regex.IsMatch(name, "^[A-Za-z0-9_.+-]+\\.zip$"), "Unsupported release asset name.");
        var archive = await Download(new Uri(new Uri(manifestUrl), Uri.EscapeDataString(name)).AbsoluteUri, Text(asset, "sha256"));
        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries)
        {
            var path = entry.FullName;
            Require(!path.Contains('\\') && !path.Contains(':') && !path.StartsWith('/') &&
                !path.Split('/').Any(x => x is ".." or "."), $"Invalid archive path: {path}");
            Require(((entry.ExternalAttributes >> 16) & 0xF000) != 0xA000, $"Archive links are unsupported: {path}");
            if (addon)
            {
                if (path is "addons/" or "addons/fennara/") continue;
                Require(path.StartsWith("addons/fennara/"), $"Unexpected addon path: {path}");
                if (path.StartsWith("addons/fennara/ai/")) continue;
            }
            Require(!new[] { "AGENTS.md", "AGENT.md", "CLAUDE.md", "SKILL.md" }.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase),
                $"Unexpected agent instructions in release payload: {path}");
            var destination = Path.Combine(target, path);
            if (path.EndsWith('/')) { Directory.CreateDirectory(destination); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination);
            if (!OperatingSystem.IsWindows())
            {
                var mode = (UnixFileMode)((entry.ExternalAttributes >> 16) & 0x1FF);
                if (mode != 0) System.IO.File.SetUnixFileMode(destination, mode);
                if (path.StartsWith("bin/") || path.EndsWith("fennara_cef_helper"))
                    System.IO.File.SetUnixFileMode(destination, System.IO.File.GetUnixFileMode(destination) | UnixFileMode.UserExecute);
            }
        }
    }

    static void ValidateManifest(JsonNode manifest, string version)
    {
        Require(manifest["schema_version"]!.GetValue<int>() == 1 && Text(manifest, "version") == version,
            "Unsupported or mismatched Fennara manifest.");
        var release = manifest["release"]!;
        Require(Text(release, "track") == "stable" && Text(release, "version") == version && Text(release, "release_tag") == "v" + version,
            "Release identity does not match the selected stable version.");
        foreach (var primitive in manifest["install_primitives"]!.AsArray())
            Require(new[] { "local-zip-v1", "addon-zip-v1", "shared-webview-cef-v1" }.Contains(primitive!.GetValue<string>()),
                "This release needs a newer project installer.");
        foreach (var platform in new[] { "windows-x86_64", "linux-x86_64", "macos-arm64" })
            foreach (var kind in new[] { "cli", "local" })
            {
                var asset = manifest["assets"]![kind]![platform];
                Require(asset != null && Regex.IsMatch(Text(asset, "sha256"), "^[0-9a-f]{64}$"), $"Missing {kind} asset for {platform}.");
            }
    }

    void Status(string version)
    {
        var installed = Path.Combine(project, "addons", "fennara", "VERSION");
        var currentPath = Path.Combine(app, "current.json");
        var addonVersion = System.IO.File.Exists(installed) ? System.IO.File.ReadAllText(installed).Trim() : "missing";
        var runtimeVersion = System.IO.File.Exists(currentPath) ? Text(JsonNode.Parse(System.IO.File.ReadAllText(currentPath))!, "version") : "missing";
        Console.WriteLine($"Locked: {version}\nAddon: {addonVersion}\nShared runtime: {runtimeVersion}\nShared directory: {app}");
        Console.WriteLine($"MCP config: {Path.Combine(root, ".codex", "config.toml")}\nProject binding: {project}");
        Require(addonVersion == version && (ci || runtimeVersion == version), "Installation differs from dependencies.lock; run ./engine.sh fennara sync.");
        RequireFile(Path.Combine(project, "addons", "fennara"), "runtime/game_capture_helper.gd");
        if (!ci)
        {
            foreach (var name in new[] { "fennara", "fennara-mcp", "fennara-daemon" }) RequireFile(app, "bin/" + name + suffix);
            foreach (var name in new[] { "fennara-mcp-runtime", "fennara-daemon-runtime" }) RequireFile(app, $"versions/{version}/{name}{suffix}");
            RequireFile(root, ".codex/config.toml");
        }
        Require(!Directory.Exists(Path.Combine(project, "addons", "fennara", "ai")), "Upstream guidance is present; run fennara sync.");
    }

    string Git(params string[] args) => GitFile(lockFile, args);
    static string GitFile(string file, params string[] args)
    {
        var info = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var value in new[] { "config", "--file", file }.Concat(args)) info.ArgumentList.Add(value);
        using var process = Process.Start(info)!;
        var result = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Require(process.ExitCode == 0, $"Could not read/write dependency lock: {error}");
        return result;
    }
    static FileStream Exclusive(string path)
    {
        try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException error) { throw new IOException($"Another setup is using {path}; retry after it finishes.", error); }
    }
    static string RequireFile(string folder, string name)
    {
        Require(!Path.IsPathRooted(name) && !name.Split('/', '\\').Contains(".."), "Invalid payload path.");
        var path = Path.Combine(folder, name);
        Require(System.IO.File.Exists(path), $"Missing release file: {path}");
        return path;
    }
    static void CheckVersion(string folder, string version) => Require(System.IO.File.ReadAllText(RequireFile(folder, "VERSION")).Trim() == version, "Payload version mismatch.");
    static void RequireVersion(string value) => Require(Regex.IsMatch(value, "^[0-9]+\\.[0-9]+\\.[0-9]+$"), "Use an exact stable version, e.g. 0.4.3.");
    static void ValidateUrl(string value)
    {
        var uri = new Uri(value);
        Require(uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback), "Downloads require HTTPS (HTTP only for loopback integration fixtures).");
    }
    static string Toml(string value) => JsonSerializer.Serialize(value.Replace('\\', '/'), new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    static byte[] DecodeBase64Url(string text) => Convert.FromBase64String(text.Replace('-', '+').Replace('_', '/').PadRight((text.Length + 3) / 4 * 4, '='));
    static string Text(JsonNode value, string key) => value[key]?.GetValue<string>() ?? throw new InvalidOperationException($"Missing manifest field: {key}");
    internal static string Hash(string file) { using var stream = System.IO.File.OpenRead(file); return Convert.ToHexStringLower(SHA256.HashData(stream)); }
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

// Keep a journal before the first rename so interrupted installs can restore the previous files.
sealed class Transaction(string journal, string[] scopes) : IDisposable
{
    public sealed record Entry(string Target, string Next, string Backup, bool IsDirectory, bool HadOriginal);
    public sealed record Journal(bool Committed, List<Entry> Entries);
    readonly List<Entry> entries = [];
    bool finished;
    public bool HasChanges => entries.Count > 0;
    public void File(string source, string target) => Stage(source, target, false);
    public void Directory(string source, string target, bool preserveScriptUids = false) => Stage(source, target, true, preserveScriptUids);
    public void Content(string text, string target)
    {
        var temporary = Path.GetTempFileName();
        try { System.IO.File.WriteAllText(temporary, text, new UTF8Encoding(false)); File(temporary, target); }
        finally { System.IO.File.Delete(temporary); }
    }
    void Stage(string source, string target, bool directory, bool preserveScriptUids = false)
    {
        if (Same(source, target, directory, preserveScriptUids)) return;
        Console.WriteLine($"Preparing replacement: {target}");
        var id = Guid.NewGuid().ToString("N");
        var hidden = Path.Combine(Path.GetDirectoryName(target)!, "." + Path.GetFileName(target));
        var entry = new Entry(target, hidden + ".ciallo-next-" + id, hidden + ".ciallo-previous-" + id,
            directory, directory ? System.IO.Directory.Exists(target) : System.IO.File.Exists(target));
        Validate(entry, scopes);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        entries.Add(entry);
        if (directory)
        {
            CopyTree(source, entry.Next);
            if (preserveScriptUids && System.IO.Directory.Exists(target))
                foreach (var uid in System.IO.Directory.GetFiles(target, "*.gd.uid", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(target, uid);
                    if (GeneratedUid(source, relative)) System.IO.File.Copy(uid, Path.Combine(entry.Next, relative));
                }
        }
        else System.IO.File.Copy(source, entry.Next);
    }
    public void Commit()
    {
        if (entries.Count == 0) { finished = true; return; }
        Save(new Journal(false, entries));
        try
        {
            foreach (var entry in entries)
            {
                if (entry.HadOriginal) Move(entry.Target, entry.Backup, entry.IsDirectory);
                Move(entry.Next, entry.Target, entry.IsDirectory);
            }
            Save(new Journal(true, entries));
        }
        catch { Recover(journal, scopes); finished = true; throw; }
        finished = true;
        Recover(journal, scopes);
    }
    void Save(Journal value)
    {
        System.IO.File.WriteAllText(journal + ".next", JsonSerializer.Serialize(value));
        System.IO.File.Move(journal + ".next", journal, true);
    }
    public static bool NeedsRollback(string path) => !JsonSerializer.Deserialize<Journal>(System.IO.File.ReadAllText(path))!.Committed;
    public static void Recover(string path, string[] scopes)
    {
        var data = JsonSerializer.Deserialize<Journal>(System.IO.File.ReadAllText(path))!;
        foreach (var entry in data.Entries) Validate(entry, scopes);
        if (data.Committed)
        {
            var completed = Path.Combine(Path.GetDirectoryName(path)!, ".ciallo-completed-" + Guid.NewGuid().ToString("N") + ".json");
            System.IO.File.Move(path, completed);
            CleanCompleted(Path.GetDirectoryName(path)!, scopes);
            return;
        }
        foreach (var entry in data.Entries.AsEnumerable().Reverse())
        {
            if (Exists(entry.Backup, entry.IsDirectory))
            {
                Delete(entry.Target, entry.IsDirectory);
                Move(entry.Backup, entry.Target, entry.IsDirectory);
            }
            else if (!entry.HadOriginal) Delete(entry.Target, entry.IsDirectory);
            Delete(entry.Next, entry.IsDirectory);
            Delete(entry.Backup, entry.IsDirectory);
        }
        System.IO.File.Delete(path);
    }
    public static void CleanCompleted(string folder, string[] scopes)
    {
        foreach (var path in System.IO.Directory.GetFiles(folder, ".ciallo-completed-*.json"))
        {
            var data = JsonSerializer.Deserialize<Journal>(System.IO.File.ReadAllText(path))!;
            Provisioner.Require(data.Committed, "Unexpected unfinished cleanup journal.");
            // A different checkout may have retained a loaded executable; let that checkout clean its files.
            if (data.Entries.Any(entry => !InScope(entry.Target, scopes))) continue;
            try
            {
                foreach (var entry in data.Entries)
                {
                    Validate(entry, scopes);
                    Delete(entry.Next, entry.IsDirectory);
                    Delete(entry.Backup, entry.IsDirectory);
                }
                System.IO.File.Delete(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine("Previous Fennara files are still in use; reconnect the MCP client. A later sync will finish cleanup.");
            }
        }
    }
    static void Validate(Entry entry, string[] scopes)
    {
        var target = Path.GetFullPath(entry.Target);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var hidden = Path.Combine(Path.GetDirectoryName(target)!, "." + Path.GetFileName(target));
        bool Sibling(string path, string kind)
        {
            path = Path.GetFullPath(path);
            return string.Equals(Path.GetDirectoryName(path), Path.GetDirectoryName(target), comparison) &&
                (path.StartsWith(hidden + kind, comparison) || path.StartsWith(target + kind, comparison));
        }
        Provisioner.Require(InScope(target, scopes) && Sibling(entry.Next, ".ciallo-next-") && Sibling(entry.Backup, ".ciallo-previous-"),
            "Installation journal points outside this checkout or the Fennara user directory; recover from its original checkout.");
    }
    static bool InScope(string path, string[] scopes) => scopes.Any(scope => Path.GetFullPath(path).StartsWith(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(scope)) + Path.DirectorySeparatorChar,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
    public void Dispose()
    {
        if (!finished && !System.IO.File.Exists(journal))
            foreach (var entry in entries) Delete(entry.Next, entry.IsDirectory);
    }
    static bool Same(string source, string target, bool directory, bool preserveScriptUids = false)
    {
        if (!Exists(target, directory)) return false;
        if (!directory) return new FileInfo(source).Length == new FileInfo(target).Length && Provisioner.Hash(source) == Provisioner.Hash(target);
        var left = System.IO.Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        var right = System.IO.Directory.GetFiles(target, "*", SearchOption.AllDirectories)
            .Where(file => !preserveScriptUids || (!GeneratedUid(source, Path.GetRelativePath(target, file)) &&
                !Path.GetFileName(file).StartsWith('~'))).ToArray();
        var directories = System.IO.Directory.GetDirectories(source, "*", SearchOption.AllDirectories).Select(x => Path.GetRelativePath(source, x)).ToHashSet();
        return directories.SetEquals(System.IO.Directory.GetDirectories(target, "*", SearchOption.AllDirectories).Select(x => Path.GetRelativePath(target, x))) &&
            left.Length == right.Length && left.All(file => Same(file, Path.Combine(target, Path.GetRelativePath(source, file)), false));
    }
    static bool GeneratedUid(string source, string relative) => relative.EndsWith(".gd.uid", StringComparison.Ordinal) &&
        !System.IO.File.Exists(Path.Combine(source, relative)) && System.IO.File.Exists(Path.Combine(source, relative[..^4]));
    static void CopyTree(string source, string target)
    {
        System.IO.Directory.CreateDirectory(target);
        foreach (var file in System.IO.Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            System.IO.File.Copy(file, destination);
        }
    }
    static bool Exists(string path, bool directory) => directory ? System.IO.Directory.Exists(path) : System.IO.File.Exists(path);
    static void Move(string source, string target, bool directory) { if (directory) System.IO.Directory.Move(source, target); else System.IO.File.Move(source, target); }
    static void Delete(string path, bool directory)
    {
        if (!Exists(path, directory)) return;
        if (directory) System.IO.Directory.Delete(path, true); else System.IO.File.Delete(path);
    }
}
