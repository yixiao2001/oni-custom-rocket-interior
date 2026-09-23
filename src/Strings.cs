using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace CustomRocketInterior
{
    /// <summary>
    /// 选项界面的字符串表（源语言为英文），与游戏的本地化系统对接。
    ///
    /// 为什么必须走 LocString/STRINGS 而不是硬编码中文：
    ///   PLib 的 LookInStrings(keyOrValue) 会先用 Strings.TryGet 解析传入的字符串——
    ///   命中已注册的 key 就返回对应语言的译文，否则原样显示。
    ///   同时 PLib 的 UI 字体是“按语言”取的（PUITuning.Fonts：
    ///   Localization.GetSelectedLanguageType() != 0 时用 Localization.FontAsset，
    ///   否则用纯拉丁字体 NotoSans-Regular / GRAYSTROKE REGULAR SDF）。
    ///   硬编码中文在英文（未选语言）下会以拉丁字库渲染 → 缺字（方框/不可读）
    ///   甚至触发字形处理异常导致崩溃；这也是英文用户“看不清选项”的直接原因。
    ///   正确做法：代码里用英文源文本 + key 注册，中文放到 translations/zh.po，
    ///   由游戏/PLib 按语言套用，任何语言下文本与字库覆盖都匹配。
    ///
    /// key 命名规则（游戏生成，勿手改）：
    ///   LocString.CreateLocStringKeys(typeof(STRINGS), "CustomRocketInterior.")
    ///   → "CustomRocketInterior.STRINGS." + [嵌套类名.] + 字段名
    ///   与 Localization.OverloadStrings 反查的 key 路径完全一致。
    /// </summary>
    public static class STRINGS
    {
        public static class OPTIONS
        {
            public static LocString INTERIOR_WIDTH_NAME = new LocString(
                "Interior Width", StringKeys.INTERIOR_WIDTH_NAME);

            public static LocString INTERIOR_WIDTH_TOOLTIP = new LocString(
                "Width of the rocket interior world, in tiles. The vanilla Spacefarer Module is 12 tiles wide.",
                StringKeys.INTERIOR_WIDTH_TOOLTIP);

            public static LocString INTERIOR_HEIGHT_NAME = new LocString(
                "Interior Height", StringKeys.INTERIOR_HEIGHT_NAME);

            public static LocString INTERIOR_HEIGHT_TOOLTIP = new LocString(
                "Height of the rocket interior world, in tiles. The vanilla Spacefarer Module is 11 tiles tall.",
                StringKeys.INTERIOR_HEIGHT_TOOLTIP);

            public static LocString WALL_MATERIAL_NAME = new LocString(
                "Wall Material", StringKeys.WALL_MATERIAL_NAME);

            public static LocString WALL_MATERIAL_TOOLTIP = new LocString(
                "Material of the shell wall tiles and their backing cells.",
                StringKeys.WALL_MATERIAL_TOOLTIP);

            public static LocString CATEGORY_SIZE = new LocString(
                "Size", StringKeys.CATEGORY_SIZE);

            public static LocString CATEGORY_APPEARANCE = new LocString(
                "Appearance", StringKeys.CATEGORY_APPEARANCE);
        }

        public static class WALL_MATERIAL
        {
            public static LocString STEEL_NAME = new LocString("Steel", StringKeys.WALL_STEEL_NAME);

            public static LocString IGNEOUS_ROCK_NAME = new LocString(
                "Igneous Rock", StringKeys.WALL_IGNEOUS_ROCK_NAME);

            public static LocString NEUTRONIUM_NAME = new LocString(
                "Neutronium (unbreakable)", StringKeys.WALL_NEUTRONIUM_NAME);

            public static LocString GLASS_NAME = new LocString("Glass", StringKeys.WALL_GLASS_NAME);
        }

        /// <summary>
        /// 注册字符串树并按当前语言套用翻译。
        /// 必须在任何 PLib 选项 UI 创建之前调用（OnLoad 里最早执行）。
        /// </summary>
        internal static void Register()
        {
            // 1) 注册到游戏的可翻译树：生成 key、把英文源写进 Strings 表，
            //    并让游戏/PLib 后续的 OverloadStrings 能按 key 覆盖为译文。
            Localization.RegisterForTranslation(typeof(STRINGS));

            // 2) 立即按当前语言套用自带翻译（嵌入资源）。
            //    游戏加载 mod 早于 Localization.Initialize，此时 locale 还没建立，
            //    只能从 KPlayerPrefs 读取所选语言；英文（未选语言）时跳过即回落英文源。
            ApplyEmbeddedTranslation();

            // 3) PLib 的 mod 翻译加载器：游戏换语言（重启）后自动重载
            //    <mod>/translations/<code>.po，方便社区翻译与热更新。
            try
            {
                new PeterHan.PLib.Database.PLocalization().Register(typeof(STRINGS).Assembly);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CustomRocketInterior] PLocalization register failed: {e}");
            }
        }

        /// <summary>
        /// 读取嵌入资源 translations/&lt;code&gt;.po 并应用到字符串树。
        /// 找不到对应语言的资源时保持英文源文本（对所有语言都安全可读）。
        /// </summary>
        private static void ApplyEmbeddedTranslation()
        {
            try
            {
                string code = GetCurrentLanguageCode();
                if (string.IsNullOrEmpty(code))
                {
                    return;
                }

                // "zh_klei" 等预装语言代码归一到 "zh"
                string[] candidates = code.Contains("_")
                    ? new[] { code, code.Split('_')[0] }
                    : new[] { code };

                Assembly asm = typeof(STRINGS).Assembly;
                foreach (string candidate in candidates)
                {
                    string resource = "CustomRocketInterior.translations." + candidate + ".po";
                    using (Stream stream = asm.GetManifestResourceStream(resource))
                    {
                        if (stream == null)
                        {
                            continue;
                        }

                        using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
                        {
                            var lines = new System.Collections.Generic.List<string>(128);
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                lines.Add(line);
                            }

                            Localization.OverloadStrings(
                                Localization.ExtractTranslatedStrings(lines.ToArray(), false));
                        }

                        // OverloadStrings 只更新 LocString 字段，这里重建 Strings 表项，
                        // 让 PLib 的 LookInStrings(按 key 查表) 也能拿到译文。
                        LocString.CreateLocStringKeys(typeof(STRINGS), "CustomRocketInterior.");
                        Debug.Log($"[CustomRocketInterior] Applied translation: {candidate}.po");
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CustomRocketInterior] Failed to apply translation: {e}");
            }
        }

        private static string GetCurrentLanguageCode()
        {
            try
            {
                // 与 Localization.GetCurrentLanguageCode 等价，但避免在 OnLoad 阶段
                // 触发 UGC 语言包（Steam）查询；None（英文）时返回默认代码。
                string type = KPlayerPrefs.GetString("SelectedLanguageType", "None");
                if (string.Equals(type, "None", StringComparison.OrdinalIgnoreCase))
                {
                    return "en";
                }
                return KPlayerPrefs.GetString("SelectedLanguageCode", "en");
            }
            catch (Exception)
            {
                return "en";
            }
        }
    }

    /// <summary>
    /// 字符串 key 常量：[Option] 特性需要编译期常量，集中在此避免手写错漏。
    /// 命名规则见 STRINGS 类注释。
    /// </summary>
    internal static class StringKeys
    {
        private const string Root = "CustomRocketInterior.STRINGS.";

        public const string INTERIOR_WIDTH_NAME = Root + "OPTIONS.INTERIOR_WIDTH_NAME";
        public const string INTERIOR_WIDTH_TOOLTIP = Root + "OPTIONS.INTERIOR_WIDTH_TOOLTIP";
        public const string INTERIOR_HEIGHT_NAME = Root + "OPTIONS.INTERIOR_HEIGHT_NAME";
        public const string INTERIOR_HEIGHT_TOOLTIP = Root + "OPTIONS.INTERIOR_HEIGHT_TOOLTIP";
        public const string WALL_MATERIAL_NAME = Root + "OPTIONS.WALL_MATERIAL_NAME";
        public const string WALL_MATERIAL_TOOLTIP = Root + "OPTIONS.WALL_MATERIAL_TOOLTIP";
        public const string CATEGORY_SIZE = Root + "OPTIONS.CATEGORY_SIZE";
        public const string CATEGORY_APPEARANCE = Root + "OPTIONS.CATEGORY_APPEARANCE";

        public const string WALL_STEEL_NAME = Root + "WALL_MATERIAL.STEEL_NAME";
        public const string WALL_IGNEOUS_ROCK_NAME = Root + "WALL_MATERIAL.IGNEOUS_ROCK_NAME";
        public const string WALL_NEUTRONIUM_NAME = Root + "WALL_MATERIAL.NEUTRONIUM_NAME";
        public const string WALL_GLASS_NAME = Root + "WALL_MATERIAL.GLASS_NAME";
    }
}
