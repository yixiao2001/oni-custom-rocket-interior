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
    /// 存档加载时的原位修复（移墙保尺寸）：
    /// 引擎规则：世界顶部 2 行禁止铺设液体管道/挂载液体端口（实测：即使端口行
    /// 上方有实体格也照样被禁）。老存档的世界高度在创建时固化（无法抬高），
    /// 因此把墙体整体下移一行：顶墙 H-2 → H-3，底墙 1 → 0（贴世界底边），
    /// 端口随各自墙线移动（液体端口 H-2 → H-3，气体端口 1 → 0）。
    /// 内部可用高度（旧 2..H-3 行 = H-4 行）保持与旧布局完全相同：
    ///   旧: [0空] [1底墙] [2..37内部36行] [38顶墙] [39空]
    ///   新: [0底墙] [1..36内部36行] [37顶墙] [38..39 空(顶部禁建区)]
    /// 舱内其余内容（家具、装修、门、配对）一律不动；只有新顶墙压到的第 37 行
    /// 有玩家东西时跳过修复（用户清空后可重试）。
    /// 触发：从存档加载的模块（targetWorldId >= 0）且该世界 H-2 行仍有墙。
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
                int topWallOld = off.y + size.y - 2;    // 需要检修的顶墙行（H-2）
                int topWallNew = off.y + size.y - 3;    // 新顶墙行（H-3）
                int bottomWallOld = off.y + 1;          // 旧底墙行
                int bottomWallNew = off.y;              // 新底墙行（贴世界底边）
                int shellLeftover = off.y + size.y - 1; // 旧壳层行（若此前做过壳层修复）
                int left = off.x + 1;
                int right = off.x + size.x - 2;

                int probe = Grid.XYToCell(off.x + size.x / 2, topWallOld);
                if (!Grid.IsValidCell(probe) || Grid.Element[probe].id == SimHashes.Vacuum)
                {
                    return; // H-2 无墙：新布局（世界已 +1）或从未被本 mod 改造过
                }

                // 检测这些行上是否有玩家内容，有则跳过（避免误伤/被新墙埋掉）
                var layers = new[] { ObjectLayer.Building, ObjectLayer.GasConduit,
                    ObjectLayer.LiquidConduit, ObjectLayer.SolidConduit,
                    ObjectLayer.Wire, ObjectLayer.LogicWire };
                var foreignBuildings = new List<string>();
                foreach (Building b in Components.BuildingCompletes.Items)
                {
                    if (b == null || b.GetMyWorldId() != world.id)
                    {
                        continue;
                    }
                    int row = Grid.CellRow(b.NaturalBuildingCell());
                    if (row != topWallOld && row != topWallNew && row != bottomWallOld
                        && row != bottomWallNew && row != shellLeftover)
                    {
                        continue;
                    }
                    string id = b.GetComponent<KPrefabID>()?.PrefabID().Name;
                    if (id != WallTileId && id != LiquidInputPortId && id != LiquidOutputPortId
                        && id != GasInputPortId && id != GasOutputPortId)
                    {
                        foreignBuildings.Add($"{id}@{row}");
                    }
                }
                if (foreignBuildings.Count > 0)
                {
                    Debug.LogWarning($"[CustomRocketInterior] Interior repair SKIPPED for world {world.id}: " +
                        $"rows {topWallNew}..{topWallOld}/{bottomWallNew}..{bottomWallOld} contain player buildings " +
                        $"({string.Join(", ", foreignBuildings)}). Remove them, save and re-load.");
                    return;
                }
                for (int r = bottomWallNew; r <= bottomWallOld; r++)
                {
                    for (int x = left; x <= right; x++)
                    {
                        int c = Grid.XYToCell(x, r);
                        foreach (ObjectLayer layer in layers)
                        {
                            if (Grid.Objects[c, (int)layer] != null)
                            {
                                Debug.LogWarning($"[CustomRocketInterior] Interior repair SKIPPED for world {world.id}: " +
                                    $"bottom rows contain {layer}. Remove them, save and re-load.");
                                return;
                            }
                        }
                    }
                }
                for (int r = topWallNew; r <= topWallOld; r++)
                {
                    for (int x = left; x <= right; x++)
                    {
                        int c = Grid.XYToCell(x, r);
                        foreach (ObjectLayer layer in layers)
                        {
                            if (Grid.Objects[c, (int)layer] != null)
                            {
                                Debug.LogWarning($"[CustomRocketInterior] Interior repair SKIPPED for world {world.id}: " +
                                    $"top rows contain {layer}. Remove them, save and re-load.");
                                return;
                            }
                        }
                    }
                }

                // 收集端口（趁还没被删），并标记旧墙体建筑
                var ports = new List<(string id, int worldX)>();
                var toDestroy = new List<Building>();
                foreach (Building b in Components.BuildingCompletes.Items)
                {
                    if (b == null || b.GetMyWorldId() != world.id)
                    {
                        continue;
                    }
                    int row = Grid.CellRow(b.NaturalBuildingCell());
                    if (row != topWallOld && row != bottomWallOld && row != shellLeftover)
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

                // 删旧墙/旧端口
                foreach (Building b in toDestroy)
                {
                    if (b != null)
                    {
                        UnityEngine.Object.Destroy(b.gameObject);
                    }
                }

                // 格子整备（同步 API，无回调冲突）
                SetRow(world, left, right, off.y, topWallNew, true);     // 新顶墙行 → 墙
                SetRow(world, left, right, off.y, topWallOld, false);    // 旧顶墙行 → 真空
                SetRow(world, left, right, off.y, shellLeftover, false); // 旧壳层行(若有) → 真空
                SetRow(world, left, right, off.y, bottomWallOld, false); // 旧底墙行 → 真空(内部)
                SetRow(world, left, right, off.y, bottomWallNew, true);  // 新底墙行 → 墙
                // 左右侧墙补齐/收尾（底墙下移、顶墙上移）
                for (int r = bottomWallNew; r <= topWallNew; r++)
                {
                    SetCell(world, left, r, true);
                    SetCell(world, right, r, true);
                }
                SetCell(world, left, topWallOld, false);
                SetCell(world, right, topWallOld, false);
                SetCell(world, left, shellLeftover, false);
                SetCell(world, right, shellLeftover, false);

                // 重建墙体与端口（PlaceBuilding 直接实例化，不走盖章回调）
                int rootCell = Grid.XYToCell(left, 0);
                int centerY = off.y + size.y / 2;
                var portXs = new HashSet<int>();
                foreach ((string id, int wx) in ports)
                {
                    portXs.Add(wx);
                }
                for (int x = left; x <= right; x++)
                {
                    if (!portXs.Contains(x))
                    {
                        SpawnWall(world, x, topWallNew);
                        SpawnWall(world, x, bottomWallNew);
                    }
                }
                foreach ((string id, int wx) in ports)
                {
                    bool isLiquid = id == LiquidInputPortId || id == LiquidOutputPortId;
                    int targetRow = isLiquid ? topWallNew : bottomWallNew;
                    TemplateLoader.PlaceBuilding(new Prefab
                    {
                        id = id,
                        location_x = wx - left,
                        location_y = targetRow - off.y,
                        element = SimHashes.Steel,
                        temperature = 293.15f,
                    }, Grid.XYToCell(left, off.y));
                }

                Debug.Log($"[CustomRocketInterior] Repaired old interior layout of world {world.id}: " +
                          $"walls moved down one row (top {topWallOld}->{topWallNew}, bottom {bottomWallOld}->{bottomWallNew}), " +
                          $"{ports.Count} ports re-placed, interior height preserved.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CustomRocketInterior] Interior repair failed: {e}");
            }
        }

        private static void SetRow(WorldContainer world, int left, int right, int offY, int row, bool solid)
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
            GameObject go = null;
            if (solid)
            {
                // 与墙同层，物理上已由 ModifyCell 提供；这里顺便清理可能残留的对象
                go = Grid.Objects[Grid.XYToCell(x, row), (int)ObjectLayer.Building];
            }
            SimMessages.ModifyCell(Grid.XYToCell(x, row), e.idx, 293.15f,
                solid ? 100f : 0f, (byte)0, 0);
            if (solid && go != null && go.GetComponent<KPrefabID>()?.PrefabID().Name != WallTileId)
            {
                UnityEngine.Object.Destroy(go);
            }
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
