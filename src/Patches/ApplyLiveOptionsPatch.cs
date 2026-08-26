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
    /// 若配置有变化则刷新运行时配置、世界尺寸并清空模板缓存——
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

                bool changed = options.WorldWidth != InteriorSizeConfig.WorldSize.x
                            || options.WorldHeight != InteriorSizeConfig.WorldSize.y
                            || options.WallMaterial != InteriorSizeConfig.LastWallMaterial;

                if (!changed)
                {
                    return;
                }

                InteriorSizeConfig.Apply(options);
                TUNING.ROCKETRY.ROCKET_INTERIOR_SIZE = InteriorSizeConfig.WorldSize;
                // 重置模板缓存让下一次 GetTemplate 拿到原生模板、按新配置重塑。
                // 注意：Clear() 只是把字典置空（GetTemplate 不会自行初始化），
                // 必须像游戏本体 DebugBaseTemplateButton 那样 Clear + Init 成对调用，
                // 否则后续 GetTemplate 会对 null 字典抛 NullReferenceException。
                TemplateCache.Clear();
                TemplateCache.Init();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CustomRocketInterior] Failed to apply live options: {e}");
            }
        }
    }
}
