using CustomRocketInterior.Config;
using CustomRocketInterior.Options;
using HarmonyLib;
using UnityEngine;

namespace CustomRocketInterior.Patches
{
    /// <summary>
    /// 让选项修改“即时生效”：
    /// 玩家在主菜单改完设置后往往直接开始游戏，而 OnLoad 早在启动时就执行过了。
    /// 此补丁在每次创建火箭内部世界前重新读取 config.json，
    /// 若配置有变化则刷新运行时配置、世界尺寸，目标模板在下次读取时按新设置重塑——
    /// 之后新建的火箭立即使用新尺寸与新材质，无需重启游戏。
    /// </summary>
    [HarmonyPatch(typeof(ClusterManager), nameof(ClusterManager.CreateRocketInteriorWorld))]
    internal static class ClusterManager_CreateRocketInteriorWorld_Patch
    {
        private static void Prefix()
        {
            try
            {
                RocketInteriorOptions options =
                    PeterHan.PLib.Options.POptions.ReadSettings<RocketInteriorOptions>();
                if (options == null)
                {
                    return;
                }

                var previousSize = InteriorSizeConfig.WorldSize;
                var previousMaterial = InteriorSizeConfig.LastWallMaterial;
                InteriorSizeConfig.Apply(options);
                bool changed = previousSize.x != InteriorSizeConfig.WorldSize.x
                            || previousSize.y != InteriorSizeConfig.WorldSize.y
                            || previousMaterial != InteriorSizeConfig.LastWallMaterial;

                if (!changed)
                {
                    return;
                }

                TUNING.ROCKETRY.ROCKET_INTERIOR_SIZE = InteriorSizeConfig.WorldSize;
                // InteriorResizer detects size/material changes per cached template;
                // leave unrelated world/POI template caches intact.
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CustomRocketInterior] Failed to apply live options: {e}");
            }
        }
    }
}
