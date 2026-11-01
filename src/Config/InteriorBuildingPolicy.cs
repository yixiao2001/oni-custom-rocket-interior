using CustomRocketInterior.Options;

namespace CustomRocketInterior.Config
{
    /// <summary>Startup settings for building interactions, including existing saved interiors.</summary>
    internal static class InteriorBuildingPolicy
    {
        public static bool AllowWallDeconstruction { get; private set; } = true;
        public static bool AllowPortsOnAnySolidTile { get; private set; } = true;

        public static void Apply(RocketInteriorOptions options)
        {
            if (options == null) return;
            AllowWallDeconstruction = options.AllowWallDeconstruction;
            AllowPortsOnAnySolidTile = options.AllowPortsOnAnySolidTile;
        }

        // Explicitly include only hull/window tiles. Fixed gas/liquid ports, doors,
        // the control station and player-built utilities are separate prefabs.
        public static bool IsShellWall(string prefabID) =>
            prefabID == "RocketWallTile" || prefabID == "RocketEnvelopeWindowTile";

        public static bool CanDeconstructWall(string prefabID, bool isModuleInterior) =>
            AllowWallDeconstruction && isModuleInterior && IsShellWall(prefabID);

        public static bool RelaxMounting(bool isModuleInterior, bool isRocketEnvelopeRule, bool requiresHullTag) =>
            AllowPortsOnAnySolidTile && isModuleInterior && isRocketEnvelopeRule && requiresHullTag;
    }
}
