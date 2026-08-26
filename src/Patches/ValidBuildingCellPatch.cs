using System;
using HarmonyLib;
using UnityEngine;

namespace CustomRocketInterior.Patches
{
    /// <summary>
    /// 火箭内部世界取消“顶部 2 行禁建”限制。
    ///
    /// 游戏规则（Grid.cs）：
    ///   IsValidBuildingCell(cell) => y <= world.maximumBounds.y - Grid.TopBorderHeight
    /// TopBorderHeight = 2，即任何世界的顶部 2 行都是禁建区（为陨石/太空层预留）。
    /// 所有建筑放置（含管道 Anywhere、端口 Tile、装配器等）都先过这道门槛，
    /// 原版火箭舱只因房间离世界顶 10 行以上而幸免；我们的房间铺满整个世界时，
    /// 顶墙行(第 2 行)就落入禁建区——液体管道/端口/装配器全部无法建造。
    /// 火箭内部世界没有陨石与太空层，该预留无意义，故对内部世界免去此限制。
    /// 作用范围仅限 IsModuleInterior 的世界，基星图等所有其他世界行为不变。
    /// </summary>
    [HarmonyPatch(typeof(Grid), nameof(Grid.IsValidBuildingCell))]
    internal static class ValidBuildingCellPatch
    {
        private static bool Prefix(ref bool __result, int cell)
        {
            try
            {
                if (!Grid.IsWorldValidCell(cell))
                {
                    __result = false;
                    return false;
                }
                WorldContainer world = ClusterManager.Instance.GetWorld(Grid.WorldIdx[cell]);
                if (world == null || !world.IsModuleInterior)
                {
                    return true; // 非火箭内部：走游戏原逻辑
                }
                // 火箭内部：完整边界内均可建造，不扣 TopBorderHeight
                Vector2I v = Grid.CellToXY(cell);
                __result = v.x >= world.minimumBounds.x && v.x <= world.maximumBounds.x
                    && v.y >= world.minimumBounds.y && v.y <= world.maximumBounds.y;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CustomRocketInterior] IsValidBuildingCell patch failed: {e}");
                return true;
            }
        }
    }
}
