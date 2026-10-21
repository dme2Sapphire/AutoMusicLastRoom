namespace Celeste.Mod.AutoMusicLastRoom;

public static class DebugCommands
{
    [Command("automusic_test", "Play the configured AutoMusicLastRoom track")]
    public static void TestMusic()
    {
        MusicController.Play();
        Engine.Commands.Log("AutoMusicLastRoom: play requested.");
    }

    [Command("automusic_stop", "Stop AutoMusicLastRoom playback")]
    public static void StopMusic()
    {
        MusicController.Stop(restoreGameMusic: true);
        Engine.Commands.Log("AutoMusicLastRoom: stopped.");
    }

    [Command("automusic_state", "Print AutoMusicLastRoom settings and playback state")]
    public static void PrintState()
    {
        AutoMusicLastRoomSettings settings = AutoMusicLastRoomModule.Settings;
        Engine.Commands.Log(
            $"enabled={settings.Enabled}, requireBerry={settings.RequireBerry}, music='{settings.MusicFile}', " +
            $"tinyRoomHeuristic={settings.EnableTinyRoomHeuristic}, ratio={settings.GetTinyRoomAreaRatio()}, " +
            $"preloaded={MusicController.IsPreloaded}, playing={MusicController.IsPlaying}");

        if (Engine.Scene is Level level)
        {
            if (LobbyDetector.IsCollabHub(level))
            {
                Engine.Commands.Log($"room='{level.Session.Level}', collabHub=true (skipped)");
                return;
            }

            Engine.Commands.Log($"room='{level.Session.Level}', target={LastRoomResolver.Resolve(level)?.Description ?? "-"}");
        }
    }

    [Command("automusic_requireberry_off", "Disable the one-life berry requirement")]
    public static void RequireBerryOff()
    {
        AutoMusicLastRoomModule.Settings.RequireBerry = false;
        Engine.Commands.Log("AutoMusicLastRoom: requireBerry=false");
    }

    [Command("automusic_requireberry_on", "Enable the one-life berry requirement")]
    public static void RequireBerryOn()
    {
        AutoMusicLastRoomModule.Settings.RequireBerry = true;
        Engine.Commands.Log("AutoMusicLastRoom: requireBerry=true");
    }

    [Command("automusic_room", "Print the current room's entities and triggers")]
    public static void PrintRoom()
    {
        if (Engine.Scene is not Level level)
        {
            Engine.Commands.Log("AutoMusicLastRoom: not in a level.");
            return;
        }

        LevelData data = level.Session.LevelData;
        Engine.Commands.Log(
            $"room={data.Name} entities={data.Entities.Count} triggers={data.Triggers.Count} " +
            $"heartGem={data.HasHeartGem} gem={data.HasGem} checkpoint={data.HasCheckpoint}");

        int printed = 0;
        foreach (EntityData entity in data.Entities.Concat(data.Triggers))
        {
            if (printed++ >= 80)
            {
                Engine.Commands.Log("... (truncated)");
                break;
            }

            string values = entity.Values == null
                ? ""
                : string.Join(",", entity.Values
                    .Where(pair => pair.Value != null)
                    .Select(pair => $"{pair.Key}={pair.Value}"));

            Engine.Commands.Log(
                $"{entity.Name} pos={entity.Position} size={entity.Width}x{entity.Height} [{values}]");
        }
    }

    [Command("automusic_levels", "List room heart/gem markers in the current map")]
    public static void PrintLevels()
    {
        if (Engine.Scene is not Level level)
        {
            Engine.Commands.Log("AutoMusicLastRoom: not in a level.");
            return;
        }

        foreach (LevelData data in level.Session.MapData.Levels)
        {
            string markers = string.Join(",", data.Entities.Concat(data.Triggers)
                .Select(entity => entity.Name)
                .Where(name => name != null &&
                    (name.Contains("heart", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("gem", StringComparison.OrdinalIgnoreCase)))
                .Distinct());

            float area = data.Bounds.Width * (float)data.Bounds.Height;
            Engine.Commands.Log(
                $"{data.Name} dummy={data.Dummy} heartGem={data.HasHeartGem} gem={data.HasGem} " +
                $"area={area:0} markers=[{markers}]");
        }
    }

    [Command("automusic_target", "Print the resolved last-room target")]
    public static void PrintTarget()
    {
        if (Engine.Scene is not Level level)
        {
            Engine.Commands.Log("AutoMusicLastRoom: not in a level.");
            return;
        }

        if (LobbyDetector.IsCollabHub(level))
        {
            Engine.Commands.Log("AutoMusicLastRoom: collab lobby/gym detected; target is skipped here.");
            return;
        }

        TargetRoom? target = LastRoomResolver.Resolve(level);
        Engine.Commands.Log(target?.Description ?? "AutoMusicLastRoom: no target resolved.");
    }
}
