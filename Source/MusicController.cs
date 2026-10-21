using Microsoft.Xna.Framework.Media;

namespace Celeste.Mod.AutoMusicLastRoom;

internal static class MusicController
{
    private const float FadeInDuration = 0.8f;
    private const float FadeOutDuration = 0.6f;
    private const float FadeInStartVolume = 0.3f;

    private static Song? song;
    private static string? songSetting;
    private static string? songPath;
    private static FMOD.Sound? fmodSound;
    private static FMOD.Channel? fmodChannel;
    private static string? previousMusic;
    private static bool playing;
    private static bool playingEvent;
    private static bool preloaded;
    private static string? preloadAttemptedSetting;

    private static bool fading;
    private static bool fadingOut;
    private static float fadeTimer;
    private static float fadeDuration;
    private static float fadeStartVolume;
    private static float fadeTargetVolume;
    private static float currentVolume;
    private static bool fadeRestoreGameMusic;

    public static bool IsPreloaded => preloaded;
    public static bool IsPlaying => playing;
    public static bool IsFadingOut => fadingOut;

    public static void Preload()
    {
        var settings = AutoMusicLastRoomModule.Settings;

        if (!settings.Enabled || playing || playingEvent)
            return;

        if (!string.IsNullOrWhiteSpace(settings.MusicEvent))
            return;

        string setting = settings.MusicFile?.Trim() ?? "";
        if (string.IsNullOrEmpty(setting))
            return;

        if (preloaded)
        {
            if (string.Equals(songSetting, setting, StringComparison.Ordinal))
                return;

            Stop(restoreGameMusic: false, fadeOut: false);
        }

        if (string.Equals(preloadAttemptedSetting, setting, StringComparison.Ordinal))
            return;

        preloadAttemptedSetting = setting;

        string? musicPath = ResolveMusicFile(setting);
        if (musicPath == null)
            return;

        if (!FmodMusic.TryLoad(musicPath, out FMOD.Sound? sound) || sound == null)
        {
            Logger.Log(LogLevel.Warn, AutoMusicLastRoomModule.ModName,
                $"Could not preload music with FMOD: {musicPath}");
            return;
        }

        fmodSound = sound;
        songSetting = setting;
        songPath = musicPath;
        preloaded = true;

        Logger.Log(LogLevel.Info, AutoMusicLastRoomModule.ModName, $"Preloaded music: {musicPath}");
    }

    public static void Play()
    {
        var settings = AutoMusicLastRoomModule.Settings;

        if (TryPlayPreloaded(settings))
            return;

        Stop(restoreGameMusic: false, fadeOut: false);

        if (!string.IsNullOrWhiteSpace(settings.MusicEvent))
        {
            if (!TryPlayEvent(settings.MusicEvent.Trim()))
                Logger.Log(LogLevel.Warn, AutoMusicLastRoomModule.ModName,
                    $"Could not start FMOD event '{settings.MusicEvent}'. Falling back to the audio file.");
        }

        if (playing)
            return;

        string? musicPath = ResolveMusicFile(settings.MusicFile);
        if (musicPath == null)
        {
            Stop(restoreGameMusic: true, fadeOut: false);
            return;
        }

        try
        {
            previousMusic ??= Audio.CurrentMusic;
            StopGameMusic();

            song = Song.FromUri(AutoMusicLastRoomModule.ModName, new Uri(musicPath));
            songSetting = settings.MusicFile?.Trim();
            songPath = musicPath;

            float targetVolume = Math.Clamp(settings.Volume * Audio.MusicVolume, 0f, 1f);
            float startVolume = Math.Min(FadeInStartVolume, targetVolume);

            MediaPlayer.IsRepeating = settings.Loop;
            MediaPlayer.Volume = startVolume;
            MediaPlayer.Play(song);
            playing = true;
            StartFadeIn(targetVolume);

            Logger.Log(LogLevel.Info, AutoMusicLastRoomModule.ModName, $"Playing music: {musicPath}");
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Error, AutoMusicLastRoomModule.ModName,
                $"Failed to play music file '{musicPath}': {e}");
            Stop(restoreGameMusic: true, fadeOut: false);
        }
    }

    private static bool TryPlayPreloaded(AutoMusicLastRoomSettings settings)
    {
        if (!preloaded || fmodSound == null || playing || playingEvent)
            return false;

        if (!string.IsNullOrWhiteSpace(settings.MusicEvent))
            return false;

        if (!string.Equals(songSetting, settings.MusicFile?.Trim(), StringComparison.Ordinal))
            return false;

        float targetVolume = Math.Clamp(settings.Volume * Audio.MusicVolume, 0f, 1f);
        float startVolume = Math.Min(FadeInStartVolume, targetVolume);

        if (!FmodMusic.TryPlay(fmodSound, settings.Loop, startVolume, out FMOD.Channel? channel) || channel == null)
            return false;

        fmodChannel = channel;
        playing = true;
        preloaded = false;

        previousMusic = Audio.CurrentMusic;
        StopGameMusic();
        StartFadeIn(targetVolume);

        Logger.Log(LogLevel.Info, AutoMusicLastRoomModule.ModName,
            $"Playing preloaded music: {songPath}");
        return true;
    }

    public static void Stop(bool restoreGameMusic, bool fadeOut = true)
    {
        if (playing && fadeOut && !playingEvent)
        {
            fadeRestoreGameMusic |= restoreGameMusic;

            if (!fadingOut)
                StartFadeOut();

            return;
        }

        FinalizeStop(restoreGameMusic);
    }

    public static void Update(float deltaTime)
    {
        if (!fading)
            return;

        fadeTimer += Math.Max(0f, deltaTime);
        float progress = fadeDuration <= 0f ? 1f : Math.Clamp(fadeTimer / fadeDuration, 0f, 1f);
        currentVolume = MathHelper.Lerp(fadeStartVolume, fadeTargetVolume, progress);
        ApplyVolume(currentVolume);

        if (progress < 1f)
            return;

        fading = false;

        if (fadingOut)
        {
            fadingOut = false;
            FinalizeStop(fadeRestoreGameMusic);
        }
    }

    private static void StartFadeIn(float targetVolume)
    {
        float startVolume = Math.Min(FadeInStartVolume, targetVolume);

        fading = true;
        fadingOut = false;
        fadeTimer = 0f;
        fadeDuration = FadeInDuration;
        fadeStartVolume = startVolume;
        fadeTargetVolume = targetVolume;
        currentVolume = startVolume;

        ApplyVolume(startVolume);
    }

    private static void StartFadeOut()
    {
        fading = true;
        fadingOut = true;
        fadeTimer = 0f;
        fadeDuration = FadeOutDuration;
        fadeStartVolume = currentVolume;
        fadeTargetVolume = 0f;
    }

    private static void ApplyVolume(float volume)
    {
        try
        {
            if (fmodChannel != null)
                fmodChannel.setVolume(volume);
            else if (song != null)
                MediaPlayer.Volume = volume;
        }
        catch (Exception)
        {
            // The audio system may already be shutting down.
        }
    }

    private static void FinalizeStop(bool restoreGameMusic)
    {
        bool wasPlaying = playing;

        fading = false;
        fadingOut = false;
        fadeRestoreGameMusic = false;

        if (fmodChannel != null || fmodSound != null)
        {
            FmodMusic.Stop(fmodChannel, fmodSound);
            fmodChannel = null;
            fmodSound = null;
            preloaded = false;
            playing = false;
        }

        if (playing && song != null)
        {
            try
            {
                MediaPlayer.Stop();
            }
            catch (Exception)
            {
                // The media player may already be shut down during game exit.
            }

            MediaPlayer.IsRepeating = false;
            playing = false;
        }

        song?.Dispose();
        song = null;
        songSetting = null;
        songPath = null;
        preloaded = false;
        preloadAttemptedSetting = null;
        currentVolume = 0f;

        if (playingEvent)
        {
            try
            {
                Audio.SetMusic("", startPlaying: false, allowFadeOut: true);
            }
            catch (Exception)
            {
                // The audio system may already be shutting down.
            }

            playingEvent = false;
            playing = false;
        }

        if (wasPlaying)
            Logger.Log(LogLevel.Info, AutoMusicLastRoomModule.ModName, "Stopped music.");

        if (restoreGameMusic && !string.IsNullOrEmpty(previousMusic))
        {
            try
            {
                Audio.SetMusic(previousMusic, startPlaying: true, allowFadeOut: false);
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Warn, AutoMusicLastRoomModule.ModName,
                    $"Could not restore previous music '{previousMusic}': {e.Message}");
            }
        }

        previousMusic = null;
    }

    private static bool TryPlayEvent(string eventPath)
    {
        try
        {
            previousMusic = Audio.CurrentMusic;
            StopGameMusic();

            if (!Audio.SetMusic(eventPath, startPlaying: true, allowFadeOut: true))
                return false;

            playing = true;
            playingEvent = true;
            Logger.Log(LogLevel.Info, AutoMusicLastRoomModule.ModName, $"Playing FMOD event: {eventPath}");
            return true;
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Warn, AutoMusicLastRoomModule.ModName,
                $"Failed to start FMOD event '{eventPath}': {e.Message}");
            return false;
        }
    }

    private static void StopGameMusic()
    {
        try
        {
            Audio.SetMusic("", startPlaying: false, allowFadeOut: true);
        }
        catch (Exception)
        {
            // Empty music paths are ignored by some Everest versions.
        }
    }

    private static string? ResolveMusicFile(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            Logger.Log(LogLevel.Warn, AutoMusicLastRoomModule.ModName, "No music file is configured.");
            return null;
        }

        configuredPath = configuredPath.Trim();

        if (Path.IsPathRooted(configuredPath) && File.Exists(configuredPath))
            return configuredPath;

        string[] candidates =
        {
            configuredPath,
            $"{AutoMusicLastRoomModule.ModName}/{configuredPath}",
        };

        foreach (string candidate in candidates)
        {
            if (Everest.Content.TryGet(candidate, out ModAsset asset, false))
            {
                string? extracted = ExtractAsset(asset, configuredPath);
                if (extracted != null)
                    return extracted;
            }
        }

        string localPath = Path.Combine(AppContext.BaseDirectory, "Mods", AutoMusicLastRoomModule.ModName, configuredPath);
        if (File.Exists(localPath))
            return localPath;

        Logger.Log(LogLevel.Warn, AutoMusicLastRoomModule.ModName,
            $"Music file '{configuredPath}' was not found in the mod content.");
        return null;
    }

    private static string? ExtractAsset(ModAsset asset, string configuredPath)
    {
        try
        {
            string? cachedPath = asset.GetCachedPath();
            if (!string.IsNullOrEmpty(cachedPath) && File.Exists(cachedPath))
                return cachedPath;

            string extension = Path.GetExtension(configuredPath);
            if (string.IsNullOrEmpty(extension))
                extension = ".ogg";

            string tempDir = Path.Combine(Path.GetTempPath(), "CelesteAutoMusicLastRoom");
            Directory.CreateDirectory(tempDir);

            string tempPath = Path.Combine(tempDir, "music" + extension);
            using (Stream source = asset.Stream)
            using (FileStream destination = File.Create(tempPath))
            {
                source.CopyTo(destination);
            }

            return tempPath;
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Warn, AutoMusicLastRoomModule.ModName,
                $"Could not extract music asset '{configuredPath}': {e.Message}");
            return null;
        }
    }
}
