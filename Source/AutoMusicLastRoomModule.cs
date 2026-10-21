namespace Celeste.Mod.AutoMusicLastRoom;

public class AutoMusicLastRoomModule : EverestModule
{
    public const string ModName = "AutoMusicLastRoom";

    public static AutoMusicLastRoomModule Instance { get; private set; } = null!;

    private static AreaKey lastLoadedArea;
    private static bool hasLoadedArea;

    public override Type SettingsType => typeof(AutoMusicLastRoomSettings);
    public static AutoMusicLastRoomSettings Settings => (AutoMusicLastRoomSettings)Instance._Settings;

    public AutoMusicLastRoomModule()
    {
        Instance = this;
    }

    public override void Load()
    {
        Everest.Events.Level.OnLoadLevel += OnLoadLevel;
        Everest.Events.Level.OnAfterUpdate += OnAfterUpdate;
        Everest.Events.Level.OnComplete += OnLevelComplete;
        Everest.Events.Level.OnExit += OnLevelExit;

        CtEvents.TrySubscribe();
    }

    public override void Unload()
    {
        Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
        Everest.Events.Level.OnAfterUpdate -= OnAfterUpdate;
        Everest.Events.Level.OnComplete -= OnLevelComplete;
        Everest.Events.Level.OnExit -= OnLevelExit;

        CtEvents.Unsubscribe();
        MusicController.Stop(restoreGameMusic: false, fadeOut: false);
    }

    private static void OnLoadLevel(Level level, Player.IntroTypes intro, bool fromLoader)
    {
        AreaKey area = level.Session.Area;
        if (hasLoadedArea && area != lastLoadedArea)
            MusicController.Stop(restoreGameMusic: false, fadeOut: false);

        lastLoadedArea = area;
        hasLoadedArea = true;

        CtEvents.TrySubscribe();
        RoomMonitor.ResumePlayback();

        if (LobbyDetector.IsCollabHub(level))
            MusicController.Stop(restoreGameMusic: false);
        else
            MusicController.Preload();

        if (!CtEvents.IsSubscribed)
            RoomMonitor.OnLevelLoaded(level);
    }

    private static void OnAfterUpdate(Level level)
    {
        RoomMonitor.Tick(level);
    }

    private static void OnLevelComplete(Level level)
    {
        MusicController.Stop(restoreGameMusic: false, fadeOut: false);
        RoomMonitor.RequestReset();
        RoomMonitor.SuppressPlayback();
    }

    private static void OnLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow)
    {
        MusicController.Stop(restoreGameMusic: false, fadeOut: false);
        RoomMonitor.RequestReset();
        RoomMonitor.SuppressPlayback();
    }

}
