# Community HUD Map

[中文](README.md) · [Map availability](MAPS.md) · [Settings reference](docs/SETTINGS.md) · [Project status](docs/STATUS.md)

A MelonLoader map mod for *The Long Dark*. It combines community-made maps, captured vanilla maps, the player position, and game-registered markers in independently configurable mini-map and full-map views.

Current pre-release: **0.9.0**. Target environment: **The Long Dark 2.55 / Unity 6**, MelonLoader 0.7.2, and ModSettings 2.2.5.

## Highlights

- Independently select Auto, Community Map, or Vanilla Map for the mini-map and full map, with optional source-fallback blocking.
- Full map: game-map-key redirect, wheel zoom, left-drag panning, hover labels, optional player movement, and independent topmost behavior.
- Mini-map: corner/custom placement, size, opacity, zoom, marker size, and independent topmost behavior.
- Filter game markers by resources, structures, carcasses, rock caches, and spray marks. Game markers on community maps are off by default.
- Resource markers react to harvesting, random spawn groups, and regeneration. Live animals are not carcasses; large-animal carcass markers follow game-object creation and cleanup.
- Cave, mine, and large-interior maps can be enabled separately. Supported multi-floor spaces switch by spatial region.
- Complex maps without trustworthy calibration use a complete reference overview without a player pointer or dynamic resource markers.
- Charcoal surveys open the vanilla map by default; the mod full map and no-popup modes are also available.

## Installation

1. Install MelonLoader 0.7.2 and ModSettings 2.2.5.
2. Copy `CommunityMinimap.dll` to the game's `Mods` directory.
3. Copy `calibrations.json`, `probe-maps.json`, and `maps` to `Mods/CommunityMinimap`.
4. Start the game and configure Community HUD Map in ModSettings.

Use the release archive's directory layout. Cartographer **SValleyFire** has authorized this mod to redistribute the current community map assets. Attribution must be preserved; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). The images are not committed to this source repository.

## Default controls

| Action | Default |
|---|---|
| Open/close full map | Game map binding (usually `M`) |
| Temporarily hide mini-map | `X` |
| Cycle mini-map / full map / hidden | `V` |
| Close full map | `Esc` |
| Zoom / pan | Mouse wheel / left drag |
| Capture vanilla map (developer mode) | `P` |
| Record calibration point (developer mode) | `F11` |

Modifier shortcuts do not trigger single-key cycling; for example, `Ctrl+V` is not treated as `V`.

## Settings overview

The menu exposes 45 options in eight sections:

| Section | Controls |
|---|---|
| General | Community-map game markers, cave/interior maps, source fallback |
| Mini-map | Visibility, source, marker size, topmost layer, position, margin, size, opacity, zoom |
| Full map | Map-key redirect, source, marker size, topmost layer, pointer, background, mouse, movement, hints |
| Player pointer | Size and palette |
| Markers and labels | Data source, tint, outline, place names, font size, hover tooltip |
| Marker filters | Resources, structures, carcasses, rock caches, spray marks |
| Keys and game integration | Mini-map toggle, view cycling, charcoal-survey popup |
| Advanced and developer | Developer mode plus conditional capture, coordinates, and calibration tools |

See [the settings reference](docs/SETTINGS.md) for defaults and detailed behavior.

## Map availability

- **Outdoor maps: 22/22** have captured vanilla maps and coarse community-map calibration.
- **Cave/interior spaces: 44/44** have a resolved binding and release behavior. Stable spaces are calibrated; complex spaces that cannot be represented reliably by one plane use pointer-free reference mode.
- The Zone of Contamination concentrator has completed in-game review.
- Langston Mine and the large Far Territory connector cave intentionally omit player positioning because of height changes, floor overlap, and source-map distortion.
- A few maps, including Sundered Pass, remain approximate and are intended mainly for exits and major landmarks.

See [MAPS.md](MAPS.md) and the [calibration checklist](docs/COARSE-CALIBRATION-CHECKLIST.md) for the inventory and internal filenames.

## Accuracy and known limitations

Vanilla maps use the game's own projection and are the most reliable source. Community maps are hand-drawn and can contain non-uniform local stretching. A multi-point affine fit improves global direction and scale but cannot remove distortion in the artwork. Some interior images combine unrelated floors on one sheet, so the mod prefers a safe reference-only view over a misleading pointer.

Vanilla map images are not redistributed with the mod. Without a local capture, the mod reads the game's 1024×1024 region texture. This works as a fallback, but it contains less map content than the complete 2048×2048 panel map; the difference is not resolution alone. To create complete local maps, temporarily disable “Use mod full map,” open the vanilla map panel for about one second in each outdoor region, and then re-enable the mod full map. Captures are saved under `Mods/CommunityMinimap/captured` and used automatically. Developer-mode `P` can recapture the current panel manually.

The game may spawn renewable resources outside reachable terrain; there is no general reachability test yet. Full-map controller zoom and panning are also incomplete.

## Building and packaging

Launch the game once to generate IL2CPP assemblies and install ModSettings, then run:

```powershell
./build.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\TheLongDark'
./package-release.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\TheLongDark'
```

The build script refuses to overwrite a DLL while the game is running. The packaging script writes to `dist`, adds checksums, and never adds map assets to Git.

## Documentation and credits

Start at [docs/INDEX.md](docs/INDEX.md). The project code is released under the [MIT License](LICENSE). Special thanks to cartographer **SValleyFire** for creating the community maps and authorizing redistribution by this mod. Map assets remain the cartographer's property and are not covered by the MIT License. The persistent Unity UI lifecycle was informed by MotionTracker's Unity 6 handling. ModSettings is maintained by DigitalzombieTLD and contributors.
