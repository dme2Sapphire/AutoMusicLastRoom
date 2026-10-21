namespace Celeste.Mod.AutoMusicLastRoom;

internal static class FmodMusic
{
    public static bool TryLoad(string path, out FMOD.Sound? sound)
    {
        sound = null;

        try
        {
            if (Audio.System == null)
                return false;

            FMOD.RESULT result = Audio.System.getLowLevelSystem(out FMOD.System core);
            if (result != FMOD.RESULT.OK || core == null)
                return false;

            result = core.createSound(path, FMOD.MODE.CREATESTREAM, out sound);
            if (result != FMOD.RESULT.OK || sound == null)
            {
                sound = null;
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Warn, AutoMusicLastRoomModule.ModName,
                $"FMOD preload failed for '{path}': {e.Message}");
            sound = null;
            return false;
        }
    }

    public static bool TryPlay(FMOD.Sound sound, bool loop, float volume, out FMOD.Channel? channel)
    {
        channel = null;

        try
        {
            if (Audio.System == null)
                return false;

            FMOD.RESULT result = Audio.System.getLowLevelSystem(out FMOD.System core);
            if (result != FMOD.RESULT.OK || core == null)
                return false;

            result = core.playSound(sound, null, paused: true, out channel);
            if (result != FMOD.RESULT.OK || channel == null)
            {
                channel = null;
                return false;
            }

            channel.setMode(FMOD.MODE._2D | (loop ? FMOD.MODE.LOOP_NORMAL : FMOD.MODE.LOOP_OFF));
            channel.setVolume(Math.Clamp(volume, 0f, 1f));
            channel.setPaused(false);
            return true;
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Warn, AutoMusicLastRoomModule.ModName,
                $"FMOD playback failed: {e.Message}");
            channel = null;
            return false;
        }
    }

    public static void Stop(FMOD.Channel? channel, FMOD.Sound? sound)
    {
        try
        {
            channel?.stop();
        }
        catch (Exception)
        {
            // The audio system may already be shutting down.
        }

        try
        {
            sound?.release();
        }
        catch (Exception)
        {
            // The audio system may already be shutting down.
        }
    }
}
