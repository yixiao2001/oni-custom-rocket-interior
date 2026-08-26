# Custom Rocket Interior (Oxygen Not Included)

English | [简体中文](README.md)

Customize the **interior size and wall material** of rocket habitat modules in *Oxygen Not Included*.

Tested on build U59-744825 (base game + *Spaced Out!* DLC).

## Features

- 🚀 **Fully customizable interior**: width/height from 12 to 96 tiles each (vanilla Spacefarer Module is only 12×11). The room automatically fills the entire rocket interior world.
- 🧱 **Four wall materials**: Steel / Igneous Rock / Neutronium (unbreakable) / Glass — applied to both the wall-tile buildings and their backing cells.
- 🌫️ **No fog of war** inside rocket interiors — no more leftover fog rings in the corners.
- ⚙️ **In-game options screen** (powered by PLib). Changes apply immediately to newly built rockets, no restart needed.
- 🚪 The interior door snaps to the bottom-left corner; gas/liquid ports and the control station are re-embedded into the new wall layout automatically.

## Installation

### Option 1: Steam Workshop (recommended)
Subscribe directly: [steamcommunity.com/sharedfiles/filedetails/?id=3789310279](https://steamcommunity.com/sharedfiles/filedetails/?id=3789310279)

### Option 2: Manual
1. Download the latest zip from [Releases](../../releases);
2. Extract into your local mods folder as a `CustomRocketInterior` subfolder:
   - Windows: `Documents/Klei/OxygenNotIncluded/mods/Local/`
   - Linux/macOS: `~/.config/unity3d/Klei/Oxygen Not Included/mods/Local/`
3. Enable the mod in Main Menu → Mods.

> Requires the *Spaced Out!* DLC. In a base-game-only configuration the mod correctly reports itself as incompatible.

## In-Game Options

Main Menu → Mods → Custom Rocket Interior → **Options**:

| Option | Range | Default | Description |
|---|---|---|---|
| Interior width | 12–96 tiles | 40 | Width of the rocket interior world |
| Interior height | 12–96 tiles | 40 | World height = value + 1 (2 safe rows at top); usable height = value − 4 |
| Wall material | Steel / Igneous Rock / Neutronium / Glass | Steel | Material of wall tiles + backing cells |

- Options are re-read every time a new rocket interior is created — new rockets use them immediately;
- Larger interiors consume more of the global grid; when exhausted, the game reports "No free rocket interior";
- Only affects **newly built** rockets; existing ones are baked into the save.

## How It Works

Based on reverse-engineering `Assembly-CSharp.dll`:

1. The rocket interior world size is controlled by `TUNING.ROCKETRY.ROCKET_INTERIOR_SIZE` (a public static mutable field, default 32×32) — simply assigning it is enough;
2. Interior layouts come from YAML interior templates (e.g. `expansion1::interiors/habitat_medium`, 12×11) loaded and cached by `TemplateCache.GetTemplate` — a Harmony postfix reshapes the template into a centered rectangle before it is returned: clear all shell layers, snap functional buildings onto the new walls, rebuild the perimeter and vacuum-fill the inside;
3. Vanilla only performs a single circular fog reveal after stamping; a postfix reveals every cell of the interior world instead.

See the source comments (header of `src/Core/InteriorResizer.cs`) for full details.

## Development

```bash
./build.sh      # compile + deploy into the game's Dev mods folder
./package.sh    # package an upload-ready workshop zip into release/
```
> ⚠️ **Publish only with the official tool**: Steam Library -> Tools -> "Oxygen Not Included - Mod Uploader".
> Uploading via raw steamcmd produces a loose file layout the game cannot read; every subscriber gets a download failure.

- Building requires .NET SDK 8 and a copy of the game; configure paths via the `ONI_MANAGED_DIR` / `ONI_MODS_DEV_DIR` environment variables or directly in the csproj;
- Inspect game code with: `ilspycmd -t TypeName <Managed dir>/Assembly-CSharp.dll`.

### Publishing to the Steam Workshop

The ONI client has no built-in Steam uploader, so this repo uses the official
steamcmd pipeline:

```bash
./publish.sh --upload <your-steam-username>
```

The script builds, stages a clean content folder plus workshop.vdf under
Windows `Documents/oni-upload/`, and invokes steamcmd. The first run creates
the workshop item (password + Steam Guard required); later runs update it by
reusing the stored publishedfileid.

```
src/
├── Mod.cs                        # entry point (read options → apply config → register options UI)
├── Options/RocketInteriorOptions.cs  # PLib options (with Chinese enum dropdown)
├── Config/InteriorSizeConfig.cs  # runtime configuration
├── Core/InteriorResizer.cs       # core pure logic: template reshaping
└── Patches/
    ├── TemplateCachePatch.cs         # intercepts template loading, applies reshaping
    ├── RevealInteriorWorldPatch.cs   # removes fog from interior worlds
    └── ApplyLiveOptionsPatch.cs      # applies fresh options before each interior creation
```

## Known Limitations

- Only affects newly built rockets; exterior visuals remain vanilla-sized (cosmetic only);
- The cluster-map footprint polygon grows with the new walls and may not match the exterior sprite;
- Outer walls are regular buildings (except Neutronium) and can be dug through, exposing the void at the world edge.

## License

MIT — see [LICENSE](LICENSE).
