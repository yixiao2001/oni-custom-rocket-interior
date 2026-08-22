using HarmonyLib;
using UnityEngine;

namespace CustomRocketInterior.Patches
{
    /// <summary>
    /// 去除火箭内部世界的战争迷雾。
    ///
    /// 原版只在放置模板后以世界中心做一次圆形揭示（GridVisibility.Reveal，半径 = 模板半径+8），
    /// 房间变大后方形世界的四角落在圆外，残留迷雾。
    /// 此补丁在世界模板放置完成后，对该世界的每个格子做一次完全揭示
    /// （Reveal 内部按 WorldIdx 过滤，不会波及全局网格里的其他世界）。
    /// </summary>
    [HarmonyPatch(typeof(WorldContainer), nameof(WorldContainer.PlaceInteriorTemplate))]
    internal static class WorldContainer_PlaceInteriorTemplate_Patch
    {
        private static void Postfix(WorldContainer __instance)
        {
            try
            {
                RevealWholeWorld(__instance);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CustomRocketInterior] Failed to reveal interior world: {e}");
            }
        }

        private static void RevealWholeWorld(WorldContainer world)
        {
            Vector2I offset = world.WorldOffset;
            Vector2I size = world.WorldSize;
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    // 半径 1、内圈 0.5：恰好把当前格完全揭示（揭示量饱和为最大值）
                    GridVisibility.Reveal(offset.x + x, offset.y + y, 1, 0.5f);
                }
            }
        }
    }
}
