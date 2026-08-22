using CustomRocketInterior.Options;
using UnityEngine;

namespace CustomRocketInterior.Config
{
    /// <summary>
    /// 运行时配置：由 PLib 选项在启动时驱动（见 Mod.OnLoad）。
    /// </summary>
    internal static class InteriorSizeConfig
    {
        public const int MinSize = 8;
        public const int MaxSize = 96;

        /// <summary>
        /// 房间四周与内部世界边缘之间留出的空隙（格）。
        /// 贴死边缘时墙体与世界边界渲染重叠、看不清，默认留 1 格。
        /// </summary>
        public const int EdgeMargin = 1;

        /// <summary>
        /// 火箭内部世界尺寸（游戏默认 32×32，见 TUNING.ROCKETRY.ROCKET_INTERIOR_SIZE）。
        /// 舱室填满整个世界，墙体就是世界的最外沿。
        /// 注意：该值决定全局网格能同时容纳多少艘火箭的内部，不宜无限放大。
        /// </summary>
        public static Vector2I WorldSize = new Vector2I(40, 40);

        /// <summary>外壳材料（墙砖建筑的建造材质 + 背板格子的元素）</summary>
        public static SimHashes WallElement = SimHashes.Steel;

        /// <summary>最近一次套用的材质选项（用于检测配置变化）</summary>
        public static 墙体材质 LastWallMaterial = 墙体材质.钢;

        /// <summary>把玩家选项套用到运行时配置（含边界钳制）</summary>
        public static void Apply(RocketInteriorOptions o)
        {
            if (o == null)
            {
                return;
            }

            WorldSize = new Vector2I(
                Mathf.Clamp(o.WorldWidth, MinSize, MaxSize),
                Mathf.Clamp(o.WorldHeight, MinSize, MaxSize));
            WallElement = ToElement(o.WallMaterial);
            LastWallMaterial = o.WallMaterial;
        }

        private static SimHashes ToElement(墙体材质 material)
        {
            switch (material)
            {
                case 墙体材质.火成岩:
                    return SimHashes.IgneousRock;
                case 墙体材质.中子质:
                    // 游戏内部名 Unobtanium，即地图边缘的不可破坏“中子质”
                    return SimHashes.Unobtanium;
                case 墙体材质.玻璃:
                    return SimHashes.Glass;
                default:
                    return SimHashes.Steel;
            }
        }
    }
}
