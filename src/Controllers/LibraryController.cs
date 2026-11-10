using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using KittyGameCenter.Models;
using KittyGameCenter.Services;

namespace KittyGameCenter.Controllers;

sealed class LibraryController
{
    readonly WebView2 web;
    readonly Form owner;
    readonly string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AkiNekoApps", "KittyGameCenter");
    List<Game> games = []; List<Game> manual = []; List<string> extra = []; HashSet<string> favorites = [];
    bool busy;
    bool automatic = true;
    List<Game> hiddenGames = [];
    Dictionary<string, string> gameNames = new();
    Dictionary<string, GamePaths> gamePaths = new();
    List<string> scanNotes = [];
    List<string> detectedFolders = [];
    readonly JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public LibraryController(Form owner, WebView2 web)
    {
        this.owner = owner;
        this.web = web;
    }
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(data);
        await Task.Run(() => DataMigration.CopyExistingData(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KittyGameCenter"), data));
        Directory.CreateDirectory(Path.Combine(data, "Previews"));
        try { var s = JsonSerializer.Deserialize<State>(File.ReadAllText(Path.Combine(data, "settings.json"))); if (s != null) { manual = s.Manual; extra = s.Roots; favorites = s.Favorites; automatic = s.Automatic; hiddenGames = s.HiddenGames ?? []; gameNames = s.GameNames ?? new(); gamePaths = s.GamePaths ?? new(); } } catch { }
        web.CreationProperties = new() { UserDataFolder = Path.Combine(data, "WebView") };
        await web.EnsureCoreWebView2Async();
        web.CoreWebView2.SetVirtualHostNameToFolderMapping("kitty.local", Path.Combine(AppContext.BaseDirectory, "www"), CoreWebView2HostResourceAccessKind.DenyCors);
        web.CoreWebView2.SetVirtualHostNameToFolderMapping("kitty-images.local", Path.Combine(data, "Previews"), CoreWebView2HostResourceAccessKind.DenyCors);
        web.CoreWebView2.Settings.AreDevToolsEnabled = false;
        web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        web.CoreWebView2.NavigationStarting += (_, e) => { if (!e.Uri.StartsWith("https://kitty.local/", StringComparison.OrdinalIgnoreCase)) e.Cancel = true; };
        web.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
        web.CoreWebView2.WebMessageReceived += async (_, e) =>
        {
            if (!e.Source.StartsWith("https://kitty.local/", StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                using var doc = JsonDocument.Parse(e.WebMessageAsJson); var m = doc.RootElement; var action = m.GetProperty("action").GetString();
                var id = m.TryGetProperty("id", out var value) ? value.GetString() : "";
                var game = games.FirstOrDefault(g => g.Id == id);
                switch (action)
                {
                    case "ready": await LoadLibrary(); break;
                    case "scan": await Scan(); break;
                    case "get-routes": SendRoutes(); break;
                    case "save-game": case "hide-game": case "restore-game":
                        try
                        {
                            var previousHidden = hiddenGames.ToList(); var previousNames = new Dictionary<string, string>(gameNames); var previousPaths = new Dictionary<string, GamePaths>(gamePaths);
                            try
                            {
                                if (action == "restore-game")
                                {
                                    var key = m.GetProperty("key").GetString();
                                    hiddenGames.RemoveAll(g => GameKey(g) == key);
                                }
                                else
                                {
                                    if (game == null) throw new IOException("El juego ya no está disponible. Volvé a abrir su configuración.");
                                    var key = GameKey(game);
                                    if (action == "hide-game")
                                    {
                                        var confirmation = m.TryGetProperty("confirmation", out var confirmValue) ? confirmValue.GetString() : null;
                                        if (!string.Equals(confirmation, DisplayName(game), StringComparison.Ordinal)) throw new IOException("Para eliminarlo, escribí exactamente el nombre del juego.");
                                        hiddenGames.RemoveAll(g => GameKey(g) == key); hiddenGames.Add(game);
                                    }
                                    else
                                    {
                                        var name = m.GetProperty("name").GetString()?.Trim() ?? "";
                                        if (name.Length is < 1 or > 160) throw new IOException("Ingresá un nombre de hasta 160 caracteres.");
                                        var effective = EffectiveGame(game);
                                        var folder = m.TryGetProperty("folder", out var folderValue) ? folderValue.GetString()?.Trim().Trim('"') ?? "" : effective.Folder;
                                        var launch = m.TryGetProperty("launch", out var launchValue) ? launchValue.GetString()?.Trim().Trim('"') ?? "" : effective.Launch;
                                        folder = Environment.ExpandEnvironmentVariables(folder);
                                        if (!Path.IsPathFullyQualified(folder) || !Directory.Exists(folder)) throw new IOException("La carpeta de instalación debe ser una ruta completa y existente.");
                                        folder = Path.GetFullPath(folder);
                                        if (launch.Length > 0)
                                        {
                                            launch = Environment.ExpandEnvironmentVariables(launch);
                                            if (Path.IsPathFullyQualified(launch))
                                            {
                                                if (!File.Exists(launch) || !Path.GetExtension(launch).Equals(".exe", StringComparison.OrdinalIgnoreCase)) throw new IOException("La ruta de inicio debe apuntar a un archivo .exe existente.");
                                                launch = Path.GetFullPath(launch);
                                            }
                                            else if (!(System.Text.RegularExpressions.Regex.IsMatch(launch, @"^steam://rungameid/\d+$") || launch.StartsWith("com.epicgames.launcher://apps/", StringComparison.Ordinal) || System.Text.RegularExpressions.Regex.IsMatch(launch, @"^uplay://launch/\d+/\d+$"))) throw new IOException("Usá la ruta de un .exe o un enlace de inicio válido de Steam, Epic o Ubisoft.");
                                        }
                                        gameNames[key] = name;
                                        gamePaths[key] = new(folder, launch);
                                    }
                                }
                                Save();
                            }
                            catch { hiddenGames = previousHidden; gameNames = previousNames; gamePaths = previousPaths; throw; }
                            Send(new { type = "game-saved", action }); SendLibrary(busy);
                            if (action == "restore-game") await Scan();
                        }
                        catch (Exception ex) { Send(new { type = "game-error", message = ex.Message }); }
                        break;
                    case "remove-root": case "set-discovery":
                        try
                        {
                            if (busy) throw new IOException("Esperá a que termine la búsqueda antes de cambiar las rutas.");
                            var previousRoots = extra.ToList(); var previousAutomatic = automatic;
                            try
                            {
                                if (action == "remove-root")
                                {
                                    var path = m.GetProperty("path").GetString();
                                    extra.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
                                }
                                else automatic = m.GetProperty("enabled").GetBoolean();
                                Save();
                            }
                            catch { extra = previousRoots; automatic = previousAutomatic; throw; }
                            SendRoutes();
                        }
                        catch (Exception ex) { SendRoutes(); Send(new { type = "routes-error", message = ex.Message }); break; }
                        await Scan(); break;
                    case "folder" when game != null:
                        var gameFolder = EffectiveGame(game).Folder;
                        if (!Directory.Exists(gameFolder)) throw new IOException("La carpeta ya no existe.");
                        Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { gameFolder }, UseShellExecute = true }); break;
                    case "play" when game != null:
                        try { Play(EffectiveGame(game)); }
                        catch (Exception ex) { Send(new { type = "launch", id = game.Id, state = "error", message = ex.Message }); }
                        break;
                    case "favorite" when game != null: if (!favorites.Add(game.Folder)) favorites.Remove(game.Folder); Save(); Send(new { type = "favorites", favorites }); break;
                    case "browse-game": BrowseEntry("game"); break;
                    case "browse-root": BrowseEntry("root"); break;
                    case "save-entry":
                        try { SaveEntry(m); }
                        catch (Exception ex) { Send(new { type = "entry-error", message = ex.Message }); break; }
                        await Scan(); break;
                    case "export":
                        using (var picker = new SaveFileDialog { Filter = "Biblioteca JSON|*.json", FileName = "KittyGameCenter-biblioteca.json" })
                        if (picker.ShowDialog(owner) == DialogResult.OK) File.WriteAllText(picker.FileName, JsonSerializer.Serialize(games, new JsonSerializerOptions { WriteIndented = true })); break;
                }
            } catch (Exception ex) { Send(new { type = "error", message = ex.Message }); }
        };
        web.Source = new Uri("https://kitty.local/index.html");
    }
    void BrowseEntry(string kind)
    {
        try
        {
            string? selected = null;
            if (kind == "game")
            {
                using var picker = new OpenFileDialog { Filter = "Ejecutable del juego (*.exe)|*.exe", Title = "Seleccionar el ejecutable del juego" };
                if (picker.ShowDialog(owner) == DialogResult.OK) selected = picker.FileName;
            }
            else
            {
                using var picker = new FolderBrowserDialog { Description = "Carpeta que contiene tus juegos", UseDescriptionForTitle = true };
                if (picker.ShowDialog(owner) == DialogResult.OK) selected = picker.SelectedPath;
            }
            Send(new { type = "picked-path", kind, path = selected, name = selected == null ? "" : Path.GetFileNameWithoutExtension(selected) });
        }
        catch (Exception ex) { Send(new { type = "picked-path", kind, error = ex.Message }); }
    }
    void SaveEntry(JsonElement message)
    {
        if (busy) throw new IOException("La búsqueda de juegos sigue en curso. Esperá a que termine y volvé a añadirlo.");
        var kind = message.GetProperty("kind").GetString();
        var path = Environment.ExpandEnvironmentVariables(message.GetProperty("path").GetString()?.Trim().Trim('"') ?? "");
        if (!Path.IsPathFullyQualified(path)) throw new IOException("Indicá una ruta completa o elegila con Examinar.");
        path = Path.GetFullPath(path);
        var previousManual = manual.ToList(); var previousRoots = extra.ToList();
        try
        {
            if (kind == "game")
            {
                var name = message.GetProperty("name").GetString()?.Trim() ?? "";
                if (name.Length is < 1 or > 160) throw new IOException("Ingresá un nombre de juego de hasta 160 caracteres.");
                if (!File.Exists(path) || !Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase)) throw new IOException("Elegí un archivo .exe existente para el juego.");
                var folder = Path.GetDirectoryName(path)!;
                manual.RemoveAll(g => string.Equals(g.Folder, folder, StringComparison.OrdinalIgnoreCase));
                manual.Add(new(Guid.NewGuid().ToString("N"), name, "Manual", folder, path));
            }
            else if (kind == "root")
            {
                if (!Directory.Exists(path)) throw new IOException("La carpeta indicada no existe o no está disponible.");
                var original = message.TryGetProperty("originalPath", out var oldPath) ? oldPath.GetString() : null;
                var index = original == null ? -1 : extra.FindIndex(p => string.Equals(p, original, StringComparison.OrdinalIgnoreCase));
                if (original != null && index < 0) throw new IOException("La ruta que querés editar ya no está en la lista.");
                if (extra.Where((_, i) => i != index).Any(p => string.Equals(p.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))) throw new IOException("Esta carpeta ya está añadida.");
                if (index >= 0) extra[index] = path; else extra.Add(path);
            }
            else throw new IOException("Tipo de entrada no válido.");
            Save();
        }
        catch { manual = previousManual; extra = previousRoots; throw; }
        SendRoutes();
        Send(new { type = "entry-saved", message = kind == "game" ? "Juego añadido. Actualizando la biblioteca…" : "Rutas guardadas. Buscando juegos…" });
    }
    void Play(Game game)
    {
        var launch = game.Launch;
        if (launch.Length == 0 || game.Candidate)
        {
            using var picker = new OpenFileDialog { InitialDirectory = game.Folder, Filter = "Ejecutable del juego (*.exe)|*.exe", Title = "Confirmar qué ejecutable abre " + game.Name };
            if (picker.ShowDialog(owner) != DialogResult.OK) { Send(new { type = "launch", id = game.Id, state = "cancelled", message = "No se seleccionó un ejecutable. Podés intentarlo cuando quieras." }); return; }
            launch = picker.FileName;
            manual.RemoveAll(g => string.Equals(g.Folder, game.Folder, StringComparison.OrdinalIgnoreCase));
            manual.Add(game with { Launch = launch, Candidate = false }); Save();
            games[games.FindIndex(g => g.Id == game.Id)] = game with { Launch = launch, Candidate = false };
        }
        if (Path.IsPathRooted(launch))
        { if (!File.Exists(launch) || !launch.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new IOException("El ejecutable no está disponible."); }
        else if (!(launch.StartsWith("steam://rungameid/") || launch.StartsWith("com.epicgames.launcher://apps/") || launch.StartsWith("uplay://launch/"))) throw new IOException("Destino de inicio no permitido.");
        Process.Start(new ProcessStartInfo(launch) { UseShellExecute = true, WorkingDirectory = game.Folder });
        Send(new { type = "launch", id = game.Id, state = "dispatched", message = "La solicitud se envió a Windows o al launcher. El juego puede tardar unos momentos en aparecer." });
    }
    async Task LoadLibrary()
    {
        bool hasCachedData = false;
        try
        {
            var path = Path.Combine(data, "library.json");
            if (File.Exists(path))
            {
                var cached = JsonSerializer.Deserialize<LibrarySnapshot>(await File.ReadAllTextAsync(path));
                if (cached?.Version == 1 && cached.Games != null && cached.Games.All(g => g != null && !string.IsNullOrWhiteSpace(g.Id) && !string.IsNullOrWhiteSpace(g.Name) && g.Folder != null && g.Launch != null))
                {
                    games = cached.Games; detectedFolders = cached.Folders ?? [];
                    hasCachedData = games.Count > 0;
                    if (hasCachedData) Send(new { type = "startup", hasCache = true });
                    SendLibrary(true); SendRoutes();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        if (!hasCachedData) Send(new { type = "startup", hasCache = false });
        // Yield to the first paint before discovering installations in the background.
        await Task.Delay(100);
        SendRoutes(); await Scan();
    }
    async Task Scan()
    {
        if (busy) return; busy = true; Send(new { type = "busy" });
        try
        {
            var previousImages = games.GroupBy(GameKey).ToDictionary(g => g.Key, g => g.First().Image);
              var rootsToCheck = extra.Concat(automatic ? detectedFolders : []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
              var observer = new Progress<ScanProgress>(report =>
              {
                  if (!owner.IsDisposed && web.CoreWebView2 != null) Send(new { type = "scan-progress", progress = report });
              });
              var result = await Task.Run(() => new Scanner().Run(rootsToCheck, automatic, observer));
            games = result.Games.Select(g => g with { Image = previousImages.GetValueOrDefault(GameKey(g)) }).ToList();
            scanNotes = result.Notes;
            detectedFolders = result.Folders ?? [];
            SendRoutes();
            foreach (var g in manual) { games.RemoveAll(x => string.Equals(x.Folder, g.Folder, StringComparison.OrdinalIgnoreCase)); if (Directory.Exists(g.Folder)) games.Add(g); }
            games = games.OrderBy(g => g.Name).ToList();
            SendLibrary(true);
              Send(new { type = "scan-progress", progress = new ScanProgress("previews", "Actualizando imágenes de la biblioteca", result.Folders?.Count ?? 0, result.Folders?.Count ?? 0, games.Count, 0) });
            var previews = new PreviewCache(Path.Combine(data, "Previews"));
            var pendingGames = games.ToArray();
            games = (await Task.Run(() => Task.WhenAll(pendingGames.Select(previews.Load)))).ToList();
            try
            {
                var path = Path.Combine(data, "library.json");
                await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(new LibrarySnapshot(1, games, detectedFolders, DateTimeOffset.UtcNow)));
                File.Move(path + ".tmp", path, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Send(new { type = "cache-warning", message = "La biblioteca se cargó, pero no se pudo guardar para el próximo inicio." }); }
              busy = false;
              SendLibrary(false);
        }
        catch (Exception ex)
        {
            SendLibrary(false);
            Send(new { type = "refresh-error", message = "No se pudo completar la actualización: " + ex.Message });
        } finally { busy = false; }
    }
    void Send(object message) => web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, json));
    void SendRoutes()
    {
        static string NormalizeFolder(string path) => Path.GetFullPath(path).Replace('/', '\\').TrimEnd('\\');
        var folders = detectedFolders.Select(NormalizeFolder).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var custom = extra.Select(NormalizeFolder).ToArray();
        var mainFolders = folders.Where(path => !custom.Any(parent => string.Equals(path, parent, StringComparison.OrdinalIgnoreCase) || path.StartsWith(parent + "\\", StringComparison.OrdinalIgnoreCase)) && !folders.Any(parent => !string.Equals(path, parent, StringComparison.OrdinalIgnoreCase) && path.StartsWith(parent + "\\", StringComparison.OrdinalIgnoreCase))).ToArray();
        Send(new { type = "routes", automatic, roots = extra.Select(p => new { path = p, available = Directory.Exists(p) }).ToArray(), automaticRoots = automatic ? mainFolders.Select(p => new { path = p, available = Directory.Exists(p) }).ToArray() : [] });
    }
    static string GameKey(Game g) => string.IsNullOrWhiteSpace(g.Launch) || Path.IsPathRooted(g.Launch) ? "folder:" + g.Folder.TrimEnd('\\').ToLowerInvariant() : g.Launch.ToLowerInvariant();
    string DisplayName(Game g) => gameNames.GetValueOrDefault(GameKey(g), g.Name);
    Game EffectiveGame(Game game) => gamePaths.TryGetValue(GameKey(game), out var paths) ? game with { Folder = paths.Folder, Launch = paths.Launch, Candidate = string.IsNullOrEmpty(paths.Launch) } : game;
    void SendLibrary(bool scanning)
    {
        var hidden = hiddenGames.Select(GameKey).ToHashSet();
        Send(new { type = "library", games = games.Where(g => !hidden.Contains(GameKey(g))).Select(g => EffectiveGame(g) with { Name = DisplayName(g) }).ToArray(), hiddenGames = hiddenGames.Select(g => new { key = GameKey(g), name = DisplayName(g) }).ToArray(), notes = scanNotes, favorites, isScanning = scanning });
    }
    void Save() => File.WriteAllText(Path.Combine(data, "settings.json"), JsonSerializer.Serialize(new State(manual, extra, favorites, automatic, hiddenGames, gameNames, gamePaths)));
}
