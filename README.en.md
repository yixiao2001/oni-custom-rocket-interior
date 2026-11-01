# Custom Rocket Interior (Oxygen Not Included)

English | [简体中文](README.md)

Customize the **interior size and wall material** of rocket habitat modules in *Oxygen Not Included*.

Tested on build U59-744825 (base game + *Spaced Out!* DLC).

## Features

- 🚀 **Fully customizable interior**: width/height from 12 to 96 tiles each (vanilla Spacefarer Module is only 12×11). The room automatically fills the entire rocket interior world.
- 🧱 **Four wall materials**: Steel / Igneous Rock / Neutronium / Glass — applied to both the wall-tile buildings and their backing cells.
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
| Interior height | 12–96 tiles | 40 | Height of the interior world |
| Wall material | Steel / Igneous Rock / Neutronium / Glass | Steel | Material of wall tiles + backing cells |

- The options UI follows the game language: English by default, Simplified Chinese when the game is set to Chinese (`translations/zh.po`);
- Options are re-read every time a new rocket interior is created — new rockets use them immediately;
- Larger interiors consume more of the global grid; when exhausted, the game reports "No free rocket interior";
- Only affects **newly built** rockets; existing ones are baked into the save.

## Removable walls and rebuilt supports (v0.4.0)

Two Construction options are enabled by default; changing them requires a restart:

- **Allow wall deconstruction** unlocks normal deconstruction of hull/window tiles, including Neutronium. It does not unlock the four fixed gas/liquid ports, interior door or control station. Existing interiors receive the permission when loaded. Turning it off does not restore removed walls.
- **Mount utilities on any solid tile** allows the six player-built cargo input/output fittings and power plug to use built solid tiles inside rockets: ordinary, insulated, metal and window tiles, for example. Rotation still determines the adjacent supporting side. Natural cells, mesh/airflow tiles and empty space are excluded. Rules outside rocket interiors remain unchanged.

Removing support marks a utility as unsupported and stops cargo exchange; the power plug disconnects from the rocket circuit. Rebuilding valid solid support restores operation. The four fixed ports stay at their original positions. Player-built cargo fittings access storage through the rocket itself and do not depend on the locations of those ports.

Removing hull tiles does not expand the interior world. Build a replacement enclosure before removing the old one if you want to keep it airtight. Disable the solid-tile option to restore original hull-only placement.

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

- Building requires .NET SDK 8 and U59 game assemblies (target: netstandard2.1); configure paths via the `ONI_MANAGED_DIR` / `ONI_MODS_DEV_DIR` environment variables or directly in the csproj;
- Inspect game code with: `ilspycmd -t TypeName <Managed dir>/Assembly-CSharp.dll`.

### Publishing to the Steam Workshop

```bash
./publish.sh    # build, run regression tests and create the release ZIP
```

Open Steam Library → Tools → **Oxygen Not Included Mod Uploader**. Select the existing
**Custom Rocket Interior** item (`3789310279`), choose Edit and set its data directory to `release/CustomRocketInterior/`.
Keep its title, description, preview and visibility. Do not create a new item.
`publish.sh` only prepares the package; it does not log in or upload.

PLib 4.25.0 is merged into the mod DLL to avoid binding to another mod's old standalone
`PLib.dll`. When manually upgrading, remove the old `PLib.dll` from this mod's folder
and keep `config.json`.

```
src/
├── ILRepack.targets             # merges PLib into the mod DLL
├── Mod.cs                        # entry point (read options → apply config → register options UI)
├── Strings.cs                    # localized UI strings (English source) + translation loader
├── Options/RocketInteriorOptions.cs  # PLib options (labels resolved via the string table)
├── Config/InteriorSizeConfig.cs  # runtime configuration
├── Core/InteriorResizer.cs       # core pure logic: template reshaping
├── translations/zh.po            # Simplified Chinese translation of the options UI
└── Patches/
    ├── TemplateCachePatch.cs         # intercepts template loading, applies reshaping
    ├── RevealInteriorWorldPatch.cs   # removes fog from interior worlds
    └── ApplyLiveOptionsPatch.cs      # applies fresh options before each interior creation
```

## Known Limitations

- Only affects newly built rockets; exterior visuals remain vanilla-sized (cosmetic only);
- The cluster-map footprint polygon grows with the new walls and may not match the exterior sprite;
- With wall deconstruction enabled, hull/window tiles (including Neutronium) can be deconstructed. Fixed gas/liquid ports and the door stay protected. Removing walls does not expand the world.

## License

MIT — see [LICENSE](LICENSE).

## v0.3.0 fixes

- Merge PLib to avoid an old standalone DLL being loaded first and crashing the options UI with `TMPro.FaceInfo` (issue #1).
- English/Chinese options labels follow the game language and preserve numeric material settings.
- Fix material-only changes being skipped and old vacuum cells remaining after shrinking cached templates.
- Normalize size limits (12–96) and invalid materials; preserve unrelated template caches when changing settings.
- Run template regression tests and merged-DLL checks before packaging. See release notes for actual UI/save validation.
