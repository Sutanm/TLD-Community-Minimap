# 社区HUD地图

A MelonLoader mod for The Long Dark that replaces the in-game map with community-made
region maps, shown either as a corner minimap or as a full-screen map — and that draws
the game's **complete** marker set on top of either one.

Targets **The Long Dark 2.55 / Unity 6**, MelonLoader 0.7.2, ModSettings 2.2.5.

## What it does

- **Two independent map layers.** The corner minimap and the full-screen map each choose
  their own source: the community artwork, or the game's own map. So you can keep the
  precise vanilla map in the corner and the community map on Tab, or any other pairing.
- **All the markers, not a fraction of them.** Markers are read from the game's own
  registry (`MapDetailManager.s_MapDetails`): **802** of them in Mountain Town, against
  the **162** the game actually draws. They are drawn on the community map too, which the
  game itself never does.
- **The game's five marker categories**, filterable individually: resources, structures,
  corpses, rock caches and spray-paint marks.
- **Place names**, read from the game's own map data and localised through its own
  localisation lookup.
- **Hover names.** Rest the pointer on an icon to see what it is.
- **Capture the real base map.** The game hands mods a 1024×1024 texture while its own
  map panel draws at 2048×2048. Press `P` on the map to capture the full-resolution
  image; it is saved and loaded automatically from then on, per region.
- **Zoom and pan** on the full-screen map, and a settable HUD position and size for the
  corner map.

## Requirements

- The Long Dark
- [MelonLoader](https://github.com/LavaGang/MelonLoader) 0.7.2
- [ModSettings](https://github.com/DigitalzombieTLD/ModSettings) 2.2.5

## Installation

1. Install MelonLoader and ModSettings.
2. Copy `CommunityMinimap.dll` into the game's `Mods` directory.
3. Create `Mods/CommunityMinimap/maps`.
4. Place region maps in that directory using the English internal filenames listed in
   [MAPS.md](MAPS.md).

The map images are deliberately not included in this repository. Obtain permission from
the original cartographers before redistributing map artwork.

## Controls

Every key is rebindable in ModSettings; these are the defaults.

| Key | Action |
|---|---|
| `M` | Open the full-screen map (while "take over the map key" is on) |
| `Esc` | Close the full-screen map |
| `X` | Show or hide the minimap |
| `Tab` | Cycle the view (off by default — enable "cycle view key" first) |
| `P` | **Capture the game's map** at full resolution |
| `F11` | Record a calibration point |

Mouse wheel zooms the full-screen map and the left button pans it, while "release mouse"
is on.

## A note on map sources

The community maps are hand-drawn and **not** drawn to a 1:1 scale with the game's own —
measured across five calibration points, the two axes differ by 16%. They are fitted *to*
the vanilla map, not the other way round, so the vanilla source stays exact and the
community source is as close as its calibration allows. If positions matter more than
appearance, use the vanilla source.

## Credits

- Community map artwork belongs to its original cartographers and is not part of this
  repository.
- The persistent Unity UI approach was informed by
  [MotionTracker](https://github.com/okclm/MotionTracker), particularly its Unity 6
  scene-lifetime handling.
- ModSettings is maintained by DigitalzombieTLD and contributors.

## For contributors

Internal documentation lives in [`docs/`](docs/INDEX.md):

- [Status and remaining work](docs/STATUS.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Pitfalls and hard rules](docs/PITFALLS.md) — read before changing anything
- [Performance notes](docs/PERFORMANCE.md)
- [Settings reference](docs/SETTINGS.md)

## Building

Install a .NET SDK capable of targeting .NET 6, launch the game once through MelonLoader
so that its IL2CPP assemblies are generated, and install ModSettings. Then run:

```powershell
./build.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\TheLongDark'
```

When `GameDirectory` is omitted the script checks Steam's standard location under
`Program Files (x86)`. A successful build copies the DLL into the game's `Mods`
directory. The script refuses to install while the game is running — the process is named
`tld`.
