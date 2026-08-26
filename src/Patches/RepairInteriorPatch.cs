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
    /// 存档加载时的原位修复（只移顶部一行墙，其余一行不动）：
    /// 引擎规则把世界顶部 2 行列为液体管道/液体端口的禁区（实测上方贴实体墙也无效）。
    /// 修复：仅把顶墙行从 H-2 下移到 H-3：
    ///   旧: [..] [37内部] [38顶墙+端口] [39空]   ← 端口行在顶起第2行, 被禁
    ///   新: [..] [37顶墙+端口] [38空] [39空]      ← 端口行在顶起第3行, 合法
    /// 底部、侧墙、舱内其余内容一律不动（内部高度因此少一行）。
    /// 冲突行(37/38)上的玩家建筑/管道/电线会在修复时直接拆除（材料不返还）。
    /// 时机：加载完成后 2 秒执行（模拟运行后再动手）。
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
                if (TargetWorldIdField == null || (int)TargetWorldIdField.GetValue(__instance) < 0)
                {
                    return;
                }
                ClustercraftExteriorDoor captured = __instance;
                string templateName = __instance.interiorTemplateName;
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
                int topOld = off.y + size.y - 2;   // 旧顶墙行 H-2（被禁）
                int topNew = off.y + size.y - 3;   // 新顶墙行 H-3（合法）
                int left = off.x + 1;
                int right = off.x + size.x - 2;

                int probe = Grid.XYToCell(off.x + size.x / 2, topOld);
                if (!Grid.IsValidCell(probe) || Grid.Element[probe].id == SimHashes.Vacuum)
                {
                    return; // 已是新布局/未改造过
                }

                // 冲突行(37 将被墙化, 38 将被清空)上的玩家内容直接拆除
                var foreign = new List<string>();
                foreach (Building b in Components.BuildingCompletes.Items)
                {
                    if (b == null || b.GetMyWorldId() != world.id)
                    {
                        continue;
                    }
                    int row = Grid.CellRow(b.NaturalBuildingCell());
                    if (row != topOld && row != topNew)
                    {
                        continue;
                    }
                    string id = b.GetComponent<KPrefabID>()?.PrefabID().Name;
                    if (id != WallTileId && id != LiquidInputPortId && id != LiquidOutputPortId
                        && id != GasInputPortId && id != GasOutputPortId)
                    {
                        foreign.Add($"{id}@{row}");
                        UnityEngine.Object.Destroy(b.gameObject);
                    }
                }
                var layers = new[] { ObjectLayer.GasConduit, ObjectLayer.LiquidConduit,
                    ObjectLayer.SolidConduit, ObjectLayer.Wire, ObjectLayer.LogicWire };
                for (int r = topNew; r <= topOld; r++)
                {
                    for (int x = left; x <= right; x++)
                    {
                        int c = Grid.XYToCell(x, r);
                        foreach (ObjectLayer layer in layers)
                        {
                            GameObject go = Grid.Objects[c, (int)layer];
                            if (go != null)
                            {
                                foreign.Add($"{layer}@{r}");
                                UnityEngine.Object.Destroy(go);
                            }
                        }
                    }
                }
                if (foreign.Count > 0)
                {
                    Debug.Log($"[CustomRocketInterior] Removed {foreign.Count} player object(s) in repair rows " +
                        $"of world {world.id} ({string.Join(", ", foreign)}).");
                }

                // 收集端口（趁未被删），并记录旧顶墙建筑
                var ports = new List<(string id, int worldX)>();
                var toDestroy = new List<Building>();
                foreach (Building b in Components.BuildingCompletes.Items)
                {
                    if (b == null || b.GetMyWorldId() != world.id)
                    {
                        continue;
                    }
                    int row = Grid.CellRow(b.NaturalBuildingCell());
                    if (row != topOld)
                    {
                        continue;
                    }
                    string id = b.GetComponent<KPrefabID>()?.PrefabID().Name;
                    if (id != WallTileId && id != LiquidInputPortId && id != LiquidOutputPortId
                        && id != GasInputPortId && id != GasOutputPortId)
                    {
                        continue;
                    }
                    if (id != WallTileId)
                    {
                        ports.Add((id, Grid.CellColumn(b.NaturalBuildingCell())));
                    }
                    toDestroy.Add(b);
                }
                foreach (Building b in toDestroy)
                {
                    if (b != null)
                    {
                        UnityEngine.Object.Destroy(b.gameObject);
                    }
                }

                // 格子：旧顶墙行 → 真空；新顶墙行 → 墙；侧墙同步收尾
                SetRow(world, left, right, topNew, true);
                SetRow(world, left, right, topOld, false);
                SetCell(world, left, topOld, false);
                SetCell(world, right, topOld, false);

                // 重建墙与端口——延迟 0.5 秒执行：Unity 的 Destroy 在帧末才真正移除对象，
                // 立刻生成新端口会与“还没死透”的旧端口争夺 utility network 端点
                // （上次日志中的 endpoint stomp 警告，随后的退出崩溃疑与此有关）。
                GameScheduler.Instance.Schedule("CustomRocketInterior.RepairSpawn", 0.5f,
                    delegate
                    {
                        try
                        {
                            int rootCell = Grid.XYToCell(left, off.y);
                            var portXs = new HashSet<int>();
                            foreach ((string id, int wx) in ports)
                            {
                                portXs.Add(wx);
                            }
                            for (int x = left; x <= right; x++)
                            {
                                if (!portXs.Contains(x))
                                {
                                    SpawnWall(world, x, topNew);
                                }
                            }
                            foreach ((string id, int wx) in ports)
                            {
                                TemplateLoader.PlaceBuilding(new Prefab
                                {
                                    id = id,
                                    location_x = wx - left,
                                    location_y = topNew - off.y,
                                    element = SimHashes.Steel,
                                    temperature = 293.15f,
                                }, rootCell);
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[CustomRocketInterior] Repair spawn failed: {e}");
                        }
                    });

                Debug.Log($"[CustomRocketInterior] Repaired old interior layout of world {world.id}: " +
                          $"top wall moved {topOld}->{topNew} ({ports.Count} ports re-placed).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CustomRocketInterior] Interior repair failed: {e}");
            }
        }

        private static void SetRow(WorldContainer world, int left, int right, int row, bool solid)
        {
            for (int x = left; x <= right; x++)
            {
                SetCell(world, x, row, solid);
            }
        }

        private static void SetCell(WorldContainer world, int x, int row, bool solid)
        {
            SimHashes element = solid ? InteriorSizeConfig.WallElement : SimHashes.Vacuum;
            Element e = ElementLoader.FindElementByHash(element);
            if (e == null)
            {
                Debug.LogError($"[CustomRocketInterior] Element {element} not found for repair.");
                return;
            }
            SimMessages.ModifyCell(Grid.XYToCell(x, row), e.idx, 293.15f,
                solid ? 100f : 0f, (byte)0, 0);
        }

        private static void SpawnWall(WorldContainer world, int x, int row)
        {
            GameObject go = TemplateLoader.PlaceBuilding(new Prefab
            {
                id = WallTileId,
                location_x = x - (world.WorldOffset.x + 1),
                location_y = row - world.WorldOffset.y,
                element = InteriorSizeConfig.WallElement,
                temperature = 293.15f,
            }, Grid.XYToCell(world.WorldOffset.x + 1, world.WorldOffset.y));
            if (go == null)
            {
                Debug.LogWarning($"[CustomRocketInterior] Failed to spawn wall at {x},{row}.");
            }
        }
    }
}
