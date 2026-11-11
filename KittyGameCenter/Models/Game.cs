namespace KittyGameCenter.Models;

public record Game(string Id, string Name, string Source, string Folder, string Launch, bool Candidate = false, string? Image = null);
