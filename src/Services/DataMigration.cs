using KittyGameCenter.Models;

namespace KittyGameCenter.Services;
static class DataMigration
{
    public static void CopyExistingData(string previous, string current)
    {
        if (!Directory.Exists(previous) || File.Exists(Path.Combine(current, ".migration-complete"))) return;
        try
        {
            Directory.CreateDirectory(current);
            foreach (var name in new[] { "library.json", "settings.json" })
            {
                var source = Path.Combine(previous, name);
                var destination = Path.Combine(current, name);
                if (File.Exists(source) && !File.Exists(destination)) File.Copy(source, destination);
            }
            var images = Path.Combine(previous, "Previews");
            if (Directory.Exists(images))
            {
                Directory.CreateDirectory(Path.Combine(current, "Previews"));
                foreach (var source in Directory.EnumerateFiles(images))
                {
                    if (!new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(Path.GetExtension(source).ToLowerInvariant())) continue;
                    var destination = Path.Combine(current, "Previews", Path.GetFileName(source));
                    if (!File.Exists(destination)) File.Copy(source, destination);
                }
            }
            File.WriteAllText(Path.Combine(current, ".migration-complete"), "1");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Retry remaining files on the next start. */ }
    }
}
