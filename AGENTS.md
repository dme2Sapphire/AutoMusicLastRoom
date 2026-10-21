# AGENTS.md — AutoMusicLastRoom handoff

Last updated: 2026-10-21

## 1. Project snapshot

- Project: `C:\Users\Sapphire\Projects\gaoshua\celeste_auto_music_last_room`
- Type: Celeste Everest code mod, .NET 8.0
- Mod name: `AutoMusicLastRoom`
- Purpose: when the player reaches the map's main final room while carrying a one-life berry, automatically play configured music.
- Current version: `1.0.0` in `everest.yaml`
- Installed test copy: `C:\Program Files (x86)\Steam\steamapps\common\Celeste\Mods\AutoMusicLastRoom\`
- Full package: `AutoMusicLastRoom.zip`
- No-music package: `AutoMusicLastRoom-nomusic.zip`

## 2. Build / install / hot reload

Build:

```bash
cd C:\Users\Sapphire\Projects\gaoshua\celeste_auto_music_last_room
dotnet build Source/AutoMusicLastRoom.csproj -c Release
```

The csproj references local game assemblies:

- `Celeste.dll`
- `FNA.dll`

`CelestePrefix` defaults to:

```text
C:\Program Files (x86)\Steam\steamapps\common\Celeste
```

After build, copy to the installed mod:

```bash
cp bin/AutoMusicLastRoom.dll "C:/Program Files (x86)/Steam/steamapps/common/Celeste/Mods/AutoMusicLastRoom/bin/"
```

Also copy `everest.yaml` when dependencies change:

```bash
cp everest.yaml "C:/Program Files (x86)/Steam/steamapps/common/Celeste/Mods/AutoMusicLastRoom/"
```

Everest `CodeReload_WIP` is already enabled in the local game settings. Overwriting the DLL hot-reloads the mod after roughly 8–12 seconds and reloads the current level. Commands can be temporarily unavailable during that window.

## 3. Packaging

Full package:

```bash
rm -rf .pack .pack-nomusic AutoMusicLastRoom.zip AutoMusicLastRoom-nomusic.zip
mkdir -p .pack/bin .pack/Dialog .pack/Audio
cp everest.yaml .pack/
cp bin/AutoMusicLastRoom.dll .pack/bin/
cp Dialog/English.txt "Dialog/Simplified Chinese.txt" .pack/Dialog/
cp Audio/rain_love.ogg Audio/the_heart.ogg .pack/Audio/
powershell -NoProfile -Command "Compress-Archive -Path '.pack\*' -DestinationPath 'AutoMusicLastRoom.zip' -Force"
rm -rf .pack
```

No-music package:

```bash
mkdir -p .pack-nomusic/bin .pack-nomusic/Dialog .pack-nomusic/Audio
cp everest.yaml .pack-nomusic/
cp bin/AutoMusicLastRoom.dll .pack-nomusic/bin/
cp Dialog/English.txt "Dialog/Simplified Chinese.txt" .pack-nomusic/Dialog/
cp Audio/.gitkeep .pack-nomusic/Audio/
powershell -NoProfile -Command "Compress-Archive -Path '.pack-nomusic\*' -DestinationPath 'AutoMusicLastRoom-nomusic.zip' -Force"
rm -rf .pack-nomusic
```

Zip root must contain `everest.yaml`, `bin/`, `Dialog/`, `Audio/`.

Do not install the full zip, no-music zip, and the folder install at the same time. They share the same mod `Name`.

## 4. Local environment state

- Celeste: `C:\Program Files (x86)\Steam\steamapps\common\Celeste`
- Everest version seen in logs: `1.4.0.0-fna [Everest: 6458-azure-4bbde-stable]`
- `everest-launch.txt`: `--console` has been uncommented by us to open a separate log console window.
- In-game command console: `~` (`OemTilde`).
- DebugRC: `http://localhost:32270/`
  - Execute commands with `http://localhost:32270/console?command=...`
- CeleMod active profiles: `Base_QOL_Mods` + `Default`.
- `AutoMusicLastRoom` has been added to both profile JSONs:
  - `celemod_blacklist_profiles/Base_QOL_Mods.json`
  - `celemod_blacklist_profiles/Default.json`
- Afterimage was temporarily enabled for modded-map testing and then restored to disabled.
- Current `Saves/modsettings-AutoMusicLastRoom.celeste` includes:
  - `RequireBerry: false`
  - `TinyRoomAreaRatio: FiftyPercent` (0.5)
  - `MusicFile: Audio/rain_love.ogg`
- Audio files are in both:
  - project `Audio/`
  - installed `Mods/AutoMusicLastRoom/Audio/`
- Extracted OGGs:
  - `rain_love.ogg` (雨爱 DJ)
  - `the_heart.ogg` (The Heart)

## 5. Source layout

```text
Source/
  AutoMusicLastRoom.csproj
  AutoMusicLastRoomModule.cs
  AutoMusicLastRoomSettings.cs
  BerryDetector.cs
  CtEvents.cs
  DebugCommands.cs
  FmodMusic.cs
  GlobalUsings.cs
  LastRoomResolver.cs
  LobbyDetector.cs
  MusicController.cs
  MusicLibrary.cs
  RoomMonitor.cs
Dialog/
  English.txt
  Simplified Chinese.txt
Audio/
  rain_love.ogg
  the_heart.ogg
everest.yaml
AGENTS.md
```

## 6. Runtime architecture

### Module

`AutoMusicLastRoomModule : EverestModule`

Subscribes to:

- `Everest.Events.Level.OnLoadLevel`
- `Everest.Events.Level.OnAfterUpdate`
- `Everest.Events.Level.OnComplete`
- `Everest.Events.Level.OnExit`

Does **not** subscribe to player death anymore. Death is intentionally a no-op so that dying and respawning in the target room does not stop/restart music.

On load level:

- Stops old music if `AreaKey` changed (immediate, no fade).
- Attempts `CtEvents.TrySubscribe()`.
- Resumes playback state.
- If current map is a CollabUtils2 lobby/gym, stops music and skips preload.
- Otherwise preloads music.
- If CCT events are not subscribed, immediately calls `RoomMonitor.OnLevelLoaded(level)`.

`OnAfterUpdate` calls `RoomMonitor.Tick(level)`.

### RoomMonitor

Static state, no entity.

- Primary room-change source: CCT `OnChangedRoom` event via `CtEvents`.
- Fallback: every `OnAfterUpdate`, compare `level.Session.Level` with last room.
- Handles:
  - lobby/gym skip
  - target resolution
  - logging
  - berry/trigger checks
  - fade update
  - preload after fade-out
- `musicStarted` means this target visit already triggered music.
- If music is already playing and not fading out when entering target, it treats it as started (death no-op behavior).
- Leaving a non-target room:
  - Stops/fades music unless the new room is a completion room.
- Completion rooms keep music playing.
- `RoomMonitor` state is static across room loads; reset on area/room changes.

### CtEvents

- Optional integration with ConsistencyTracker.
- Reflects `Celeste.Mod.ConsistencyTracker.Events.Events`.
- Subscribes to `OnChangedRoom` (`void (string roomName, bool isPreviousRoom)`).
- Handler logs at **Debug** level and calls `RoomMonitor.OnCtRoomChanged()`.
- Subscription success logs at Info.
- Unsubscribes on mod unload.
- CCT is an optional dependency; do not hard-reference it.

### LastRoomResolver

Responsible for target inference.

Public methods:

- `Resolve(Level level) -> TargetRoom?`
- `IsCompletionRoom(Level level, string roomName) -> bool`

TargetRoom includes:

- source
- debug room name
- matching room names
- area/previous area
- CCT path availability and CCT last/previous room

#### Target inference priority

1. **ConsistencyTracker path** (if enabled and available)
   - Use selected path segment.
   - Ignore `IsNonGameplayRoom` and ignored rooms.
   - Take last gameplay room; support grouped rooms.
   - Apply completion-room and small-room heuristics.
2. **No CCT: strong completion markers**
   - `eventTrigger` whose `event` value:
     - starts with `end`
     - contains `_end`
     - contains `complete`, `credit`, or `final`
   - Entity/trigger names containing:
     - `levelexit`
     - `level_exit`
     - `areaexit`
     - `completearea` (Everest `completeAreaTrigger`)
     - `theend`
3. **Weak completion markers**
   - heart/gem markers
   - no bonfire/campfire anymore
4. **No completion marker**
   - farthest reachable room from start via BFS

#### Room graph

- `LevelData.Bounds` rectangles.
- Adjacency if rooms touch or are within 4 tiles and overlap on the perpendicular axis.
- BFS from `map.StartLevel()` to compute distances and parents.

#### Predecessor selection

- If BFS parent exists, use it.
- Else among adjacent rooms:
  - prefer smaller distance from start (closer to initial room)
  - then smaller bounding-box distance to the completion room
  - then larger area
- Last resort: nearest room by rectangle distance, then smaller start distance, then larger area.

This is intentional: maps sometimes put easter eggs/credits after the settlement room.

#### Completion/cutscene/tiny heuristics

`IsCompletionRoom` returns true only for strong completion markers.

`IsCutsceneCompletionRoom`:

- must have strong marker or heart marker
- `level.Entities.Count <= 8`
- triggers are intentionally excluded from the count because light/music/camera triggers inflated settlement rooms

`IsTiny`:

```text
candidateArea < previousArea * ratio
```

Ratio options:

```text
0.01, 0.1, 0.2, 0.3, 0.5, 0.75
```

Default and current saved value: **0.5**.

If the completion room is a cutscene or tiny, use the predecessor.

#### Heart detection

Real heart markers include:

- `HasHeartGem`
- `HasGem`
- entity/trigger names containing `heartgem`
- `crystalheart`
- `heart`, with exclusions

Excluded names:

- `door`
- `gate`
- `barrier`
- `fake`

Important:

- `MiniHeart` is considered **real**.
- `FakeMiniHeart` is excluded because it contains `fake`.
- `FakeHeart` / `CustomFakeHeart` are excluded.
- `ReskinnableCrystalHeart` is recognized as real.

Known example:

- SJ Grandmaster HeartSide `ZZ-HeartSide`:
  - `d2_08-Aiden` has `CustomFakeHeart`; it must not become the completion room.
  - Real completion room is `gg_Heart` (`ReskinnableCrystalHeart`).
  - With ratio 0.5, target becomes `f3_01-Hydro`.

#### Lobby/Gym

`LobbyDetector`:

- Reflects `Celeste.Mod.CollabUtils2.LobbyHelper`:
  - `IsCollabLobby(string sid)`
  - `IsCollabGym(string sid)`
- If either is true, skip preload/target/play.
- Caches by SID.
- Fallback when CCT/CU2 unavailable: scan map rooms for:
  - `chapterpanel`
  - `lobbymapwarp`
  - `lobbymapcontroller`
  - `lobbywarppoint`
- `everest.yaml` includes optional `CollabUtils2 1.0.0`.

Known examples:

- `StrawberryJam2021/0-Lobbies/1-Beginner` skips.
- `StrawberryJam2021/0-Gyms/1-Beginner` skips.

### BerryDetector

Checks `Player.Leader.Followers`:

- `Strawberry.Golden && !Winged`
- type name exactly in configured one-life berry types
- defaults: `SilverBerry`, `PlatinumBerry`
- ignores `SpeedBerry`
- extra type names via hidden setting `ExtraOneLifeBerryTypes`

Special levels can detach/reattach berries; preload was moved to level entry so that does not cause repeated preload.

### MusicController / FmodMusic

Primary playback uses Celeste's own FMOD low-level system:

- `Audio.System.getLowLevelSystem()`
- `createSound(path, FMOD.MODE.CREATESTREAM)`
- `playSound(..., paused: true)`
- set channel mode/volume
- unpause

Preload happens on level entry if the map is not a lobby/gym. The sound object is created before the target is reached.

Fade behavior:

- Fade in starts at `min(0.3, targetVolume)`.
- Fade in duration: 0.8 s.
- Fade out duration: 0.6 s.
- Volume target: `settings.Volume * Audio.MusicVolume`.
- Fades are driven by `MusicController.Update(Engine.DeltaTime)` from `RoomMonitor.Tick`.
- Fade-out applies to room leaving, manual stop, and other normal stops.
- Level complete/exit/unload use immediate stop (`fadeOut: false`).
- Fade does **not** apply to `MusicEvent` (custom FMOD bank mode).
- If fade-out completes, `RoomMonitor.Tick` can preload again.
- MediaPlayer is a fallback if FMOD preload/play fails.
- OGG is the primary supported file format.

Important states:

- `MusicController.IsPlaying` stays true during fade-out.
- `MusicController.IsFadingOut` distinguishes fade-out from active playback.
- Entering target while playing and not fading out is a no-op (death/respawn same room).

Event mode:

- If `MusicEvent` is configured, `Audio.SetMusic` is used.
- No fade support for event mode.
- `MusicEvent` is hidden from the menu; edit settings file if needed.

### MusicLibrary

Scans for `.ogg` and `.qoa` in:

- physical `Mods/AutoMusicLastRoom/Audio`
- mod content map keys under `Audio/`

Music menu uses a slider of detected tracks. If no files, it shows a disabled “no files” message.

### Settings UI

`AutoMusicLastRoomSettings` uses Everest's public property auto-generation plus custom entry methods:

- `CreateMusicFileEntry`
- `CreateVolumeEntry`
- `CreateAdvancedOptionsEntry`

Main menu:

- Enable auto music
- Only play while carrying a one-life berry
- Music selector
- Volume
- Loop
- Advanced

Advanced submenu:

- Use Consistency Tracker path
- Small-room heuristic exclusion
- Small-room area ratio (disabled when heuristic off)
- Stop when leaving target room
- Log target selection

Hidden/config-only settings:

- `ExtraOneLifeBerryTypes`
- `MusicEvent`

Do not add `[Setting]` attributes in this Everest version; public properties are auto-detected. Use `[SettingIgnore]` to hide.

### Debug commands

```text
automusic_test
automusic_stop
automusic_state
automusic_target
automusic_levels
automusic_room
automusic_requireberry_on
automusic_requireberry_off
```

Useful console commands:

```text
switch_slot -1          # debug save
load <SID> [room]       # load map/room
give_golden             # gives golden berry
automusic_state         # current room, lobby, target, preload, playing
automusic_target        # target inference details
automusic_levels        # list heart/gem markers per room
automusic_room          # current room entities/triggers
```

The in-game console opens with `~`.

## 7. Tested cases

- Forsaken City 1A:
  - CT target `12a`.
  - `end` is a completion room via `eventTrigger(event=end_city)`.
  - Entering `end` keeps music playing.
  - Entering a normal room fades out.
  - Same-room reload behaves as no-op.
- 1oC (`tabitha/1overC/1oC`):
  - `H2C2O4_05` is a settlement room.
  - It contains `everest/completeAreaTrigger`.
  - Target is now `H2C2O4_04`.
  - `H2C2O4_strawberry` is not chosen as final.
- Strawberry Jam Beginner `Bing_Over_Google`:
  - Target `09- Fin` at ratio 0.1.
  - At ratio 0.5, target becomes `08- U Turn`.
- SJ HeartSide `ZZ-HeartSide`:
  - Target `f3_01-Hydro`.
  - Fake heart in `d2_08-Aiden` must be ignored.
  - Real heart is `gg_Heart`.
- SJ lobbies and gyms:
  - Both skipped.
- Afterimage `simonius/mtc2024/mtc2simon`:
  - CT target `ENDER`.
  - ENDER is a real final challenge, not a settlement room.
- Silver berry, golden berry, no-berry, RequireBerry toggle, completion stop, fade-out, and CCT event subscription have all been tested.

## 8. Current known limitations / possible next work

- No CT path means heuristic is best-effort.
- There is no per-map manual target override yet.
- Heart detection is name-based for custom hearts; fake/mini variants are handled by name heuristics.
- `IsCutsceneCompletionRoom` uses entity count only; unusual maps may need tuning.
- Fade/loop is not implemented for `MusicEvent` bank mode.
- Optional dependencies use major-version matching:
  - ConsistencyTracker 2.x
  - CollabUtils2 1.x
- If CU2 is absent, lobby/gym fallback uses room marker names.
- No automatic test suite; testing is in-game via DebugRC/debug save.
- Current game logs can be large; use `log.txt` and the separate `--console` window.

## 9. Do / don't

- Use the debug save (`switch_slot -1`) or a new save for `give_golden` tests. Do not accidentally add golden berries to a real save.
- Don’t run `give_golden` in the user’s main save.
- Don’t install the folder version and both zips simultaneously.
- After code changes, rebuild, copy the DLL, and repackage both zips.
- When changing settings defaults, update the saved local settings file if the user expects the new default immediately.
- Keep CCT event logs at Debug; keep per-level/room inference and playback logs at Info.
- The old `LastRoomWatcher` entity design was removed; do not reintroduce an entity for room monitoring. Use CCT `OnChangedRoom` + static `RoomMonitor.Tick`.

## 10. File map details

- `AutoMusicLastRoomModule.cs` — module lifecycle and Everest events.
- `RoomMonitor.cs` — static room state, target checks, playback decisions, logging, fade update trigger.
- `CtEvents.cs` — optional CCT event reflection/subscription.
- `LastRoomResolver.cs` — target inference, graph, completion/heart detection.
- `LobbyDetector.cs` — CU2 lobby/gym detection.
- `BerryDetector.cs` — one-life berry follower detection.
- `MusicController.cs` — FMOD preload/play, MediaPlayer fallback, fade.
- `FmodMusic.cs` — FMOD core create/play/stop wrappers.
- `MusicLibrary.cs` — audio file discovery.
- `AutoMusicLastRoomSettings.cs` — settings, custom menu entries, ratio enum.
- `DebugCommands.cs` — in-game diagnostics.

## 11. Git / GitHub status

- Local repository initialized with `git init -b main`.
- Source files are staged, but **no commit has been made yet** because `user.name` and `user.email` are unset.
- `.gitignore` excludes:
  - `bin/`, `Source/bin/`, `Source/obj/`
  - `.pack/`, `.pack-nomusic/`, `.research/`, `*.zip`
  - `*.mp4`
  - `Audio/*.ogg`, `Audio/*.qoa`
  - editor/OS files
- `Audio/.gitkeep` is included so the directory exists in the repo.
- The two mp4 files, both OGGs, both zips, and build outputs are intentionally not staged.
- Recommended remote setup:
  ```bash
  git config --global user.name "GitHub display name"
  git config --global user.email "GitHub email or noreply email"
  git commit -m "Initial commit: AutoMusicLastRoom"
  git remote add origin https://github.com/<USER>/<REPO>.git
  git push -u origin main
  ```
- If the repo was created with a README on GitHub, pull/rebase before pushing:
  ```bash
  git pull --rebase origin main
  git push -u origin main
  ```
- Full BGM zip is for local/private use only; for public releases attach the no-music zip and let users add their own OGG.

