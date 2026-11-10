namespace KittyGameCenter.Models;

record LibrarySnapshot(int Version, List<Game> Games, List<string>? Folders, DateTimeOffset UpdatedAt);
