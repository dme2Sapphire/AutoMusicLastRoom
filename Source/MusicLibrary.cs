namespace Celeste.Mod.AutoMusicLastRoom;

internal static class MusicLibrary
{
    private static readonly string[] SupportedExtensions = { ".ogg", ".qoa" };

    public static List<string> GetTracks()
    {
        var tracks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string audioDirectory = Path.Combine(
            AppContext.BaseDirectory, "Mods", AutoMusicLastRoomModule.ModName, "Audio");

        try
        {
            if (Directory.Exists(audioDirectory))
            {
                foreach (string file in Directory.EnumerateFiles(audioDirectory, "*", SearchOption.AllDirectories))
                {
                    if (!IsSupported(file))
                        continue;

                    string relative = Path.GetRelativePath(audioDirectory, file).Replace('\\', '/');
                    tracks.Add("Audio/" + relative);
                }
            }
        }
        catch (Exception)
        {
            // Content lookup below is the fallback for zip-installed mods.
        }

        try
        {
            foreach (ModContent content in Everest.Content.Mods)
            {
                bool isOurMod =
                    string.Equals(content.Mod?.Name, AutoMusicLastRoomModule.ModName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(content.Name, AutoMusicLastRoomModule.ModName, StringComparison.OrdinalIgnoreCase);

                if (!isOurMod)
                    continue;

                foreach (string path in content.Map.Keys)
                {
                    if (path.StartsWith("Audio/", StringComparison.OrdinalIgnoreCase) && IsSupported(path))
                        tracks.Add(path.Replace('\\', '/'));
                }
            }
        }
        catch (Exception)
        {
            // Ignore content lookup failures and show the physical files, if any.
        }

        return tracks
            .OrderBy(track => track, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string GetTrackLabel(string track)
    {
        string label = track.StartsWith("Audio/", StringComparison.OrdinalIgnoreCase)
            ? track[6..]
            : track;
        return label.Replace('\\', '/');
    }

    private static bool IsSupported(string path)
    {
        string extension = Path.GetExtension(path);
        return SupportedExtensions.Any(supported =>
            string.Equals(extension, supported, StringComparison.OrdinalIgnoreCase));
    }
}
