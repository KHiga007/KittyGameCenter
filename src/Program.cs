using System.Diagnostics;
using System.Text.Json;
using KittyGameCenter.Services;
using KittyGameCenter.Views;

namespace KittyGameCenter;
static class Program
{
    [STAThread] static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--preview-check")
        {
            var cache = new PreviewCache(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!, "preview-check"));
            var detected = new Scanner().Run().Games.Where(g => g.Source != "Steam").ToList();
            var checkedGames = Task.WhenAll(detected.Select(cache.Load)).GetAwaiter().GetResult();
            File.WriteAllText(args[1], JsonSerializer.Serialize(checkedGames, new JsonSerializerOptions { WriteIndented = true })); return;
        }
        if (args.Length == 2 && args[0] == "--scan") { File.WriteAllText(args[1], JsonSerializer.Serialize(new Scanner().Run(), new JsonSerializerOptions { WriteIndented = true })); return; }
        ApplicationConfiguration.Initialize(); Application.Run(new MainWindow());
    }
}
