namespace Celeste.Mod.AutoMusicLastRoom;

internal static class RoomMonitor
{
    private static bool resetRequested;
    private static bool playbackSuppressed;

    private static AreaKey lastArea;
    private static string? lastRoom;
    private static TargetRoom? target;
    private static string? loggedTargetKey;
    private static bool musicStarted;
    private static bool loggedMissingTarget;
    private static bool loggedTargetAttempt;
    private static bool roomIsCompletionRoom;
    private static int retryTimer;

    public static void OnLevelLoaded(Level level)
    {
        HandleRoom(level, level.Session.Level, "LevelLoad");
    }

    public static void OnCtRoomChanged()
    {
        if (Engine.Scene is not Level level)
            return;

        if (!LobbyDetector.IsCollabHub(level))
            MusicController.Preload();

        HandleRoom(level, level.Session.Level, "CCT");
    }

    public static void Tick(Level level)
    {
        MusicController.Update(Engine.DeltaTime);

        if (!playbackSuppressed && !MusicController.IsPlaying && !MusicController.IsPreloaded &&
            !LobbyDetector.IsCollabHub(level))
        {
            MusicController.Preload();
        }

        string room = level.Session.Level;
        if (level.Session.Area != lastArea || !string.Equals(room, lastRoom, StringComparison.Ordinal))
            HandleRoom(level, room, "Tick");

        Process(level);
    }

    public static void RequestReset()
    {
        resetRequested = true;
    }

    public static void SuppressPlayback()
    {
        playbackSuppressed = true;
    }

    public static void ResumePlayback()
    {
        playbackSuppressed = false;
    }

    private static void HandleRoom(Level level, string room, string source)
    {
        AreaKey area = level.Session.Area;
        bool areaChanged = area != lastArea;
        bool roomChanged = areaChanged || !string.Equals(room, lastRoom, StringComparison.Ordinal);

        lastArea = area;
        lastRoom = room;

        if (roomChanged)
        {
            target = null;
            musicStarted = false;
            loggedMissingTarget = false;
            loggedTargetAttempt = false;
            roomIsCompletionRoom = false;
            retryTimer = 0;

            if (areaChanged)
                loggedTargetKey = null;
        }

        if (LobbyDetector.IsCollabHub(level))
        {
            target = null;
            musicStarted = false;

            if (MusicController.IsPlaying)
                MusicController.Stop(restoreGameMusic: true);

            if (roomChanged && AutoMusicLastRoomModule.Settings.LogTargetSelection)
            {
                Logger.Log(LogLevel.Info, AutoMusicLastRoomModule.ModName,
                    $"Room='{room}' map='{level.Session.Area.GetSID()}' source={source} collabHub=true (skipped)");
            }

            return;
        }

        target = LastRoomResolver.Resolve(level);
        roomIsCompletionRoom = LastRoomResolver.IsCompletionRoom(level, room);
        LogTarget(level, room, source, roomChanged);

        Process(level);
    }

    private static void Process(Level level)
    {
        if (LobbyDetector.IsCollabHub(level))
        {
            if (MusicController.IsPlaying)
                MusicController.Stop(restoreGameMusic: true);

            musicStarted = false;
            return;
        }

        var settings = AutoMusicLastRoomModule.Settings;

        if (resetRequested)
        {
            resetRequested = false;
            musicStarted = false;
        }

        if (!settings.Enabled || playbackSuppressed)
        {
            if (musicStarted)
            {
                MusicController.Stop(restoreGameMusic: true);
                musicStarted = false;
            }
            return;
        }

        if (target == null)
        {
            if (++retryTimer >= 30)
            {
                retryTimer = 0;
                target = LastRoomResolver.Resolve(level);
                if (target != null)
                    LogTarget(level, lastRoom ?? level.Session.Level, "Retry", roomChanged: false);
            }
            return;
        }

        bool atTarget = target.Matches(lastRoom ?? level.Session.Level);
        if (!atTarget)
        {
            if (settings.StopWhenLeavingTarget && !roomIsCompletionRoom &&
                (musicStarted || MusicController.IsPlaying))
            {
                MusicController.Stop(restoreGameMusic: true, fadeOut: true);
            }

            musicStarted = false;
            return;
        }

        if (musicStarted)
            return;

        if (MusicController.IsPlaying && !MusicController.IsFadingOut)
        {
            musicStarted = true;
            return;
        }

        Player? player = level.Entities.OfType<Player>().FirstOrDefault();
        if (player == null)
            return;

        bool holdingBerry = BerryDetector.IsHoldingOneLifeBerry(player);

        if (!loggedTargetAttempt && settings.LogTargetSelection)
        {
            loggedTargetAttempt = true;
            Logger.Log(LogLevel.Info, AutoMusicLastRoomModule.ModName,
                $"At target: room='{lastRoom}', requireBerry={settings.RequireBerry}, holdingBerry={holdingBerry}, " +
                $"preloaded={MusicController.IsPreloaded}, playing={MusicController.IsPlaying}");
        }

        if (settings.RequireBerry && !holdingBerry)
            return;

        MusicController.Play();
        musicStarted = true;
    }

    private static void LogTarget(Level level, string room, string source, bool roomChanged)
    {
        if (!AutoMusicLastRoomModule.Settings.LogTargetSelection)
            return;

        if (target == null)
        {
            if (roomChanged || !loggedMissingTarget)
            {
                loggedMissingTarget = true;
                Logger.Log(LogLevel.Info, AutoMusicLastRoomModule.ModName,
                    $"Room='{room}' map='{level.Session.Area.GetSID()}' source={source} target=-");
            }
            return;
        }

        if (!roomChanged && loggedTargetKey == target.LogKey)
            return;

        loggedTargetKey = target.LogKey;
        Logger.Log(LogLevel.Info, AutoMusicLastRoomModule.ModName,
            $"Room='{room}' map='{level.Session.Area.GetSID()}' source={source} target={target.Description}");
    }
}
