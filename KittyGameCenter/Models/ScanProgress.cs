namespace KittyGameCenter.Models;

public record ScanProgress(string Phase, string Path, int Completed, int Total, int Found, int Directories, string State = "scanning", int RouteGames = 0);
