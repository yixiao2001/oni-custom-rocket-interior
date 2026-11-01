using System.Collections.Generic;
using System.Reflection;
using CustomRocketInterior.Config;
using HarmonyLib;

namespace CustomRocketInterior.Patches
{
    [HarmonyPatch]
    internal static class InteriorWallReachabilityPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(RocketWallTileConfig), "CreateBuildingDef");
            yield return AccessTools.Method(typeof(RocketEnvelopeWindowTileConfig), "CreateBuildingDef");
        }

        private static void Postfix(BuildingDef __result)
        {
            // Vanilla hull tiles exclude work from directly below, which prevents
            // reaching the ceiling from inside. Use normal tile reachability.
            if (__result != null && InteriorBuildingPolicy.AllowWallDeconstruction)
                __result.ConstructionOffsetFilter = null;
        }
    }

    [HarmonyPatch(typeof(Deconstructable), "OnSpawn")]
    internal static class InteriorWallDeconstructionPatch
    {
        private static void Postfix(Deconstructable __instance)
        {
            string id = __instance.GetComponent<KPrefabID>()?.PrefabTag.Name;
            if (!InteriorBuildingPolicy.IsShellWall(id)) return;

            WorldContainer world = __instance.GetMyWorld();
            ApplyToWall(__instance, world != null && world.IsModuleInterior);
        }

        internal static void ApplyToWall(Deconstructable building, bool isModuleInterior)
        {
            string id = building.GetComponent<KPrefabID>()?.PrefabTag.Name;
            if (InteriorBuildingPolicy.IsShellWall(id))
                building.SetAllowDeconstruction(InteriorBuildingPolicy.CanDeconstructWall(id, isModuleInterior));
        }
    }

    [HarmonyPatch(typeof(BuildingDef), nameof(BuildingDef.CheckFoundation))]
    internal static class InteriorUtilityFoundationPatch
    {
        private static bool Prefix(int cell, Orientation orientation, BuildLocationRule location_rule,
            int width, Tag optionalFoundationRequiredTag, ref bool __result)
        {
            if (!InteriorBuildingPolicy.AllowPortsOnAnySolidTile
                || location_rule != BuildLocationRule.OnRocketEnvelope
                || (optionalFoundationRequiredTag.IsValid && optionalFoundationRequiredTag != GameTags.RocketEnvelopeTile)
                || !Grid.IsWorldValidCell(cell)) return true;

            WorldContainer world = ClusterManager.Instance?.GetWorld(Grid.WorldIdx[cell]);
            if (!InteriorBuildingPolicy.RelaxMounting(world != null && world.IsModuleInterior, true, true))
                return true;

            __result = HasBuiltSolidSupport(cell, orientation, width);
            return false;
        }

        internal static bool IsBuiltSolidTile(UnityEngine.GameObject tile)
        {
            return tile != null && tile.HasTag(GameTags.FloorTiles)
                && tile.GetComponent<SimCellOccupier>()?.doReplaceElement == true;
        }

        private static bool HasBuiltSolidSupport(int cell, Orientation orientation, int width)
        {
            // Same supporting side/rotation as vanilla OnRocketEnvelope. Mesh and
            // airflow tiles also set Grid.Solid for walking, so check the actual
            // solid-tile occupier as well; they cannot serve as sealed hull supports.
            for (int x = -(width - 1) / 2; x <= width / 2; x++)
            {
                CellOffset offset = Rotatable.GetRotatedCellOffset(new CellOffset(x, -1), orientation);
                if (!Grid.IsCellOffsetValid(cell, offset)) return false;
                int support = Grid.OffsetCell(cell, offset);
                if (!Grid.IsValidBuildingCell(support) || Grid.WorldIdx[support] != Grid.WorldIdx[cell]
                    || !Grid.Solid[support] || !IsBuiltSolidTile(Grid.Objects[support, (int)ObjectLayer.FoundationTile]))
                    return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    internal static class InteriorUtilitySupportMonitoringPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            // Player-built cargo interfaces and power plug; never the four fixed ports.
            foreach (System.Type config in new[] {
                typeof(RocketInteriorGasInputConfig), typeof(RocketInteriorGasOutputConfig),
                typeof(RocketInteriorLiquidInputConfig), typeof(RocketInteriorLiquidOutputConfig),
                typeof(RocketInteriorSolidInputConfig), typeof(RocketInteriorSolidOutputConfig),
                typeof(RocketInteriorPowerPlugConfig)
            })
                yield return AccessTools.Method(config, "CreateBuildingDef");
        }

        private static void Postfix(BuildingDef __result)
        {
            if (__result != null && (InteriorBuildingPolicy.AllowWallDeconstruction
                || InteriorBuildingPolicy.AllowPortsOnAnySolidTile))
                __result.ContinuouslyCheckFoundation = true;
        }
    }

    [HarmonyPatch(typeof(RocketInteriorPowerPlugConfig), nameof(RocketInteriorPowerPlugConfig.DoPostConfigureComplete))]
    internal static class InteriorPowerPlugSupportPatch
    {
        private static void Postfix(UnityEngine.GameObject go)
        {
            if (InteriorBuildingPolicy.AllowWallDeconstruction || InteriorBuildingPolicy.AllowPortsOnAnySolidTile)
                go.AddOrGet<Core.InteriorPowerPlugSupport>();
        }
    }
}
