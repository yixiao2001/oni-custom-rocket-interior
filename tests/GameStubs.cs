// Minimal data-only game types. These tests exercise the shipped resizing/config
// sources without starting Unity; UI and world creation still need in-game checks.
public struct Vector2I(int x, int y) { public int x = x, y = y; }
public enum SimHashes { Vacuum, Steel, IgneousRock, Unobtanium, Glass, Cuprite }
namespace UnityEngine
{
    public static class Mathf { public static int Clamp(int v, int min, int max) => Math.Clamp(v, min, max); }
    public static class Debug { public static void Log(object message) { } public static void LogError(object message) => throw new Exception(message.ToString()); }
}
namespace TemplateClasses
{
    public class Cell(int x, int y, SimHashes element, float temperature, float mass, object disease, int count)
    {
        public int location_x = x, location_y = y;
        public object disease = disease; public int count = count;
        public SimHashes element = element;
        public float temperature = temperature, mass = mass;
    }
    public class Prefab
    {
        public enum Type { Building }
        public string id;
        public int location_x, location_y;
        public SimHashes element;
        public float temperature;
        public Type type;
    }
}
public class TemplateContainer
{
    public string name = "test";
    public List<TemplateClasses.Cell> cells = new();
    public List<TemplateClasses.Prefab> buildings = new(), otherEntities = new();
    public int RefreshCount;
    public void RefreshInfo() => RefreshCount++;
}
namespace PeterHan.PLib.Options
{
    [AttributeUsage(AttributeTargets.Class)] public class ConfigFileAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class OptionAttribute(string name, string tooltip, string category = null) : Attribute { public string Name = name, Tooltip = tooltip, Category = category; }
    [AttributeUsage(AttributeTargets.Property)] public class LimitAttribute(double min, double max) : Attribute { public double Min = min, Max = max; }
}
namespace CustomRocketInterior
{
    internal static class StringKeys
    {
        public const string INTERIOR_WIDTH_NAME = "Width", INTERIOR_WIDTH_TOOLTIP = "Width tooltip",
            INTERIOR_HEIGHT_NAME = "Height", INTERIOR_HEIGHT_TOOLTIP = "Height tooltip",
            WALL_MATERIAL_NAME = "Wall", WALL_MATERIAL_TOOLTIP = "Wall tooltip",
            CATEGORY_SIZE = "Size", CATEGORY_APPEARANCE = "Appearance",
            WALL_STEEL_NAME = "Steel", WALL_IGNEOUS_ROCK_NAME = "Igneous Rock",
            WALL_NEUTRONIUM_NAME = "Neutronium", WALL_GLASS_NAME = "Glass";
    }
}
