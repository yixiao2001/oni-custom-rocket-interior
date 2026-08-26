using PeterHan.PLib.Options;

namespace CustomRocketInterior.Options
{
    /// <summary>
    /// 墙体材质。用中文命名枚举成员，这样 PLib 的下拉框会直接显示中文标签。
    /// </summary>
    public enum 墙体材质
    {
        钢,
        火成岩,
        中子质,
        玻璃,
    }

    /// <summary>
    /// mod 设置项：在 主菜单 -> 模组 -> 本 mod 的“设置”按钮中修改。
    /// 配置持久化到 PLib 的 config.json，启动时读取。
    /// </summary>
    [ConfigFile]
    public sealed class RocketInteriorOptions
    {
        [Option("舱室宽度", "火箭内部世界的宽度（格）。原版太空员舱为 12。", "大小")]
        [Limit(12, 96)]
        public int WorldWidth { get; set; } = 40;

        [Option("舱室高度", "火箭内部世界的高度（格）。原版太空员舱为 11；顶部需双层墙（外壳+端口墙）才能铺设液体管道，实际世界高度 = 设置值 + 1（顶部预留 2 行安全边距，液体管道不能贴近世界顶边），舱内可用高度 = 设置值 - 4。", "大小")]
        [Limit(12, 96)]
        public int WorldHeight { get; set; } = 40;

        [Option("墙体材质", "外壳墙砖与背板格子的材料。", "外观")]
        public 墙体材质 WallMaterial { get; set; } = 墙体材质.钢;
    }
}
