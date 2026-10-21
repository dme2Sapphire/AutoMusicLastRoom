namespace Celeste.Mod.AutoMusicLastRoom;

internal static class CtEvents
{
    private const string EventsTypeName = "Celeste.Mod.ConsistencyTracker.Events.Events";

    private static EventInfo? changedRoomEvent;
    private static Delegate? changedRoomHandler;
    private static bool attempted;
    private static bool subscribed;

    public static bool IsSubscribed => subscribed;

    public static bool TrySubscribe()
    {
        if (subscribed)
            return true;

        if (attempted)
            return false;

        try
        {
            Type? eventsType = Type.GetType($"{EventsTypeName}, ConsistencyTracker");
            if (eventsType == null)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!string.Equals(assembly.GetName().Name, "ConsistencyTracker", StringComparison.OrdinalIgnoreCase))
                        continue;

                    eventsType = assembly.GetType(EventsTypeName);
                    if (eventsType != null)
                        break;
                }
            }

            if (eventsType == null)
                return false;

            EventInfo? ev = eventsType.GetEvent("OnChangedRoom", BindingFlags.Public | BindingFlags.Static);
            MethodInfo? handlerMethod = typeof(CtEvents).GetMethod(
                nameof(OnChangedRoom), BindingFlags.NonPublic | BindingFlags.Static);

            if (ev?.EventHandlerType == null || handlerMethod == null)
            {
                attempted = true;
                return false;
            }

            Delegate handler = Delegate.CreateDelegate(ev.EventHandlerType, handlerMethod);
            ev.AddEventHandler(null, handler);

            changedRoomEvent = ev;
            changedRoomHandler = handler;
            subscribed = true;

            Logger.Log(LogLevel.Info, AutoMusicLastRoomModule.ModName,
                "Subscribed to ConsistencyTracker's OnChangedRoom event.");
            return true;
        }
        catch (Exception e)
        {
            attempted = true;
            Logger.Log(LogLevel.Warn, AutoMusicLastRoomModule.ModName,
                $"Could not subscribe to ConsistencyTracker room events: {e.Message}");
            return false;
        }
    }

    public static void Unsubscribe()
    {
        if (changedRoomEvent != null && changedRoomHandler != null)
        {
            try
            {
                changedRoomEvent.RemoveEventHandler(null, changedRoomHandler);
            }
            catch (Exception)
            {
                // The tracker may already be unloading.
            }
        }

        changedRoomEvent = null;
        changedRoomHandler = null;
        subscribed = false;
        attempted = false;
    }

    private static void OnChangedRoom(string roomName, bool isPreviousRoom)
    {
        if (AutoMusicLastRoomModule.Settings.LogTargetSelection)
            Logger.Log(LogLevel.Debug, AutoMusicLastRoomModule.ModName,
                $"CCT OnChangedRoom event: room='{roomName}', isPreviousRoom={isPreviousRoom}");

        RoomMonitor.OnCtRoomChanged();
    }
}
