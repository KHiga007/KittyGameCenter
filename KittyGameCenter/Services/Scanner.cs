using Microsoft.Win32;
using System.Text.Json;
using System.Text.RegularExpressions;

using KittyGameCenter.Models;

namespace KittyGameCenter.Services;
public class Scanner
{
    readonly List<Game> games = [];
    readonly List<string> notes = [];
    readonly HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> detectedFolders = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> knownGameFolders = new(StringComparer.OrdinalIgnoreCase);
    IProgress<ScanProgress>? progress;
    int completedRoutes, totalRoutes, scannedDirectories;
    long lastProgressTick;
    static string PF => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    static string PF86 => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
    static string PD => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    static string Val(string text, string key) => Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*\"([^\"]*)\"").Groups[1].Value.Replace("\\\\", "\\");
    void Guard(string label, Action action) { try { action(); } catch (Exception ex) { notes.Add(label + ": " + ex.Message); } }
    void Add(string name, string source, string folder, string launch, bool candidate = false)
    {
        folder = folder.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(name) || !Directory.Exists(folder)) return;
        if (name.Equals("Steamworks Common Redistributables", StringComparison.OrdinalIgnoreCase)) return;
        string norm = Path.GetFullPath(folder).TrimEnd('\\');
        knownGameFolders.Add(norm);
        if (source == "Steam") detectedFolders.Add(Path.GetDirectoryName(norm)!);
        else if (source != "Carpeta" && source != "Xbox / carpeta") detectedFolders.Add(norm);
        if (games.Any(g => string.Equals(g.Folder, norm, StringComparison.OrdinalIgnoreCase) || (launch.Length > 0 && g.Launch == launch))) return;
        games.Add(new(Guid.NewGuid().ToString("N"), name, source, norm, launch, candidate));
    }
    static IEnumerable<string> Files(string path, string pattern) { try { return Directory.GetFiles(path, pattern); } catch { return []; } }
    static IEnumerable<string> Dirs(string path) { try { return Directory.GetDirectories(path); } catch { return []; } }
    public static string FindExe(string folder)
    {
        var dirs = new List<string> { folder };
        dirs.AddRange(Dirs(folder).Where(d => !Regex.IsMatch(d, @"(?i)(redist|support|installer|directx|crash|anticheat)")));
        foreach (var d in dirs.ToArray().Skip(1)) dirs.AddRange(Dirs(d).Take(30));
        return dirs.SelectMany(d => Files(d, "*.exe"))
            .Where(p => !Regex.IsMatch(Path.GetFileName(p), @"(?i)(unins|setup|install|crash|report|redist|helper|service|anticheat|vc_redist|dxsetup|update)"))
            .OrderByDescending(p => { try { return new FileInfo(p).Length; } catch { return 0; } }).FirstOrDefault() ?? "";
    }
    public ScanResult Run(IEnumerable<string>? extra = null, bool automatic = true, IProgress<ScanProgress>? observer = null)
    {
        progress = observer;
        Report("discovery", "Buscando bibliotecas de launchers", force: true);
        if (automatic)
        {
        Guard("Steam", Steam);
        Guard("Epic", Epic);
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            Guard("Registro " + hive + " " + view, () => RegistryGames(hive, view));
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        foreach (var name in new[] { "Games", "Juegos", "SteamLibrary", "Epic Games", "GOG Games", "XboxGames", "Riot Games", "Ubisoft Games", "EA Games", "Battle.net" })
            roots.Add(Path.Combine(drive.RootDirectory.FullName, name));
        foreach (var p in new[] { PF, PF86 })
        foreach (var name in new[] { "Epic Games", "GOG Galaxy\\Games", "Ubisoft\\Ubisoft Game Launcher\\games", "EA Games", "Origin Games", "Riot Games", "Rockstar Games" }) roots.Add(Path.Combine(p, name));
        }
        var requested = (extra ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Select(CanonicalPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var path in requested) roots.Add(path);
        foreach (var folder in detectedFolders) roots.Add(folder);
        var candidates = roots.Select(CanonicalPath).Distinct(StringComparer.OrdinalIgnoreCase).Where(p => Directory.Exists(p) || requested.Contains(p, StringComparer.OrdinalIgnoreCase)).ToArray();
        var plan = candidates.Where(path => !candidates.Any(parent => !path.Equals(parent, StringComparison.OrdinalIgnoreCase) && IsInside(path, parent))).OrderBy(p => p).ToArray();
        totalRoutes = plan.Length;
        foreach (var root in plan)
        {
            Report("routes", root, force: true);
            var state = "complete";
            if (!Directory.Exists(root)) { notes.Add("Ruta no disponible: " + root); state = "unavailable"; }
            else
            {
                var previousNotes = notes.Count;
                Guard(root, () => ScanRoot(root));
                if (notes.Count > previousNotes) state = "partial";
            }
            completedRoutes++;
            progress?.Report(new("routes", root, completedRoutes, totalRoutes, games.Count, scannedDirectories, state, games.Count(g => IsInside(g.Folder, root))));
        }
        Report("done", "Análisis de rutas completado", force: true);
        notes.Add("Se revisaron " + plan.Length + " rutas principales y " + scannedDirectories + " carpetas. El recorrido omite enlaces, carpetas de sistema y auxiliares, y se limita a 8 niveles; los juegos dudosos se muestran como candidatos.");
        return new(games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList(), notes, plan.ToList());
    }
    static string CanonicalPath(string path)
    {
        var full = Path.GetFullPath(path).Replace('/', '\\').TrimEnd('\\');
        return full.EndsWith(':') ? full + "\\" : full;
    }
    static bool IsInside(string path, string root)
    {
        path = CanonicalPath(path); root = CanonicalPath(root);
        return path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
    }
    void Report(string phase, string path, bool force = false)
    {
        var now = Environment.TickCount64;
        if (!force && now - lastProgressTick < 100) return;
        lastProgressTick = now;
        progress?.Report(new(phase, path, completedRoutes, totalRoutes, games.Count, scannedDirectories));
    }

    void Steam()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(PF86, "Steam"), Path.Combine(PF, "Steam") };
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        foreach (var key in new[] { @"Software\Valve\Steam", @"Software\WOW6432Node\Valve\Steam" })
        { using var k = hive.OpenSubKey(key); var p = k?.GetValue("SteamPath") as string ?? k?.GetValue("InstallPath") as string; if (p != null) paths.Add(p); }
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady)) paths.Add(Path.Combine(drive.RootDirectory.FullName, "SteamLibrary"));
        foreach (var p in paths.ToArray())
        { var vdf = Path.Combine(p, "steamapps", "libraryfolders.vdf"); if (File.Exists(vdf)) foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\"")) paths.Add(m.Groups[1].Value.Replace("\\\\", "\\")); }
        foreach (var p in paths)
            if (Directory.Exists(Path.Combine(p, "steamapps"))) detectedFolders.Add(Path.Combine(p, "steamapps"));
        foreach (var p in paths)
        foreach (var file in Files(Path.Combine(p, "steamapps"), "appmanifest_*.acf")) Guard(file, () =>
        { var t = File.ReadAllText(file); var id = Val(t, "appid"); if (!Regex.IsMatch(id, @"^\d+$")) return; Add(Val(t, "name"), "Steam", Path.Combine(p, "steamapps", "common", Val(t, "installdir")), "steam://rungameid/" + id); });
    }
    void Epic()
    {
        foreach (var file in Files(Path.Combine(PD, "Epic", "EpicGamesLauncher", "Data", "Manifests"), "*.item")) Guard(file, () =>
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file)); var j = doc.RootElement;
            string Get(string k) => j.TryGetProperty(k, out var v) ? v.ToString() : "";
            if (j.TryGetProperty("bIsIncompleteInstall", out var incomplete) && incomplete.ValueKind == JsonValueKind.True) return;
            if (j.TryGetProperty("bIsApplication", out var application) && application.ValueKind == JsonValueKind.False) return;
            var app = Get("AppName");
            Add(Get("DisplayName"), "Epic Games", Get("InstallLocation"), string.IsNullOrEmpty(app) ? "" : "com.epicgames.launcher://apps/" + Uri.EscapeDataString(app) + "?action=launch&silent=true");
        });
    }
    void RegistryGames(RegistryHive hive, RegistryView view)
    {
        using var reg = RegistryKey.OpenBaseKey(hive, view);
        using (var gog = reg.OpenSubKey(@"SOFTWARE\GOG.com\Games"))
        if (gog != null) foreach (var id in gog.GetSubKeyNames()) Guard("GOG " + id, () =>
        { using var k = gog.OpenSubKey(id); if (k == null) return; var folder = k.GetValue("path") as string ?? ""; var exe = k.GetValue("exe") as string ?? ""; if (!Path.IsPathRooted(exe) && exe.Length > 0) exe = Path.Combine(folder, exe); Add(k.GetValue("gameName") as string ?? id, "GOG", folder, exe); });
        using (var ubi = reg.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher\Installs"))
        if (ubi != null) foreach (var id in ubi.GetSubKeyNames()) Guard("Ubisoft " + id, () =>
        { using var k = ubi.OpenSubKey(id); var folder = k?.GetValue("InstallDir") as string ?? ""; Add(Path.GetFileName(folder.TrimEnd('\\', '/')), "Ubisoft Connect", folder, "uplay://launch/" + id + "/0"); });
        using var uninstall = reg.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        if (uninstall == null) return;
        foreach (var id in uninstall.GetSubKeyNames()) Guard("Instalación " + id, () =>
        {
            using var k = uninstall.OpenSubKey(id); if (k == null) return;
            var name = k.GetValue("DisplayName") as string ?? ""; var folder = k.GetValue("InstallLocation") as string ?? "";
            if (!Directory.Exists(folder)) return;
            var pub = k.GetValue("Publisher") as string ?? "";
            var hint = name + " " + pub + " " + folder;
            if (!Regex.IsMatch(hint, @"(?i)(steam|gog|epic games|ubisoft|electronic arts|ea games|origin games|blizzard|battle.net|riot games|rockstar|bethesda|xboxgames|\\games\\|\\juegos\\)")) return;
            if (Regex.IsMatch(name, @"(?i)(launcher|redistributable|sdk|anti.cheat|webview|directx|GOG GALAXY|Ubisoft Connect|Epic Games Launcher|^Steam$|^EA app$|^Battle.net$)")) return;
            var source = Regex.IsMatch(hint, "(?i)blizzard|battle.net") ? "Battle.net" : Regex.IsMatch(hint, "(?i)electronic arts|ea games|origin games") ? "EA / Origin" : Regex.IsMatch(hint, "(?i)riot games") ? "Riot Games" : Regex.IsMatch(hint, "(?i)rockstar") ? "Rockstar" : "Registro";
            Add(name, source, folder, "", true);
        });
    }
    static bool SkipFolder(string path) => Regex.IsMatch(Path.GetFileName(path), @"(?i)^(windows|programdata|appdata|\$recycle\.bin|system volume information|node_modules|\.git|\.egstore|\.vs|redist|_commonredist|support|installer|installers|directx|anticheat|easyanticheat|battleye|shadercache|downloading|appcache|depotcache|workshop|gamesave|steamworks shared|launcher|epic online services|minecraft launcher)$");
    static bool IsGameExe(string path) => !Regex.IsMatch(Path.GetFileName(path), @"(?i)(unins|setup|install|crash|report|redist|helper|service|anticheat|vc_redist|dxsetup|update|^steam\.exe$|epicgameslauncher|galaxyclient|ubisoftconnect|eadesktop|riotclient|rockstarlauncher)");
    static bool IsGameAssetDirectory(string folder)
    {
        // Unity stores resources, profiles and saves here, never separate game installations.
        // Recognize the structure too: a folder merely ending in _Data is not enough.
        return Path.GetFileName(folder).EndsWith("_Data", StringComparison.OrdinalIgnoreCase)
            && (File.Exists(Path.Combine(folder, "globalgamemanagers"))
                || Directory.Exists(Path.Combine(folder, "Managed"))
                || Directory.Exists(Path.Combine(folder, "StreamingAssets")));
    }
    void ReadLibraryManifests(string folder)
    {
        var steamapps = Path.GetFileName(folder).Equals("steamapps", StringComparison.OrdinalIgnoreCase) ? folder : Path.Combine(folder, "steamapps");
        if (!Directory.Exists(steamapps)) return;
        foreach (var file in Files(steamapps, "appmanifest_*.acf")) Guard(file, () =>
        {
            var text = File.ReadAllText(file); var id = Val(text, "appid");
            if (Regex.IsMatch(id, @"^\d+$")) Add(Val(text, "name"), "Steam", Path.Combine(steamapps, "common", Val(text, "installdir")), "steam://rungameid/" + id);
        });
    }
    void ScanRoot(string root)
    {
        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((root, 0));
        while (pending.Count > 0)
        {
            var (folder, depth) = pending.Pop();
            if (knownGameFolders.Any(gameFolder => IsInside(folder, gameFolder)) || IsGameAssetDirectory(folder)) continue;
            if (SkipFolder(folder)) { if (depth == 0) notes.Add("Ruta de sistema o auxiliar omitida: " + folder); continue; }
            try
            {
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) { notes.Add("No se recorrió el enlace de carpeta: " + folder); continue; }
                scannedDirectories++;
                Report("routes", folder);
                ReadLibraryManifests(folder);
                // Manifest/registry entries already identify this game; do not turn its binaries into extra games.
                if (knownGameFolders.Any(gameFolder => IsInside(folder, gameFolder))) continue;
                var executables = Files(folder, "*.exe").Where(IsGameExe).ToArray();
                var executable = executables.FirstOrDefault();
                if (executable == null)
                {
                    foreach (var binaryFolder in new[] { "Binaries\\Win64", "Binaries\\WinGDK", "bin", "bin64", "Win64", "Content" })
                    {
                        var location = Path.Combine(folder, binaryFolder);
                        executable = Files(location, "*.exe").FirstOrDefault(IsGameExe);
                        if (executable != null) break;
                    }
                }
                if (executable != null)
                {
                    // Container folders can contain launcher helpers: keep descending when games already exist inside.
                    if (!games.Any(g => !g.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase) && IsInside(g.Folder, folder)))
                    {
                        Add(Path.GetFileName(folder.TrimEnd('\\')), root.Contains("Xbox", StringComparison.OrdinalIgnoreCase) ? "Xbox / carpeta" : "Carpeta", folder, "", true);
                        continue;
                    }
                }
                var children = Directory.GetDirectories(folder).Where(p => !SkipFolder(p)).ToArray();
                if (depth >= 8)
                {
                    if (children.Length > 0) notes.Add("Se alcanzó el límite de profundidad: " + folder);
                    continue;
                }
                foreach (var child in children.Reverse()) pending.Push((child, depth + 1));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                notes.Add("No se pudo revisar " + folder + ": " + ex.Message);
            }
        }
    }

}
