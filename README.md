# Community Minimap for The Long Dark

An experimental MelonLoader mod that displays a community-made Mystery Lake map as
an in-game minimap, with a moving player marker and configurable zoom.

The project currently targets **The Long Dark 2.55 / Unity 6**, MelonLoader 0.7.2,
and ModSettings 2.2.5.

## Current status

- Mystery Lake (`LakeRegion`) is the only supported region.
- The HUD uses a persistent Unity `Canvas` and `RawImage`. It intentionally avoids
  IMGUI texture drawing, which caused invalid IL2CPP texture handles during scene
  transitions on Unity 6.
- Player position uses a globally aligned transform plus a smooth local correction
  near the south railway car.
- The source community map is schematic rather than strictly 1:1. Landmark sizes
  and distances can be exaggerated, so perfect accuracy everywhere is not possible
  without additional regional control points.

## Requirements

- The Long Dark
- [MelonLoader](https://github.com/LavaGang/MelonLoader) 0.7.2
- [ModSettings](https://github.com/DigitalzombieTLD/ModSettings) 2.2.5

## Installation

1. Install MelonLoader and ModSettings.
2. Copy `CommunityMinimap.dll` into the game's `Mods` directory.
3. Create `Mods/CommunityMinimap/maps`.
4. Place the Mystery Lake map at:

   ```text
   Mods/CommunityMinimap/maps/神秘湖.jpg
   ```

The map image is deliberately not included in this repository. Obtain permission
from the original cartographer before redistributing map artwork.

## Controls

- `F8`: temporarily show or hide the minimap.
- `F9`: record the current scene, world position, and heading to
  `Mods/CommunityMinimap/calibration_points.csv`.
- Size, position, opacity, zoom, marker size, and key bindings are available through
  ModSettings.

## Building

Install the .NET 6 SDK, launch the game once through MelonLoader so that its IL2CPP
assemblies are generated, and install ModSettings. Then run:

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

