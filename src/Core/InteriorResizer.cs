using System;
using System.Collections.Generic;
using CustomRocketInterior.Config;
using TemplateClasses;
using UnityEngine;

namespace CustomRocketInterior.Core
{
    /// <summary>
    /// 核心纯逻辑：把火箭内部模板（TemplateContainer）重塑为填满整个内部世界的矩形。
    ///
    /// 游戏原理解析（基于对 Assembly-CSharp 的反编译）：
    ///   - 太空员舱等模块通过 ClustercraftExteriorDoor.interiorTemplateName 引用一个内部模板，
    ///     例如 "expansion1::interiors/habitat_medium"，对应游戏目录下的 YAML 文件：
    ///     StreamingAssets/dlc/expansion1/templates/interiors/habitat_medium.yaml
    ///   - 模板由三层内容构成：
    ///       cells          物理格子：外壳为实体元素，内部为 Vacuum
    ///       buildings      建筑：RocketWallTile（外壳墙砖）、RocketEnvelopeWindowTile（窗）、
    ///                      以及气/液端口、RocketControlStation 等功能建筑
    ///       otherEntities  特殊实体：ClustercraftInteriorDoor（内部门）在这里
    ///   - 原版太空员舱不是矩形：主体上方还有一排"天花板"墙（液体端口嵌在其中），
    ///     因此重塑时不能只拆包围盒边界，必须清掉全部壳层再重建。
    ///   - 内部门与外部门按"世界"配对（PairWithInteriorDoor），与坐标无关；
    ///   - TemplateContainer.RefreshInfo() 会从 cells 自动重算 info.size/min/area。
    ///
    /// 重塑策略（幂等）：
    ///   1. 以原点为中心生成新包围盒（模板以原点印在世界中心），尺寸 = 配置的世界尺寸；
    ///   2. 删除全部墙砖/窗砖建筑；删除全部非真空格（保护位置除外）；
    ///   3. 功能内容按规则吸附到新结构上：
    ///        气体端口 -> 新底边墙线；液体端口 -> 新顶边墙线；
    ///        内部门 -> 左下角（左墙线、底边内侧）；控制站 -> 底边内一格；
    ///   4. 新边界铺配置材料的背板格 + RocketWallTile 墙砖，内部空位填真空；
    ///   5. RefreshInfo() 重算元数据。
    /// </summary>
    internal static class InteriorResizer
    {
        private const string WallTileId = "RocketWallTile";
        private const string WindowTileId = "RocketEnvelopeWindowTile";
        private const string InteriorDoorId = "ClustercraftInteriorDoor";
        private const string ControlStationId = "RocketControlStation";
        private const string GasInputPortId = "RocketInteriorGasInputPort";
        private const string GasOutputPortId = "RocketInteriorGasOutputPort";
        private const string LiquidInputPortId = "RocketInteriorLiquidInputPort";
        private const string LiquidOutputPortId = "RocketInteriorLiquidOutputPort";

        /// <summary>与游戏原生模板一致的参数（293.15K / mass=100）</summary>
        private const float ShellTemperature = 293.15f;
        private const float ShellMass = 100f;

        /// <summary>
        /// 补丁入口：按当前配置把模板重塑为填满世界。无关模板或已重塑过时安全跳过。
        /// </summary>
        public static void ApplyOverride(TemplateContainer template, string templatePath)
        {
            if (template == null || templatePath == null)
            {
                return;
            }

            // 安全过滤：TemplateCache 同时服务基地/POI 等所有模板，
            // 只允许作用于火箭内部模板。注意路径格式 "expansion1::interiors/habitat_medium"
            // 中 interiors 前面是 :: 分隔符，因此匹配 "interiors/" 即可。
            if (!templatePath.Contains("interiors/"))
            {
                return;
            }

            Vector2I ws = InteriorSizeConfig.WorldSize;
            try
            {
                // 四周各留 EdgeMargin 格空隙。世界高度在赋值 ROCKET_INTERIOR_SIZE 时
                // 已额外 +1（顶部留 2 行安全边距：世界顶边附近的行不能铺设液体管道），
                // 房间仍按设置值生成，舱内可用高度与旧版一致。
                Resize(template,
                    Math.Max(MinSizeClamp, ws.x - 2 * InteriorSizeConfig.EdgeMargin),
                    Math.Max(MinSizeClamp, ws.y - 2 * InteriorSizeConfig.EdgeMargin));
            }
            catch (Exception e)
            {
                // 模板重塑失败不应让游戏崩溃，退回原生模板并留下排查线索
                Debug.LogError($"[CustomRocketInterior] Failed to resize template '{templatePath}': {e}");
            }
        }

        private const int MinSizeClamp = 4;

        /// <summary>
        /// 把模板重塑为 newWidth × newHeight 的居中矩形。
        /// 返回是否发生了实际修改；重复调用（缓存命中）时自动跳过，保证幂等。
        /// </summary>
        public static bool Resize(TemplateContainer template, int newWidth, int newHeight)
        {
            if (template?.cells == null || template.buildings == null)
            {
                return false;
            }

            ComputeBounds(template, out int xmin, out int xmax, out int ymin, out int ymax);

            newWidth = Math.Max(newWidth, MinSizeClamp);
            newHeight = Math.Max(newHeight, MinSizeClamp);

            // 以原点为中心的新包围盒（模板印在世界中心，居中才能贴合世界边缘）。
            // 奇数高度时 -(H/2) 会向下偏一格，故用 -( (H+1)/2 ) 让房间底边对齐
            // 底部 1 格边距、顶部多出 1 格边距（顶墙抵达 H-3）。
            int nxmin = -((newWidth + 1) / 2);
            int nymin = -((newHeight + 1) / 2);
            int nxmax = nxmin + newWidth - 1;
            int nymax = nymin + newHeight - 1;

            // 幂等保护：已是目标居中尺寸则跳过（模板是进程级缓存单例，会被反复获取）。
            if (xmin == nxmin && xmax == nxmax && ymin == nymin && ymax == nymax)
            {
                return false;
            }

            int curW = xmax - xmin + 1;
            int curH = ymax - ymin + 1;

            // 先把功能内容挪到新结构的对应位置，再清壳、重建。
            // 注意顺序：不能在清壳时“保护”功能建筑原位置的背板格——
            // 它们随后会被挪走，旧位置的保护格会变成房间里的游离实心块。
            List<Prefab> functional = CollectFunctionalPrefabs(template);
            SnapFunctionalPrefabs(functional, nxmin, nxmax, nymin, nymax);
            SnapInteriorDoor(template, nxmin, nymin, nymax);

            ClearShell(template);
            FillNewLayout(template, nxmin, nxmax, nymin, nymax);

            template.RefreshInfo();
            Debug.Log($"[CustomRocketInterior] Resized interior '{template.name}': " +
                      $"{curW}x{curH} -> {newWidth}x{newHeight}, wall = {InteriorSizeConfig.WallElement}");
            return true;
        }

        /// <summary>基于 cells + buildings + otherEntities 计算模板内容包围盒</summary>
        private static void ComputeBounds(TemplateContainer t,
            out int xmin, out int xmax, out int ymin, out int ymax)
        {
            xmin = ymin = int.MaxValue;
            xmax = ymax = int.MinValue;

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;

            foreach (Cell c in t.cells)
            {
                minX = Math.Min(minX, c.location_x);
                maxX = Math.Max(maxX, c.location_x);
                minY = Math.Min(minY, c.location_y);
                maxY = Math.Max(maxY, c.location_y);
            }

            foreach (Prefab p in EnumerateAll(t.buildings, t.otherEntities))
            {
                minX = Math.Min(minX, p.location_x);
                maxX = Math.Max(maxX, p.location_x);
                minY = Math.Min(minY, p.location_y);
                maxY = Math.Max(maxY, p.location_y);
            }

            if (minX > maxX || minY > maxY)
            {
                throw new InvalidOperationException("Template has no content to measure.");
            }

            xmin = minX;
            xmax = maxX;
            ymin = minY;
            ymax = maxY;
        }

        /// <summary>收集需要保留并重新定位的功能建筑（端口、控制站等）</summary>
        private static List<Prefab> CollectFunctionalPrefabs(TemplateContainer t)
        {
            var result = new List<Prefab>();
            foreach (Prefab b in t.buildings)
            {
                if (b.id != WallTileId && b.id != WindowTileId)
                {
                    result.Add(b);
                }
            }

            return result;
        }

        /// <summary>
        /// 清空全部壳层：所有墙砖/窗砖建筑，以及所有非真空的实体格子。
        /// 用“非真空”而不是枚举具体材料，天然兼容任意可配置的墙体材料；
        /// 不做任何位置保护——功能建筑已先一步吸附到新结构，
        /// 其新位置的背板格由 FillNewLayout 统一重建。
        /// </summary>
        private static void ClearShell(TemplateContainer t)
        {
            t.buildings.RemoveAll(b => b.id == WallTileId || b.id == WindowTileId);
            t.cells.RemoveAll(c => c.element != SimHashes.Vacuum);
        }

        /// <summary>
        /// 把功能建筑吸附到新结构的对应位置（保持原有相对布局语义）：
        /// 气体端口贴底边墙线、液体端口贴顶边墙线、控制站贴底边内侧一格，
        /// 其余（若有）夹取到房间内部。
        /// </summary>
        private static void SnapFunctionalPrefabs(List<Prefab> functional,
            int xmin, int xmax, int ymin, int ymax)
        {
            foreach (Prefab b in functional)
            {
                switch (b.id)
                {
                    case GasInputPortId:
                    case GasOutputPortId:
                        b.location_y = ymin;
                        b.location_x = Clamp(b.location_x, xmin + 1, xmax - 1);
                        break;
                    case LiquidInputPortId:
                    case LiquidOutputPortId:
                        b.location_y = ymax;
                        b.location_x = Clamp(b.location_x, xmin + 1, xmax - 1);
                        break;
                    case ControlStationId:
                        b.location_y = ymin + 1;
                        b.location_x = Clamp(b.location_x, xmin + 2, xmax - 2);
                        break;
                    default:
                        b.location_x = Clamp(b.location_x, xmin + 1, xmax - 1);
                        b.location_y = Clamp(b.location_y, ymin + 1, ymax - 1);
                        break;
                }
            }
        }

        /// <summary>
        /// 内部门吸到左下角（左墙线、底边内侧一格），与原版"贴墙"语义一致；
        /// 与外部门的配对按世界查找（PairWithInteriorDoor），移动无副作用。
        /// </summary>
        private static void SnapInteriorDoor(TemplateContainer t, int xmin, int ymin, int _)
        {
            foreach (Prefab e in t.otherEntities ?? new List<Prefab>())
            {
                if (e.id == InteriorDoorId)
                {
                    e.location_x = xmin;
                    e.location_y = ymin + 1;
                }
            }
        }

        /// <summary>
        /// 生成新布局：边界 = 配置材料的背板格 + RocketWallTile 墙砖（被功能建筑占据的位置不放墙砖）；
        /// 内部所有格子统一为真空。已有格子直接原地改写元素，避免重复添加。
        /// </summary>
        private static void FillNewLayout(TemplateContainer t,
            int xmin, int nxmax, int ymin, int nymax)
        {
            SimHashes wallElement = InteriorSizeConfig.WallElement;

            Dictionary<long, Cell> cellAt = new Dictionary<long, Cell>();
            foreach (Cell c in t.cells)
            {
                cellAt[Key(c.location_x, c.location_y)] = c;
            }

            // 功能建筑与特殊实体的新位置不放墙砖（端口嵌在墙线里）
            HashSet<long> occupied = new HashSet<long>();
            foreach (Prefab p in EnumerateAll(t.buildings, t.otherEntities))
            {
                occupied.Add(Key(p.location_x, p.location_y));
            }

            var newCells = new List<Cell>();
            var newBuildings = new List<Prefab>();

            for (int y = ymin; y <= nymax; y++)
            {
                for (int x = xmin; x <= nxmax; x++)
                {
                    long k = Key(x, y);
                    bool onEdge = x == xmin || x == nxmax || y == ymin || y == nymax;

                    if (onEdge)
                    {
                        if (cellAt.TryGetValue(k, out Cell existing))
                        {
                            // 原地改写为墙体材料（例如保护位置上的旧格子）
                            existing.element = wallElement;
                            existing.mass = ShellMass;
                            existing.temperature = ShellTemperature;
                        }
                        else
                        {
                            var cell = ShellCell(x, y);
                            newCells.Add(cell);
                            cellAt[k] = cell;
                        }

                        if (!occupied.Contains(k))
                        {
                            newBuildings.Add(WallTile(x, y));
                        }
                    }
                    else if (!cellAt.ContainsKey(k))
                    {
                        var cell = VacuumCell(x, y);
                        newCells.Add(cell);
                        cellAt[k] = cell;
                    }
                }
            }

            t.cells.AddRange(newCells);
            t.buildings.AddRange(newBuildings);
        }

        private static IEnumerable<Prefab> EnumerateAll(List<Prefab> a, List<Prefab> b)
        {
            if (a != null)
            {
                foreach (Prefab p in a)
                {
                    yield return p;
                }
            }

            if (b != null)
            {
                foreach (Prefab p in b)
                {
                    yield return p;
                }
            }
        }

        private static int Clamp(int v, int min, int max) => Math.Max(min, Math.Min(max, v));

        private static Cell ShellCell(int x, int y) =>
            new Cell(x, y, InteriorSizeConfig.WallElement, ShellTemperature, ShellMass, null, 0);

        private static Cell VacuumCell(int x, int y) =>
            new Cell(x, y, SimHashes.Vacuum, 0f, 0f, null, 0);

        private static Prefab WallTile(int x, int y) =>
            new Prefab
            {
                id = WallTileId,
                location_x = x,
                location_y = y,
                element = InteriorSizeConfig.WallElement,
                temperature = ShellTemperature,
                type = Prefab.Type.Building,
            };

        private static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    }
}
