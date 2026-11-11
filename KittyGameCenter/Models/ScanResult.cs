namespace KittyGameCenter.Models;

public record ScanResult(List<Game> Games, List<string> Notes, List<string>? Folders = null);
