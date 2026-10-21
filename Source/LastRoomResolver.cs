using System.Collections;

namespace Celeste.Mod.AutoMusicLastRoom;

internal sealed class TargetRoom
{
    public required string Source { get; init; }
    public required string DebugName { get; init; }
    public required HashSet<string> RoomNames { get; init; }
    public required float Area { get; init; }
    public required float PreviousArea { get; init; }
    public required string? PreviousName { get; init; }
    public bool CtPathAvailable { get; init; }
    public string? CtLastRoom { get; init; }
    public string? CtPreviousRoom { get; init; }

    public string LogKey =>
        $"{Source}|{DebugName}|{Area:0}|{PreviousName}|{PreviousArea:0}|CT={CtPathAvailable}:{CtLastRoom}";

    public string Description =>
        $"{DebugName} (source={Source}, area={Area:0}, previous={PreviousName ?? "-"}, previousArea={PreviousArea:0}, " +
        $"CCT path={(CtPathAvailable ? "yes" : "no")}, CCT last={CtLastRoom ?? "-"}, CCT prev={CtPreviousRoom ?? "-"})";

    public bool Matches(string roomName) => RoomNames.Contains(roomName);
}

internal static class LastRoomResolver
{
    private const string CtModuleTypeName = "Celeste.Mod.ConsistencyTracker.ConsistencyTrackerModule";

    public static TargetRoom? Resolve(Level level)
    {
        MapData? map = level.Session.MapData;
        if (map == null)
            return null;

        var settings = AutoMusicLastRoomModule.Settings;
        float tinyRoomRatio = settings.EnableTinyRoomHeuristic ? settings.GetTinyRoomAreaRatio() : 0f;

        if (settings.UseConsistencyTrackerPath)
        {
            TargetRoom? fromCt = TryResolveFromConsistencyTracker(map, tinyRoomRatio);
            if (fromCt != null)
                return fromCt;
        }

        return ResolveFromMap(map, tinyRoomRatio);
    }

    private static TargetRoom? TryResolveFromConsistencyTracker(MapData map, float ratio)
    {
        try
        {
            Type? moduleType = FindCtModuleType();
            if (moduleType == null)
                return null;

            object? module = GetStaticMember(moduleType, "Instance");
            object? segmentList = GetProperty(module, "CurrentChapterPathSegmentList");
            object? path = GetProperty(segmentList, "CurrentPath");
            if (path == null)
                return null;

            HashSet<string> ignored = GetStringEnumerable(GetProperty(path, "IgnoredRooms"))
                .SelectMany(name => new[] { name, StripUid(name) })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var rooms = new List<RoomChoice>();
            foreach (object checkpoint in EnumerableProperty(path, "Checkpoints"))
            {
                foreach (object room in EnumerableProperty(checkpoint, "Rooms"))
                {
                    if (GetProperty(room, "IsNonGameplayRoom") is true)
                        continue;

                    if (GetProperty(room, "DebugRoomName") is not string debugRoomName ||
                        string.IsNullOrWhiteSpace(debugRoomName))
                        continue;

                    string actualName = GetProperty(room, "ActualDebugRoomName") as string
                                        ?? StripUid(debugRoomName);

                    if (ignored.Contains(debugRoomName) || ignored.Contains(actualName))
                        continue;

                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { actualName };
                    foreach (string grouped in GetStringEnumerable(GetProperty(room, "GroupedRooms")))
                        names.Add(StripUid(grouped));

                    rooms.Add(new RoomChoice(debugRoomName, actualName, names));
                }
            }

            if (rooms.Count == 0)
                return null;

            RoomChoice last = rooms[^1];
            RoomChoice? previous = rooms.Count > 1 ? rooms[^2] : null;
            RoomChoice chosen = ApplyTinyRoomHeuristic(last, previous, map, ratio);

            return BuildTarget(
                chosen, last, previous, map, "ConsistencyTracker",
                ctPathAvailable: true,
                ctLastRoom: last.DebugRoomName,
                ctPreviousRoom: previous?.DebugRoomName);
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Warn, AutoMusicLastRoomModule.ModName,
                $"ConsistencyTracker path lookup failed, falling back to map heuristics: {e.Message}");
            return null;
        }
    }

    private static TargetRoom? ResolveFromMap(MapData map, float ratio)
    {
        var levels = map.Levels
            .Where(level => level != null && !level.Dummy)
            .ToList();

        if (levels.Count == 0)
            return null;

        Dictionary<LevelData, List<LevelData>> adjacency = BuildAdjacency(levels);
        LevelData start = map.StartLevel();
        if (start == null || !levels.Contains(start))
            start = levels[0];

        (Dictionary<LevelData, int> distances, Dictionary<LevelData, LevelData> parents) = Bfs(start, adjacency);

        (LevelData Room, string Source)? completion = FindCompletionRoom(levels, distances);
        if (completion != null)
        {
            LevelData completionRoom = completion.Value.Room;
            LevelData? previous = FindPredecessor(completionRoom, start, levels, adjacency, distances, parents);
            bool cutscene = IsCutsceneCompletionRoom(completionRoom);
            bool usePrevious = previous != null && (cutscene || IsTiny(completionRoom, previous, ratio));
            LevelData chosen = usePrevious ? previous! : completionRoom;
            string source = completion.Value.Source + (usePrevious ? "(penultimate)" : "");
            return CreateTarget(chosen, previous, source);
        }

        LevelData candidate = levels
            .Where(room => !ReferenceEquals(room, start))
            .OrderByDescending(room => distances.TryGetValue(room, out int distance) ? distance : -1)
            .ThenByDescending(AreaOf)
            .FirstOrDefault() ?? levels[^1];

        LevelData? fallbackPrevious = FindPredecessor(candidate, start, levels, adjacency, distances, parents);
        bool fallbackUsePrevious = fallbackPrevious != null && IsTiny(candidate, fallbackPrevious, ratio);
        LevelData fallbackChosen = fallbackUsePrevious ? fallbackPrevious! : candidate;
        string fallbackSource = "LastRoom" + (fallbackUsePrevious ? "(penultimate)" : "");
        return CreateTarget(fallbackChosen, fallbackPrevious, fallbackSource);
    }

    private static (LevelData Room, string Source)? FindCompletionRoom(
        List<LevelData> levels,
        Dictionary<LevelData, int> distances)
    {
        List<LevelData> strong = levels.Where(HasStrongCompletionMarker).ToList();
        if (strong.Count > 0)
            return (ChooseFarthest(strong, distances), "Completion");

        List<LevelData> hearts = levels.Where(HasHeartMarker).ToList();
        if (hearts.Count > 0)
            return (ChooseFarthest(hearts, distances), "HeartGem");

        return null;
    }

    private static LevelData ChooseFarthest(List<LevelData> rooms, Dictionary<LevelData, int> distances)
    {
        return rooms
            .OrderByDescending(room => distances.TryGetValue(room, out int distance) ? distance : -1)
            .First();
    }

    private static LevelData? FindPredecessor(
        LevelData target,
        LevelData start,
        List<LevelData> levels,
        Dictionary<LevelData, List<LevelData>> adjacency,
        Dictionary<LevelData, int> distances,
        Dictionary<LevelData, LevelData> parents)
    {
        if (!ReferenceEquals(target, start) && parents.TryGetValue(target, out LevelData? parent))
            return parent;

        if (adjacency.TryGetValue(target, out List<LevelData>? neighbors))
        {
            LevelData? adjacent = neighbors
                .Where(neighbor => !ReferenceEquals(neighbor, target))
                .OrderBy(neighbor => distances.TryGetValue(neighbor, out int distance) && distance >= 0
                    ? distance
                    : int.MaxValue)
                .ThenBy(neighbor => RectDistance(target.Bounds, neighbor.Bounds))
                .ThenByDescending(AreaOf)
                .FirstOrDefault();

            if (adjacent != null)
                return adjacent;
        }

        return FindNearestRoom(target, levels, distances);
    }

    private static LevelData? FindNearestRoom(
        LevelData target,
        List<LevelData> levels,
        Dictionary<LevelData, int> distances)
    {
        return levels
            .Where(level => !ReferenceEquals(level, target))
            .Select(level => new
            {
                Level = level,
                BoundsDistance = RectDistance(target.Bounds, level.Bounds),
                StartDistance = distances.TryGetValue(level, out int distance) && distance >= 0
                    ? distance
                    : int.MaxValue,
                Area = AreaOf(level),
            })
            .OrderBy(candidate => candidate.BoundsDistance)
            .ThenBy(candidate => candidate.StartDistance)
            .ThenByDescending(candidate => candidate.Area)
            .Select(candidate => candidate.Level)
            .FirstOrDefault();
    }

    private static int RectDistance(Rectangle a, Rectangle b)
    {
        int dx = Math.Max(0, Math.Max(a.Left - b.Right, b.Left - a.Right));
        int dy = Math.Max(0, Math.Max(a.Top - b.Bottom, b.Top - a.Bottom));
        return dx + dy;
    }

    private static Dictionary<LevelData, List<LevelData>> BuildAdjacency(List<LevelData> levels)
    {
        var adjacency = levels.ToDictionary(level => level, _ => new List<LevelData>());

        for (int i = 0; i < levels.Count; i++)
        {
            for (int j = i + 1; j < levels.Count; j++)
            {
                if (!Touches(levels[i].Bounds, levels[j].Bounds))
                    continue;

                adjacency[levels[i]].Add(levels[j]);
                adjacency[levels[j]].Add(levels[i]);
            }
        }

        return adjacency;
    }

    private static bool Touches(Rectangle a, Rectangle b)
    {
        const int maxGap = 4;

        bool verticalOverlap = a.Top < b.Bottom && b.Top < a.Bottom;
        bool horizontalOverlap = a.Left < b.Right && b.Left < a.Right;

        int gapX = Math.Max(0, Math.Max(a.Left - b.Right, b.Left - a.Right));
        int gapY = Math.Max(0, Math.Max(a.Top - b.Bottom, b.Top - a.Bottom));

        return (gapX <= maxGap && verticalOverlap) || (gapY <= maxGap && horizontalOverlap);
    }

    private static (Dictionary<LevelData, int> Distances, Dictionary<LevelData, LevelData> Parents) Bfs(
        LevelData start,
        Dictionary<LevelData, List<LevelData>> adjacency)
    {
        var distances = new Dictionary<LevelData, int> { [start] = 0 };
        var parents = new Dictionary<LevelData, LevelData>();
        var queue = new Queue<LevelData>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            LevelData current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out List<LevelData>? neighbors))
                continue;

            foreach (LevelData neighbor in neighbors)
            {
                if (distances.ContainsKey(neighbor))
                    continue;

                distances[neighbor] = distances[current] + 1;
                parents[neighbor] = current;
                queue.Enqueue(neighbor);
            }
        }

        return (distances, parents);
    }

    private static TargetRoom CreateTarget(LevelData chosen, LevelData? previous, string source)
    {
        return new TargetRoom
        {
            Source = source,
            DebugName = chosen.Name,
            RoomNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { chosen.Name },
            Area = AreaOf(chosen),
            PreviousArea = AreaOf(previous),
            PreviousName = previous?.Name,
        };
    }

    private static RoomChoice ApplyTinyRoomHeuristic(RoomChoice last, RoomChoice? previous, MapData map, float ratio)
    {
        if (previous == null)
            return last;

        LevelData? lastData = FindLevelData(last, map);
        LevelData? previousData = FindLevelData(previous, map);

        if (lastData != null && previousData != null &&
            (IsCutsceneCompletionRoom(lastData) || IsTiny(lastData, previousData, ratio)))
            return previous;

        float lastArea = AreaOf(last, map);
        float previousArea = AreaOf(previous, map);

        if (lastArea > 0 && previousArea > 0 && lastArea < previousArea * ratio)
            return previous;

        return last;
    }

    private static LevelData? FindLevelData(RoomChoice room, MapData map)
    {
        foreach (string name in room.Names)
        {
            LevelData? level = map.Get(name);
            if (level != null)
                return level;
        }

        return map.Get(room.DebugRoomName);
    }

    private static bool IsTiny(LevelData candidate, LevelData previous, float ratio)
    {
        float candidateArea = AreaOf(candidate);
        float previousArea = AreaOf(previous);
        return candidateArea > 0 && previousArea > 0 && candidateArea < previousArea * ratio;
    }

    private static bool IsCutsceneCompletionRoom(LevelData level)
    {
        if (!HasStrongCompletionMarker(level) && !HasHeartMarker(level))
            return false;

        return level.Entities.Count <= 8;
    }

    public static bool IsCompletionRoom(Level level, string roomName)
    {
        LevelData? data = level.Session.MapData?.Get(roomName);
        if (data == null)
            return false;

        return HasStrongCompletionMarker(data) || HasHeartMarker(data);
    }

    private static bool HasStrongCompletionMarker(LevelData level)
    {
        foreach (EntityData entity in level.Entities.Concat(level.Triggers))
        {
            string name = entity.Name ?? "";

            if (name.Contains("levelexit", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("level_exit", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("areaexit", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("completearea", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("theend", StringComparison.OrdinalIgnoreCase))
                return true;

            if (name.Contains("eventtrigger", StringComparison.OrdinalIgnoreCase) && IsCompletionEvent(entity))
                return true;
        }

        return false;
    }

    private static bool IsCompletionEvent(EntityData entity)
    {
        if (entity.Values == null || !entity.Values.TryGetValue("event", out object? value))
            return false;

        string eventName = value as string ?? "";
        return eventName.StartsWith("end", StringComparison.OrdinalIgnoreCase) ||
               eventName.Contains("_end", StringComparison.OrdinalIgnoreCase) ||
               eventName.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
               eventName.Contains("credit", StringComparison.OrdinalIgnoreCase) ||
               eventName.Contains("final", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasHeartMarker(LevelData level)
    {
        if (level.HasHeartGem || level.HasGem)
            return true;

        foreach (EntityData entity in level.Entities.Concat(level.Triggers))
        {
            if (IsHeartEntityName(entity.Name))
                return true;
        }

        return false;
    }

    private static TargetRoom BuildTarget(
        RoomChoice chosen,
        RoomChoice last,
        RoomChoice? previous,
        MapData map,
        string baseSource,
        bool ctPathAvailable = false,
        string? ctLastRoom = null,
        string? ctPreviousRoom = null)
    {
        float chosenArea = AreaOf(chosen, map);
        float previousArea = AreaOf(previous, map);
        string source = ReferenceEquals(chosen, last) ? baseSource : $"{baseSource}(penultimate)";

        return new TargetRoom
        {
            Source = source,
            DebugName = chosen.DebugRoomName,
            RoomNames = chosen.Names,
            Area = chosenArea,
            PreviousArea = previousArea,
            PreviousName = previous?.DebugRoomName,
            CtPathAvailable = ctPathAvailable,
            CtLastRoom = ctLastRoom,
            CtPreviousRoom = ctPreviousRoom,
        };
    }

    private static float AreaOf(RoomChoice? room, MapData map)
    {
        if (room == null)
            return 0;

        foreach (string name in room.Names)
        {
            float area = AreaOf(map.Get(name));
            if (area > 0)
                return area;
        }

        return AreaOf(map.Get(room.DebugRoomName));
    }

    private static float AreaOf(LevelData? level)
    {
        if (level == null)
            return 0;

        return level.Bounds.Width * (float)level.Bounds.Height;
    }

    private static bool IsHeartEntityName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        if (name.Contains("door", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("gate", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("barrier", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("fake", StringComparison.OrdinalIgnoreCase))
            return false;

        return name.Contains("heartgem", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("crystalheart", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("heart", StringComparison.OrdinalIgnoreCase);
    }

    private static Type? FindCtModuleType()
    {
        Type? type = Type.GetType($"{CtModuleTypeName}, ConsistencyTracker");
        if (type != null)
            return type;

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string? assemblyName = assembly.GetName().Name;
            if (!string.Equals(assemblyName, "ConsistencyTracker", StringComparison.OrdinalIgnoreCase))
                continue;

            type = assembly.GetType(CtModuleTypeName);
            if (type != null)
                return type;
        }

        return null;
    }

    private static object? GetStaticMember(Type type, string name)
    {
        return type.GetField(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
               ?? type.GetProperty(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
    }

    private static object? GetProperty(object? instance, string name)
    {
        return instance?.GetType()
            .GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(instance);
    }

    private static IEnumerable<object> EnumerableProperty(object? instance, string name)
    {
        return GetProperty(instance, name) is IEnumerable enumerable
            ? enumerable.Cast<object>()
            : Enumerable.Empty<object>();
    }

    private static IEnumerable<string> GetStringEnumerable(object? value)
    {
        return value is IEnumerable enumerable
            ? enumerable.Cast<object>().OfType<string>()
            : Enumerable.Empty<string>();
    }

    private static string StripUid(string roomName)
    {
        int separator = roomName.LastIndexOf(':');
        return separator >= 0 && separator < roomName.Length - 1
            ? roomName[(separator + 1)..]
            : roomName;
    }

    private sealed record RoomChoice(string DebugRoomName, string ActualName, HashSet<string> Names);
}
