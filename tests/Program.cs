using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using CustomRocketInterior.Config;
using CustomRocketInterior.Core;
using CustomRocketInterior.Options;
using TemplateClasses;

if (args.Length == 2 && args[0] == "--check-mod")
{
    using var pe = new PEReader(File.OpenRead(args[1]));
    var md = pe.GetMetadataReader();
    if (md.AssemblyReferences.Any(h => md.GetString(md.GetAssemblyReference(h).Name) == "PLib"))
        throw new Exception("Mod still references external PLib.dll and can bind to another mod's old copy.");
    foreach (var handle in md.TypeReferences)
    {
        var type = md.GetTypeReference(handle);
        if (md.GetString(type.Namespace) == "TMPro" && md.GetString(type.Name) == "FaceInfo")
            throw new Exception("Incompatible PLib: references the removed TMPro.FaceInfo type. Use netstandard2.1.");
    }
    if (!md.TypeDefinitions.Any(h => md.GetString(md.GetTypeDefinition(h).Name) == "PComboBox`1"))
        throw new Exception("Mod is missing its embedded PLib UI.");
    Console.WriteLine("PASS: embedded PLib UI, no external PLib dependency or legacy TMPro.FaceInfo reference.");
    return;
}

int checks = 0;
void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
TemplateContainer Sample() => new() {
    cells = new() { new(0, 0, SimHashes.Vacuum, 0, 0, null, 0), new(-5, -4, SimHashes.Steel, 293, 100, null, 0) },
    buildings = new() {
        new() { id = "RocketWallTile", location_x = -5, location_y = -4 },
        new() { id = "RocketControlStation", location_x = 0, location_y = -3 },
        new() { id = "RocketInteriorGasInputPort", location_x = 4, location_y = -4 },
        new() { id = "RocketInteriorGasOutputPort", location_x = -3, location_y = -4 },
        new() { id = "RocketInteriorLiquidInputPort", location_x = 4, location_y = 5 },
        new() { id = "RocketInteriorLiquidOutputPort", location_x = -3, location_y = 5 }
    },
    otherEntities = new() { new() { id = "ClustercraftInteriorDoor", location_x = -5, location_y = -3 } }
};
void Verify(TemplateContainer t, int width, int height, SimHashes material)
{
    int left = -(width / 2), bottom = -(height / 2), right = left + width - 1, top = bottom + height - 1;
    Check(t.cells.Count == width * height, "Cell count must equal the new bounds, including after shrinking");
    Check(t.cells.Select(c => (c.location_x, c.location_y)).Distinct().Count() == width * height, "No duplicate cells");
    foreach (var c in t.cells)
    {
        Check(c.location_x >= left && c.location_x <= right && c.location_y >= bottom && c.location_y <= top, "No cells outside resized bounds");
        bool edge = c.location_x == left || c.location_x == right || c.location_y == bottom || c.location_y == top;
        Check(c.element == (edge ? material : SimHashes.Vacuum), "Shell material/interior vacuum must match options");
    }
    Check(t.buildings.Where(b => b.id == "RocketWallTile").All(b => b.element == material), "Wall buildings must use selected material");
    Check(t.buildings.All(b => b.location_x >= left && b.location_x <= right && b.location_y >= bottom && b.location_y <= top), "Functional buildings inside bounds");
    Check(t.buildings.Where(b => b.id.Contains("Gas")).All(b => b.location_y == bottom), "Gas ports on bottom wall");
    Check(t.buildings.Where(b => b.id.Contains("Liquid")).All(b => b.location_y == top), "Liquid ports on top wall");
    var door = t.otherEntities.Single();
    Check(door.location_x == left && door.location_y == bottom + 1, "Door follows bottom-left corner");
}

// Reuse a single cached template through growth, shrinkage, odd dimensions and
// material-only changes, as happens when options change between new rockets.
var cached = Sample();
foreach (var size in new[] { (38, 38), (94, 94), (10, 10), (11, 39), (94, 10), (38, 38) })
foreach (var material in new[] { SimHashes.Steel, SimHashes.IgneousRock, SimHashes.Unobtanium, SimHashes.Glass })
{
    InteriorSizeConfig.WallElement = material;
    Check(InteriorResizer.Resize(cached, size.Item1, size.Item2), "Changed size/material must rebuild");
    Verify(cached, size.Item1, size.Item2, material);
    int refreshCount = cached.RefreshCount;
    Check(!InteriorResizer.Resize(cached, size.Item1, size.Item2), "Identical cache hit must skip rebuilding");
    Check(cached.RefreshCount == refreshCount, "Cache hit must avoid refresh work");
}
// A fresh template with matching bounds must still get the selected material.
var sameBounds = Sample();
sameBounds.cells.Add(new(-19, -19, SimHashes.Vacuum, 0, 0, null, 0));
sameBounds.cells.Add(new(18, 18, SimHashes.Vacuum, 0, 0, null, 0));
Check(InteriorResizer.Resize(sameBounds, 38, 38), "Matching original bounds must not bypass material override");
Verify(sameBounds, 38, 38, SimHashes.Glass);
var config = new RocketInteriorOptions { WorldWidth = -100, WorldHeight = 1000, WallMaterial = (WallMaterial)99 };
InteriorSizeConfig.Apply(config);
Check(InteriorSizeConfig.WorldSize.x == 12 && InteriorSizeConfig.WorldSize.y == 96, "Clamp config to advertised 12..96 range");
Check(config.WorldWidth == 12 && config.WorldHeight == 96 && config.WallMaterial == WallMaterial.Steel, "Normalize malformed config for subsequent comparisons");
Check(InteriorSizeConfig.WallElement == SimHashes.Steel, "Invalid material falls back to steel");
var unrelated = Sample();
InteriorResizer.ApplyOverride(unrelated, "expansion1::poi/interiors/example");
Check(unrelated.RefreshCount == 0, "Do not modify unrelated templates containing interiors/ in the middle");
InteriorResizer.ApplyOverride(unrelated, "expansion1::interiors/habitat_medium");
Verify(unrelated, 10, 94, SimHashes.Steel);
Check(cached.cells.Count == 38 * 38, "Changing another template leaves cached template intact");

InteriorBuildingPolicy.Apply(new RocketInteriorOptions());
foreach (string id in new[] { "RocketWallTile", "RocketEnvelopeWindowTile" })
{
    Check(InteriorBuildingPolicy.CanDeconstructWall(id, true), "Hull and window tiles can be deconstructed inside rockets");
    Check(!InteriorBuildingPolicy.CanDeconstructWall(id, false), "Do not unlock hull tiles outside rocket interiors");
}
foreach (string id in new[] { null, "", "Tile", "InsulationTile", "ClustercraftInteriorDoor", "RocketControlStation",
    "RocketInteriorGasInputPort", "RocketInteriorGasOutputPort", "RocketInteriorLiquidInputPort", "RocketInteriorLiquidOutputPort",
    "RocketInteriorGasInput", "RocketInteriorSolidOutput", "RocketInteriorPowerPlug" })
    Check(!InteriorBuildingPolicy.CanDeconstructWall(id, true), "Only the explicit hull/window whitelist may change deconstruction permissions");
Check(InteriorBuildingPolicy.RelaxMounting(true, true, true), "Relax tagged rocket-hull mounting in interiors");
Check(!InteriorBuildingPolicy.RelaxMounting(false, true, true), "Never relax mounting outside rockets");
Check(!InteriorBuildingPolicy.RelaxMounting(true, false, true), "Leave unrelated building placement rules alone");
Check(!InteriorBuildingPolicy.RelaxMounting(true, true, false), "Leave unrelated foundation tag requirements alone");
InteriorBuildingPolicy.Apply(new RocketInteriorOptions { AllowWallDeconstruction = false, AllowPortsOnAnySolidTile = false });
Check(!InteriorBuildingPolicy.CanDeconstructWall("RocketWallTile", true), "Disabled option restores wall protection");
Check(!InteriorBuildingPolicy.RelaxMounting(true, true, true), "Disabled option keeps original hull-only mounting");
Console.WriteLine($"PASS: {checks:N0} regression assertions (template cache, resize, materials, ports, door, config).");
