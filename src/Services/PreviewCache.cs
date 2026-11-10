using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Drawing.Imaging;

using KittyGameCenter.Models;

namespace KittyGameCenter.Services;
public class PreviewCache(string folder)
{
    static readonly HttpClient http = CreateHttp();
    readonly SemaphoreSlim slots = new(6);
    static HttpClient CreateHttp() { var c = new HttpClient { Timeout = TimeSpan.FromSeconds(7) }; c.DefaultRequestHeaders.UserAgent.ParseAdd("KittyGameCenter/0.3"); return c; }
    static string Normalize(string title) => Regex.Replace(title.Normalize(NormalizationForm.FormD).ToLowerInvariant(), @"[^a-z0-9]", "");
    static string Key(Game g) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(g.Source + "|" + g.Name))).ToLowerInvariant()[..24];
    static bool Allowed(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "https" && new[] { "steamstatic.com", "steamcdn-a.akamaihd.net", "epicgames.com", "unrealengine.com", "gog-statics.com", "gog.com" }.Any(h => u.Host == h || u.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));
    static IEnumerable<string> SteamImages(string id) => new[] {
        $"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{id}/header.jpg",
        $"https://cdn.akamai.steamstatic.com/steam/apps/{id}/header.jpg",
        $"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{id}/library_600x900.jpg" };
    public async Task<Game> Load(Game game)
    {
        await slots.WaitAsync();
        var key = Key(game); var path = Path.Combine(folder, key + ".png");
        Game Result() => game with { Image = "https://kitty-images.local/" + key + ".png" };
        try
        {
            Directory.CreateDirectory(folder);
            if (File.Exists(path) && new FileInfo(path).Length > 100) return Result();
            foreach (var local in LocalArt(game)) if (SaveLocal(local, path)) return Result();
            var steam = Regex.Match(game.Launch, @"^steam://rungameid/(\d+)$");
            if (steam.Success)
            {
                var id = steam.Groups[1].Value;
                // Reuse previews downloaded by earlier versions.
                var old = Path.Combine(folder, id + ".jpg");
                if (File.Exists(old) && SaveLocal(old, path)) return Result();
                foreach (var url in SteamImages(id)) if (await Download(url, path)) return Result();
                if (await SteamDetails(id, path)) return Result();
            }
            else
            {
                if (await SearchSteam(game.Name, path)) return Result();
                if (await SearchEpic(game.Name, path)) return Result();
                if (await SearchGog(game.Name, path)) return Result();
            }
            // A game-specific executable icon is preferable to a generic placeholder.
            var exe = Path.IsPathRooted(game.Launch) && File.Exists(game.Launch) ? game.Launch : Scanner.FindExe(game.Folder);
            if (File.Exists(exe))
            {
                using var icon = Icon.ExtractAssociatedIcon(exe);
                if (icon != null) { using var bitmap = icon.ToBitmap(); bitmap.Save(path, ImageFormat.Png); return Result(); }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { }
        finally { slots.Release(); }
        return game;
    }
    static IEnumerable<string> LocalArt(Game g)
    {
        var dirs = new[] { g.Folder, Path.Combine(g.Folder, "Assets"), Path.Combine(g.Folder, "Images"), Path.Combine(g.Folder, "Content", "Assets") };
        foreach (var dir in dirs)
        {
            string[] files; try { files = Directory.GetFiles(dir); } catch { continue; }
            foreach (var f in files.Where(f => Regex.IsMatch(Path.GetExtension(f), @"(?i)^\.(png|jpg|jpeg|bmp)$") && Regex.IsMatch(Path.GetFileNameWithoutExtension(f), @"(?i)(^(header|cover|poster|banner|preview|splash|folder|boxart)$|goggame-.*|StoreLogo|Square150x150Logo|Wide310x150Logo)")).OrderBy(f => Path.GetFileName(f).Contains("Logo", StringComparison.OrdinalIgnoreCase) ? 1 : 0)) yield return f;
        }
    }
    static bool SaveLocal(string input, string output)
    {
        try { using var image = Image.FromFile(input); if (image.Width < 16 || image.Height < 16) return false; image.Save(output, ImageFormat.Png); return true; } catch { return false; }
    }
    static async Task<bool> Download(string url, string output)
    {
        if (!Allowed(url)) return false;
        try
        {
            using var r = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!r.IsSuccessStatusCode || r.Content.Headers.ContentType?.MediaType?.StartsWith("image/") != true || r.Content.Headers.ContentLength > 15_000_000) return false;
            if (!Allowed(r.RequestMessage?.RequestUri?.ToString() ?? "")) return false;
            using var stream = await r.Content.ReadAsStreamAsync(); using var buffer = new MemoryStream();
            var chunk = new byte[8192]; int n;
            while ((n = await stream.ReadAsync(chunk)) > 0) { if (buffer.Length + n > 15_000_000) return false; buffer.Write(chunk, 0, n); }
            buffer.Position = 0; using var img = Image.FromStream(buffer);
            if (img.Width < 16 || img.Height < 16) return false;
            img.Save(output, ImageFormat.Png); return true;
        } catch { return false; }
    }
    static async Task<bool> SteamDetails(string id, string path)
    {
        try
        {
            using var j = JsonDocument.Parse(await http.GetStringAsync("https://store.steampowered.com/api/appdetails?appids=" + id + "&filters=basic"));
            if (j.RootElement.GetProperty(id).TryGetProperty("data", out var data) && data.TryGetProperty("header_image", out var image)) return await Download(image.GetString() ?? "", path);
        } catch { }
        return false;
    }
    static async Task<bool> SearchSteam(string title, string path)
    {
        try
        {
            using var j = JsonDocument.Parse(await http.GetStringAsync("https://steamcommunity.com/actions/SearchApps/" + Uri.EscapeDataString(title)));
            foreach (var item in j.RootElement.EnumerateArray())
            {
                if (Normalize(item.GetProperty("name").GetString() ?? "") != Normalize(title)) continue;
                var id = item.GetProperty("appid").ToString();
                if (!Regex.IsMatch(id, @"^\d+$")) continue;
                if (await SteamDetails(id, path)) return true;
                foreach (var url in SteamImages(id)) if (await Download(url, path)) return true;
                if (item.TryGetProperty("logo", out var logo) && await Download(logo.GetString() ?? "", path)) return true;
            }
        } catch { }
        return false;
    }
    static IEnumerable<string> ImageUrls(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject())
            {
                if (Regex.IsMatch(p.Name, @"(?i)(rating|social|logo)")) continue;
                if (p.Value.ValueKind == JsonValueKind.String && Regex.IsMatch(p.Name, @"(?i)(image|thumbnail|artwork|url|^src$)") && p.Value.GetString() is string url && Allowed(url) && Regex.IsMatch(url, @"(?i)\.(jpg|jpeg|png)(\?|$)")) yield return url;
                else foreach (var u in ImageUrls(p.Value)) yield return u;
            }
        }
        else if (e.ValueKind == JsonValueKind.Array) foreach (var a in e.EnumerateArray()) foreach (var u in ImageUrls(a)) yield return u;
    }
    static async Task<bool> SearchEpic(string title, string path)
    {
        try
        {
            var slug = Regex.Replace(title.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            using var j = JsonDocument.Parse(await http.GetStringAsync("https://store-content.ak.epicgames.com/api/en-US/content/products/" + slug));
            if (!j.RootElement.TryGetProperty("productName", out var name) || Normalize(name.GetString() ?? "") != Normalize(title)) return false;
            foreach (var url in ImageUrls(j.RootElement).Distinct().OrderByDescending(u => Regex.IsMatch(u, @"(2560x1440|1920x1080|3840x2160)")).Take(5)) if (await Download(url, path)) return true;
        } catch { }
        return false;
    }
    static async Task<bool> SearchGog(string title, string path)
    {
        try
        {
            using var j = JsonDocument.Parse(await http.GetStringAsync("https://embed.gog.com/games/ajax/filtered?mediaType=game&search=" + Uri.EscapeDataString(title)));
            foreach (var product in j.RootElement.GetProperty("products").EnumerateArray())
            {
                if (Normalize(product.GetProperty("title").GetString() ?? "") != Normalize(title)) continue;
                var image = product.GetProperty("image").GetString() ?? "";
                if (image.StartsWith("//")) image = "https:" + image;
                if (await Download(image + "_product_card_v2_mobile_slider_639.jpg", path)) return true;
                if (await Download(image, path)) return true;
            }
        } catch { }
        return false;
    }
}
