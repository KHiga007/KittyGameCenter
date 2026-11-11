namespace KittyGameCenter.Models;

record State(List<Game> Manual, List<string> Roots, HashSet<string> Favorites, bool Automatic = true, List<Game>? HiddenGames = null, Dictionary<string, string>? GameNames = null, Dictionary<string, GamePaths>? GamePaths = null);
