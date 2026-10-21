namespace Celeste.Mod.AutoMusicLastRoom;

internal static class BerryDetector
{
    public static bool IsHoldingOneLifeBerry(Player? player)
    {
        var followers = player?.Leader?.Followers;
        if (followers == null)
            return false;

        foreach (var follower in followers)
        {
            var entity = follower?.Entity;
            if (entity == null)
                continue;

            string typeName = entity.GetType().Name;
            if (typeName.Equals("SpeedBerry", StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsConfiguredOneLifeType(typeName))
                return true;

            if (entity is Strawberry berry && berry.Golden && !berry.Winged)
                return true;
        }

        return false;
    }

    private static bool IsConfiguredOneLifeType(string typeName)
    {
        string configured = AutoMusicLastRoomModule.Settings.ExtraOneLifeBerryTypes;
        if (string.IsNullOrWhiteSpace(configured))
            return false;

        foreach (string part in configured.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (typeName.Equals(part.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
