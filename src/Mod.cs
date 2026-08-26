using CustomRocketInterior.Config;
using CustomRocketInterior.Options;
using HarmonyLib;
using KMod;
using UnityEngine;
using PLibOptions = PeterHan.PLib.Options.POptions;

namespace CustomRocketInterior
{
    /// <summary>
    /// mod 入口：游戏加载 DLL 时反射找到 UserMod2 子类并调用 OnLoad。
    /// </summary>
    public sealed class Mod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony); // 应用全部 [HarmonyPatch]

            // 读取玩家设置（PLib 持久化的 config.json），套用到运行时配置
            RocketInteriorOptions options =
                PLibOptions.ReadSettings<RocketInteriorOptions>() ?? new RocketInteriorOptions();
            InteriorSizeConfig.Apply(options);

            // 扩大火箭内部世界。游戏源码中这是 public static 可变字段（默认 32×32），
            // ClusterManager.CreateRocketInteriorWorld 与 BestFit.CountRocketInteriors
            // 都在运行时读取它，直接赋值即可全局生效，无需打补丁。
            // 世界高度 = 设置值 + 1：顶部留出 2 行安全边距（世界顶边附近的行不能建液体管道），
// 舱内可用高度仍与旧版一致（设置值 - 4），一处空间都不损失；设置值 40 即世界 41 行。
TUNING.ROCKETRY.ROCKET_INTERIOR_SIZE = new Vector2I(InteriorSizeConfig.WorldSize.x,
    InteriorSizeConfig.WorldSize.y + 1);

            // 注册选项界面：主菜单 -> 模组 -> 本 mod -> 设置
            new PLibOptions().RegisterOptions(this, typeof(RocketInteriorOptions));

            Debug.Log($"[CustomRocketInterior] loaded. " +
                      $"interior world = {InteriorSizeConfig.WorldSize.x}x{InteriorSizeConfig.WorldSize.y}, " +
                      $"wall = {InteriorSizeConfig.WallElement}");
        }
    }
}
