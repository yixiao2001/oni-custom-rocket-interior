using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TemplateClasses;
using CustomRocketInterior.Config;
using UnityEngine;

namespace CustomRocketInterior.Patches
{
    /// <summary>
    /// 存档加载时的“外科手术”式原位修复：只动火箭内部世界的顶部两行，
    /// 把旧布局（顶墙位于世界顶部第 2 行 H-2，液体管道/装配器无法建造）
    /// 迁移到新布局（单层顶墙下移到 H-3，端口随迁，旧顶行清成真空）。
    ///
    /// 舱内其余内容（管道、装修、建筑、门与配对关系）全部不动。
    /// 触发：从存档加载的模块（targetWorldId >= 0）且 H-2 行仍有墙。
    /// 幂等：修复后 H-2 行为真空，下次加载自动跳过。
    /// </summary>
    [HarmonyPatch(typeof(ClustercraftExteriorDoor), "OnSpawn")]
    internal static class RepairInteriorPatch
    {
        private const string WallTileId = "RocketWallTile";
        private const string LiquidInputPortId = "RocketInteriorLiquidInputPort";
        private const string LiquidOutputPortId = "RocketInteriorLiquidOutputPort";
        private const string GasInputPortId = "RocketInteriorGasInputPort";
        private const string GasOutputPortId = "RocketInteriorGasOutputPort";

        private static readonly FieldInfo TargetWorldIdField =
            typeof(ClustercraftExteriorDoor).GetField("targetWorldId", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void Postfix(ClustercraftExteriorDoor __instance)
        {
            try
            {
                // 新建模块 targetWorldId == -1（刚刚走完创建流程，已是新布局），跳过
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
                if (world == null || !world.IsModuleInterior)
                {
                    return;
                }

                Vector2I off = world.WorldOffset;
                Vector2I size = world.WorldSize;
                int oldRow = off.y + size.y - 2;   // 旧顶墙行（世界顶部第 2 行）
                int newRow = off.y + size.y - 3;   // 新顶墙行（世界顶部第 3 行）
                int left = off.x + 1;
                int right = off.x + size.x - 2;

                int probe = Grid.XYToCell(off.x + size.x / 2, oldRow);
                if (!Grid.IsValidCell(probe) || Grid.Element[probe].id == SimHashes.Vacuum)
                {
                    // H-2 行无墙：已是新布局（或从未被本 mod 改造过），无需修复
                    return;
                }

                // 1) 收集旧顶部建筑（墙砖 + 端口）。若两行上有任何"非墙砖/端口"的
                //    玩家建筑、管道或电线，则跳过修复（避免把东西埋进新墙里）。
                var ports = new List<(string id, int worldX)>();
                var toDestroy = new List<Building>();
                var foreign = new List<string>();
                foreach (Building b in Components.BuildingCompletes.Items)
                {
                    if (b == null || b.GetMyWorldId() != world.id)
                    {
                        continue;
                    }
                    int row = Grid.CellRow(b.NaturalBuildingCell());
                    if (row != oldRow && row != newRow)
                    {
                        continue;
                    }
                    string id = b.GetComponent<KPrefabID>()?.PrefabID().Name;
                    if (id != WallTileId && id != LiquidInputPortId && id != LiquidOutputPortId
                        && id != GasInputPortId && id != GasOutputPortId)
                    {
                        foreign.Add(id);
                        continue;
                    }
                    if (id != WallTileId)
                    {
                        ports.Add((id, Grid.CellColumn(b.NaturalBuildingCell())));
                    }
                    toDestroy.Add(b);
                }
                if (foreign.Count > 0)
                {
                    Debug.LogWarning($"[CustomRocketInterior] Interior repair SKIPPED for world {world.id}: " +
                        $"top rows contain player buildings ({string.Join(", ", foreign)}). " +
                        $"Remove anything on the top two rows, save and re-load to repair.");
                    return;
                }
                // 管道/电线等非建筑对象同样会阻止修复
                var layers = new[] { ObjectLayer.GasConduit, ObjectLayer.LiquidConduit,
                    ObjectLayer.SolidConduit, ObjectLayer.Wire, ObjectLayer.LogicWire };
                var blocked = new List<string>();
                for (int r = newRow; r <= oldRow; r++)
                {
                    for (int x = left; x <= right; x++)
                    {
                        int c = Grid.XYToCell(x, r);
                        foreach (ObjectLayer layer in layers)
                        {
                            if (Grid.Objects[c, (int)layer] != null)
                            {
                                blocked.Add(layer.ToString());
                            }
                        }
                    }
                }
                if (blocked.Count > 0)
                {
                    Debug.LogWarning($"[CustomRocketInterior] Interior repair SKIPPED for world {world.id}: " +
                        $"top rows contain {string.Join(", ", blocked)}. " +
                        $"Remove anything on the top two rows, save and re-load to repair.");
                    return;
                }

                // 2) 构建“增量模板”：旧行清真空 + 新行铺墙，仅含顶部两行
                var cells = new List<Cell>();
                var buildings = new List<Prefab>();
                int centerX = off.x + size.x / 2;
                int centerY = off.y + size.y / 2;
                for (int x = left; x <= right; x++)
                {
                    cells.Add(new Cell(
                        x - centerX, oldRow - centerY,
                        SimHashes.Vacuum, 293.15f, 0f, null, 0));
                    cells.Add(new Cell(
                        x - centerX, newRow - centerY,
                        InteriorSizeConfig.WallElement, 293.15f, 100f, null, 0));
                }
                var portXs = new HashSet<int>();
                foreach ((string id, int wx) in ports)
                {
                    portXs.Add(wx);
                }
                for (int x = left; x <= right; x++)
                {
                    if (!portXs.Contains(x))
                    {
                        buildings.Add(new Prefab
                        {
                            id = WallTileId,
                            location_x = x - centerX,
                            location_y = newRow - centerY,
                            element = InteriorSizeConfig.WallElement,
                            temperature = 293.15f,
                        });
                    }
                }
                foreach ((string id, int wx) in ports)
                {
                    buildings.Add(new Prefab
                    {
                        id = id,
                        location_x = wx - centerX,
                        location_y = newRow - centerY,
                        element = SimHashes.Steel,
                        temperature = 293.15f,
                    });
                }

                // 3) 删除旧顶部两行的建筑，然后用增量模板覆盖顶部两行格子和新建筑
                foreach (Building b in toDestroy)
                {
                    if (b != null)
                    {
                        UnityEngine.Object.Destroy(b.gameObject);
                    }
                }

                var delta = new TemplateContainer();
                delta.cells = cells;
                delta.buildings = buildings;
                delta.pickupables = new List<Prefab>();
                delta.elementalOres = new List<Prefab>();
                delta.otherEntities = new List<Prefab>();
                delta.info = TemplateCache.GetTemplate(__instance.interiorTemplateName)?.info;
                TemplateLoader.Stamp(delta,
                    new Vector2(centerX, centerY), null);

                Debug.Log($"[CustomRocketInterior] Repaired old interior layout of world {world.id}: " +
                          $"top wall moved from H-2 to H-3 ({ports.Count} ports re-placed).");
            }
            catch (Exception e)
            {
                // 修复失败绝不能让游戏崩溃
                Debug.LogError($"[CustomRocketInterior] Interior repair failed: {e}");
            }
        }
    }
}
