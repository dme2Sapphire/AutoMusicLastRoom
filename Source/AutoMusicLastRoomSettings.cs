namespace Celeste.Mod.AutoMusicLastRoom;

public enum TinyRoomAreaRatioOption
{
    OnePercent,
    TenPercent,
    TwentyPercent,
    ThirtyPercent,
    FiftyPercent,
    SeventyFivePercent,
}

public class AutoMusicLastRoomSettings : EverestModuleSettings
{
    public bool Enabled { get; set; } = true;

    public bool RequireBerry { get; set; } = true;

    public string MusicFile { get; set; } = "Audio/rain_love.ogg";

    public float Volume { get; set; } = 0.8f;

    public bool Loop { get; set; } = false;

    [SettingIgnore]
    public bool AdvancedOptions { get; set; }

    [SettingIgnore]
    public bool UseConsistencyTrackerPath { get; set; } = true;

    [SettingIgnore]
    public bool EnableTinyRoomHeuristic { get; set; } = true;

    [SettingIgnore]
    public TinyRoomAreaRatioOption TinyRoomAreaRatio { get; set; } = TinyRoomAreaRatioOption.FiftyPercent;

    [SettingIgnore]
    public string ExtraOneLifeBerryTypes { get; set; } = "SilverBerry, PlatinumBerry";

    [SettingIgnore]
    public string MusicEvent { get; set; } = "";

    [SettingIgnore]
    public bool StopWhenLeavingTarget { get; set; } = true;

    [SettingIgnore]
    public bool LogTargetSelection { get; set; } = true;

    public float GetTinyRoomAreaRatio() => TinyRoomAreaRatio switch
    {
        TinyRoomAreaRatioOption.OnePercent => 0.01f,
        TinyRoomAreaRatioOption.TenPercent => 0.1f,
        TinyRoomAreaRatioOption.TwentyPercent => 0.2f,
        TinyRoomAreaRatioOption.ThirtyPercent => 0.3f,
        TinyRoomAreaRatioOption.SeventyFivePercent => 0.75f,
        _ => 0.5f,
    };

    public void CreateMusicFileEntry(TextMenu menu, bool inGame)
    {
        List<string> tracks = MusicLibrary.GetTracks();

        if (tracks.Count == 0)
        {
            TextMenu.Button empty = new(Dialog.Clean("modoptions_automusiclastroom_no_music"));
            empty.Disabled = true;
            menu.Add(empty);
            return;
        }

        if (!string.IsNullOrWhiteSpace(MusicFile) &&
            !tracks.Any(track => string.Equals(track, MusicFile, StringComparison.OrdinalIgnoreCase)))
        {
            tracks.Insert(0, MusicFile);
        }

        int selected = tracks.FindIndex(track =>
            string.Equals(track, MusicFile, StringComparison.OrdinalIgnoreCase));
        if (selected < 0)
            selected = 0;

        menu.Add(new TextMenu.Slider(
                Dialog.Clean("modoptions_automusiclastroom_music"),
                index => MusicLibrary.GetTrackLabel(tracks[index]),
                0,
                tracks.Count - 1,
                selected)
            .Change(index => MusicFile = tracks[index]));
    }

    public void CreateVolumeEntry(TextMenu menu, bool inGame)
    {
        int percent = (int)Math.Round(Math.Clamp(Volume, 0f, 1f) * 100);
        menu.Add(new TextMenu.Slider(
                Dialog.Clean("modoptions_automusiclastroom_volume"),
                value => $"{value}%",
                0,
                100,
                percent)
            .Change(value => Volume = value / 100f));
    }

    public void CreateAdvancedOptionsEntry(TextMenu menu, bool inGame)
    {
        TextMenuExt.SubMenu submenu = new(
            Dialog.Clean("modoptions_automusiclastroom_advanced"),
            enterOnSelect: false);

        TextMenu.OnOff useCtPath = new(
            Dialog.Clean("modoptions_automusiclastroom_ctpath"),
            UseConsistencyTrackerPath);
        useCtPath.Change(value => UseConsistencyTrackerPath = value);

        TextMenu.Slider? ratioSlider = null;

        TextMenu.OnOff tinyRoom = new(
            Dialog.Clean("modoptions_automusiclastroom_tinyroom"),
            EnableTinyRoomHeuristic);
        tinyRoom.Change(value =>
        {
            EnableTinyRoomHeuristic = value;
            if (ratioSlider != null)
                ratioSlider.Disabled = !value;
        });

        ratioSlider = new TextMenu.Slider(
            Dialog.Clean("modoptions_automusiclastroom_ratio"),
            value => value switch
            {
                0 => "0.01",
                1 => "0.1",
                2 => "0.2",
                3 => "0.3",
                4 => "0.5",
                5 => "0.75",
                _ => "0.5",
            },
            0,
            5,
            (int)TinyRoomAreaRatio);
        ratioSlider.Change(value => TinyRoomAreaRatio = (TinyRoomAreaRatioOption)value);
        ratioSlider.Disabled = !EnableTinyRoomHeuristic;

        submenu.Add(useCtPath);
        submenu.Add(tinyRoom);
        submenu.Add(ratioSlider);
        submenu.Add(new TextMenu.OnOff(
                Dialog.Clean("modoptions_automusiclastroom_stopleaving"),
                StopWhenLeavingTarget)
            .Change(value => StopWhenLeavingTarget = value));
        submenu.Add(new TextMenu.OnOff(
                Dialog.Clean("modoptions_automusiclastroom_logtarget"),
                LogTargetSelection)
            .Change(value => LogTargetSelection = value));

        menu.Add(submenu);
    }
}
