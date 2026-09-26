# 社区HUD地图

An experimental MelonLoader mod that displays community-made region maps as a
corner minimap or an opaque full-screen map.

The project currently targets **The Long Dark 2.55 / Unity 6**, MelonLoader 0.7.2,
and ModSettings 2.2.5.

## Current status

- Version 0.4.7 adds a multi-region map catalog and automatic scene switching.
- `Tab` switches between the corner minimap and the opaque full-screen map.
- The full-screen map always fits the complete current-region image to the screen.
- Minimap UI size and local map zoom are independent settings.
- The player marker is a single soft compass pointer: a translucent dark position
  ring, warm center dot, and a configurable accent color. Coral red, wine red,
  lavender, teal, amber, and bright red presets are included.
- Mystery Lake (`LakeRegion`) retains its 0.3.3 player-position calibration.
- Other mapped regions can display their complete map, but intentionally hide the
  player marker until region-specific calibration data is available.
- The HUD uses a persistent Unity `Canvas` and `RawImage`. It avoids IMGUI texture
  drawing, which caused invalid IL2CPP texture handles during scene transitions.

## Requirements

- The Long Dark
- [MelonLoader](https://github.com/LavaGang/MelonLoader) 0.7.2
- [ModSettings](https://github.com/DigitalzombieTLD/ModSettings) 2.2.5

## Installation

1. Install MelonLoader and ModSettings.
2. Copy `CommunityMinimap.dll` into the game's `Mods` directory.
3. Create `Mods/CommunityMinimap/maps`.
4. Place region maps in that directory using the English internal filenames listed
   in [MAPS.md](MAPS.md).

The map images are deliberately not included in this repository. Obtain permission
from the original cartographers before redistributing map artwork.

## Controls

- `Tab`: switch between corner minimap and full-screen map.
- `Esc`: leave the full-screen map and return to the corner minimap.
- `F8`: temporarily show or hide the map UI.
- `F9`: record the current scene, map ID, world position, and heading to
  `Mods/CommunityMinimap/calibration_points.csv`.

Size, position, minimap opacity, full-screen background opacity, local zoom, marker
size, and key bindings are available through ModSettings.

## Building

Install a .NET SDK capable of targeting .NET 6, launch the game once through
MelonLoader so that its IL2CPP assemblies are generated, and install ModSettings.
Then run:

```powershell
./build.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\TheLongDark'
```

When `GameDirectory` is omitted, the script checks Steam's standard location under
`Program Files (x86)`. A successful build copies the DLL into the game's `Mods`
directory.

## Credits

- Community map artwork belongs to its original cartographers and is not part of
  this repository.
- The persistent Unity UI approach was informed by
  [MotionTracker](https://github.com/okclm/MotionTracker), particularly its Unity 6
  scene-lifetime handling.
- ModSettings is maintained by DigitalzombieTLD and contributors.
