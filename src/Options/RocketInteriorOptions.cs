using PeterHan.PLib.Options;

namespace CustomRocketInterior.Options
{
    /// <summary>
    /// 墙体材质。
    ///
    /// 注意：枚举成员名必须是 ASCII——它们会进入 JSON 配置、Enum.GetValues/GetMember
    /// 反射匹配、ToUpperInvariant 生成的 STRINGS key 等路径；显示名通过 [Option]
    /// 指向字符串表 key（LookInStrings 按语言解析），不再依赖中文标识符。
    /// </summary>
    public enum WallMaterial
    {
        [Option(StringKeys.WALL_STEEL_NAME, StringKeys.WALL_MATERIAL_TOOLTIP)]
        Steel,

        [Option(StringKeys.WALL_IGNEOUS_ROCK_NAME, StringKeys.WALL_MATERIAL_TOOLTIP)]
        IgneousRock,

        [Option(StringKeys.WALL_NEUTRONIUM_NAME, StringKeys.WALL_MATERIAL_TOOLTIP)]
        Neutronium,

        [Option(StringKeys.WALL_GLASS_NAME, StringKeys.WALL_MATERIAL_TOOLTIP)]
        Glass,
    }

    /// <summary>
    /// mod 设置项：在 主菜单 -> 模组 -> 本 mod 的“设置”按钮中修改。
    /// 配置持久化到 PLib 的 config.json，启动时读取。
    ///
    /// 所有显示文本均以字符串表 key 经 PLib 的 LookInStrings 按语言解析：
    /// 英文用户看到英文（ASCII，任何字库都能渲染），中文用户在 translations/zh.po
    /// 命中后看到中文（游戏切到中文字库渲染）。
    /// </summary>
    [ConfigFile]
    public sealed class RocketInteriorOptions
    {
        [Option(StringKeys.INTERIOR_WIDTH_NAME, StringKeys.INTERIOR_WIDTH_TOOLTIP, StringKeys.CATEGORY_SIZE)]
        [Limit(Config.InteriorSizeConfig.MinSize, Config.InteriorSizeConfig.MaxSize)]
        public int WorldWidth { get; set; } = 40;

        [Option(StringKeys.INTERIOR_HEIGHT_NAME, StringKeys.INTERIOR_HEIGHT_TOOLTIP, StringKeys.CATEGORY_SIZE)]
        [Limit(Config.InteriorSizeConfig.MinSize, Config.InteriorSizeConfig.MaxSize)]
        public int WorldHeight { get; set; } = 40;

        [Option(StringKeys.WALL_MATERIAL_NAME, StringKeys.WALL_MATERIAL_TOOLTIP, StringKeys.CATEGORY_APPEARANCE)]
        public WallMaterial WallMaterial { get; set; } = WallMaterial.Steel;
    }
}
