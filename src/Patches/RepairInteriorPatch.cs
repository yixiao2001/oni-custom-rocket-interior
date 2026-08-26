using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TemplateClasses;
using UnityEngine;
using CustomRocketInterior.Config;

namespace CustomRocketInterior.Patches
{
    /// <summary>
    /// 存档加载时的原位修复（纯加法，零侵入）：
    /// 旧布局的火箭内部（单层顶墙在世界顶部第 2 行 H-2）因为端口行上方没有实体格，
    /// 整行无法铺设液体管道/挂载液体端口。修复 = 只在原本空着的顶部边距行 H-1
    /// （世界顶边行）补一整行同材料墙（壳层），端口墙行与舱内内容全部不动：
    ///   H-1  壳层墙（新增，贴世界顶边）
    ///   H-2  端口墙（不动，管道从此可铺）
    /// 舱内可用高度与旧布局完全一致，一行也不损失。
    ///
    /// 触发：从存档加载的模块（targetWorldId >= 0），H-2 行有墙且 H-1 行无墙。
    /// 幂等：修复后 H-1 行为墙，下次加载自动跳过。
    /// </summary>
    [HarmonyPatch(typeof(ClustercraftExteriorDoor), "OnSpawn")]
    internal static class RepairInteriorPatch
    {
        private const string WallTileId = "RocketWallTile";

        private static readonly FieldInfo TargetWorldIdField =
            typeof(ClustercraftExteriorDoor).GetField("targetWorldId", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void Postfix(ClustercraftExteriorDoor __instance)
        {
            // 重要：绝不能在这里直接盖章——存档加载期间模拟系统尚未就绪，
            // 异步回调句柄会与游戏自身的 cell 更新冲突（CallbackInfo 版本错配 → NRE）。
            // 改为在世界加载完成、模拟跑起来之后（延迟 2 秒）再执行修复。
            try
            {
                if (TargetWorldIdField == null || (int)TargetWorldIdField.GetValue(__instance) < 0)
                {
                    return;
                }
                RocketModuleCluster module = __instance.GetComponent<RocketModuleCluster>();
                if (module == null || module.CraftInterface == null)
                {
                    return;
                }
                WorldContainer world = module.CraftInterface.gameObject.GetComponent<WorldContainer>();
                if (world == null)
                {
                    return;
                }
                string templateName = __instance.interiorTemplateName;
                ClustercraftExteriorDoor captured = __instance;
                GameScheduler.Instance.Schedule("CustomRocketInterior.Repair", 2f,
                    delegate { Run(captured, templateName); });
            }
            catch (Exception e)
            {
                Debug.LogError($"[CustomRocketInterior] Interior repair scheduling failed: {e}");
            }
        }

        private static void Run(ClustercraftExteriorDoor __instance, string templateName)
        {
            try
            {
                // Unity 销毁过的对象 == null（判空安全）
                if (__instance == null)
                {
                    return;
                }
                RocketModuleCluster module = __instance.GetComponent<RocketModuleCluster>();
                if (module == null || module.CraftInterface == null)
                {
                    return;
                }
                WorldContainer world = module.CraftInterface.gameObject.GetComponent<WorldContainer>();
                if (world == null || !world.IsModuleInterior)
                {
                    return;
                }

                Vector2I off = world.WorldOffset;
                Vector2I size = world.WorldSize;
                int portRow = off.y + size.y - 2;   // 端口墙行 H-2（旧布局顶墙行）
                int shellRow = off.y + size.y - 1;  // 壳层行 H-1（世界顶边）
                int left = off.x + 1;
                int right = off.x + size.x - 2;

                int probePort = Grid.XYToCell(off.x + size.x / 2, portRow);
                int probeShell = Grid.XYToCell(off.x + size.x / 2, shellRow);
                if (!Grid.IsValidCell(probePort) || !Grid.IsValidCell(probeShell))
                {
                    return;
                }
                // H-2 无墙 = 无需修复（已是新布局/原版小模板）；H-1 已有墙 = 已修复过
                if (Grid.Element[probePort].id == SimHashes.Vacuum
                    || Grid.Element[probeShell].id != SimHashes.Vacuum)
                {
                    return;
                }

                // 壳层行原本是空顶边距，通常没有对象；万一有玩家东西则跳过以免误伤
                var layers = new[] { ObjectLayer.Building, ObjectLayer.GasConduit,
                    ObjectLayer.LiquidConduit, ObjectLayer.SolidConduit,
                    ObjectLayer.Wire, ObjectLayer.LogicWire };
                for (int x = left; x <= right; x++)
                {
                    int c = Grid.XYToCell(x, shellRow);
                    foreach (ObjectLayer layer in layers)
                    {
                        if (Grid.Objects[c, (int)layer] != null)
                        {
                            Debug.LogWarning($"[CustomRocketInterior] Interior repair SKIPPED for world {world.id}: " +
                                $"empty border row {shellRow} contains objects. Remove them, save and re-load.");
                            return;
                        }
                    }
                }

                // 构建增量模板：只补壳层行（全宽墙）
                var cells = new List<Cell>();
                var buildings = new List<Prefab>();
                int centerX = off.x + size.x / 2;
                int centerY = off.y + size.y / 2;
                for (int x = left; x <= right; x++)
                {
                    cells.Add(new Cell(
                        x - centerX, shellRow - centerY,
                        InteriorSizeConfig.WallElement, 293.15f, 100f, null, 0));
                    buildings.Add(new Prefab
                    {
                        id = WallTileId,
                        location_x = x - centerX,
                        location_y = shellRow - centerY,
                        element = InteriorSizeConfig.WallElement,
                        temperature = 293.15f,
                    });
                }

                var delta = new TemplateContainer();
                delta.cells = cells;
                delta.buildings = buildings;
                delta.pickupables = new List<Prefab>();
                delta.elementalOres = new List<Prefab>();
                delta.otherEntities = new List<Prefab>();
                delta.info = TemplateCache.GetTemplate(templateName)?.info;
                TemplateLoader.Stamp(delta, new Vector2(centerX, centerY), null);

                Debug.Log($"[CustomRocketInterior] Repaired old interior layout of world {world.id}: " +
                          $"shell row added at H-1 above the port wall (interior size preserved).");
            }
            catch (Exception e)
            {
                // 修复失败绝不能让游戏崩溃
                Debug.LogError($"[CustomRocketInterior] Interior repair failed: {e}");
            }
        }
    }
}
