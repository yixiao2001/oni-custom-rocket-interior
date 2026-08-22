using HarmonyLib;

namespace CustomRocketInterior.Patches
{
    /// <summary>
    /// 拦截内部模板缓存：游戏在创建/加载火箭内部世界时通过 TemplateCache.GetTemplate
    /// 按 YAML 路径取得模板（带进程级缓存），我们在返回前把目标模板重塑成配置尺寸。
    /// 由于容器是缓存的单例，重塑一次后整个存档周期生效；幂等保护保证重复获取安全。
    /// </summary>
    [HarmonyPatch(typeof(TemplateCache), nameof(TemplateCache.GetTemplate))]
    internal static class TemplateCache_GetTemplate_Patch
    {
        private static void Postfix(string templatePath, ref TemplateContainer __result)
        {
            try
            {
                Core.InteriorResizer.ApplyOverride(__result, templatePath);
            }
            catch (System.Exception e)
            {
                // 任何异常都不能打断游戏流程：最坏情况退回原生模板
                Debug.LogError($"[CustomRocketInterior] Template override failed for '{templatePath}': {e}");
            }
        }
    }
}
