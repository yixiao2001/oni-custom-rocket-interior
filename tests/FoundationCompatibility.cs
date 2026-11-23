using System.Reflection;
using System.Runtime.Loader;

internal static class FoundationCompatibility
{
    // Run once per game version in a fresh process, using that version's own Harmony.
    internal static void Check(string modPath, string managedDirectory)
    {
        // The Unity build of Harmony uses Mono-only Reflection.Emit APIs by default.
        // On .NET 8 use its Cecil emitter and allow its in-memory patch-info serializer.
        AppContext.SetData("MonoMod.DMDType", "cecil");
        AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);
        string managed = Path.GetFullPath(managedDirectory);
        var loader = AssemblyLoadContext.Default;
        loader.Resolving += (_, name) => {
            string path = Path.Combine(managed, name.Name + ".dll");
            return File.Exists(path) ? loader.LoadFromAssemblyPath(path) : null;
        };
        var game = loader.LoadFromAssemblyPath(Path.Combine(managed, "Assembly-CSharp.dll"));
        var harmonyType = loader.LoadFromAssemblyPath(Path.Combine(managed, "0Harmony.dll"))
            .GetType("HarmonyLib.Harmony", true);
        var mod = loader.LoadFromAssemblyPath(Path.GetFullPath(modPath));
        var harmony = Activator.CreateInstance(harmonyType, "cri.tests.foundation");
        foreach (var type in mod.GetTypes().Where(t => t.Namespace == "CustomRocketInterior.Patches"
            && t.Name.Contains("FoundationPatch", StringComparison.Ordinal)))
        {
            var processor = harmonyType.GetMethod("CreateClassProcessor").Invoke(harmony, new object[] { type });
            processor.GetType().GetMethod("Patch").Invoke(processor, null);
        }

        var building = game.GetType("BuildingDef", true);
        var foundation = building.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "CheckFoundation").OrderByDescending(m => m.GetParameters().Length).First();
        var patched = (IEnumerable<MethodBase>)harmonyType.GetMethod("GetPatchedMethods").Invoke(harmony, null);
        if (!patched.SequenceEqual(new[] { foundation }))
            throw new Exception($"Expected a single prefix on the canonical foundation check: {foundation}");
        Console.WriteLine($"PASS: real game Harmony attached the foundation patch only to {foundation}");
    }
}
