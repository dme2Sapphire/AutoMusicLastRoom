namespace Celeste.Mod.AutoMusicLastRoom;

internal static class LobbyDetector
{
    private const string LobbyHelperTypeName = "Celeste.Mod.CollabUtils2.LobbyHelper";

    private static string? cachedSid;
    private static bool cachedIsHub;
    private static MethodInfo? lobbyMethod;
    private static MethodInfo? gymMethod;
    private static bool lookupAttempted;

    public static bool IsCollabHub(Level level)
    {
        string sid = level.Session.Area.GetSID();
        if (string.IsNullOrEmpty(sid))
            return false;

        if (string.Equals(sid, cachedSid, StringComparison.Ordinal))
            return cachedIsHub;

        cachedSid = sid;
        cachedIsHub = QueryCollabUtils(sid) || HasLobbyMarkers(level);

        if (cachedIsHub && AutoMusicLastRoomModule.Settings.LogTargetSelection)
        {
            Logger.Log(LogLevel.Info, AutoMusicLastRoomModule.ModName,
                $"Collab lobby/gym detected: sid='{sid}' - auto music is disabled here.");
        }

        return cachedIsHub;
    }

    private static bool QueryCollabUtils(string sid)
    {
        try
        {
            if (!lookupAttempted)
            {
                lookupAttempted = true;

                Type? helperType = Type.GetType($"{LobbyHelperTypeName}, CollabUtils2");
                if (helperType == null)
                {
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (!string.Equals(assembly.GetName().Name, "CollabUtils2", StringComparison.OrdinalIgnoreCase))
                            continue;

                        helperType = assembly.GetType(LobbyHelperTypeName);
                        if (helperType != null)
                            break;
                    }
                }

                lobbyMethod = helperType?.GetMethod(
                    "IsCollabLobby",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string) },
                    null);

                gymMethod = helperType?.GetMethod(
                    "IsCollabGym",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string) },
                    null);
            }

            if (lobbyMethod?.Invoke(null, new object[] { sid }) is true)
                return true;

            return gymMethod?.Invoke(null, new object[] { sid }) is true;
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Warn, AutoMusicLastRoomModule.ModName,
                $"CollabUtils2 lobby/gym lookup failed: {e.Message}");
            return false;
        }
    }

    private static bool HasLobbyMarkers(Level level)
    {
        foreach (LevelData data in level.Session.MapData.Levels)
        {
            foreach (EntityData entity in data.Entities.Concat(data.Triggers))
            {
                string name = entity.Name ?? "";
                if (name.Contains("chapterpanel", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("lobbymapwarp", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("lobbymapcontroller", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("lobbywarppoint", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
