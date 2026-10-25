# Scripts folders

- **Core/** – project-wide infrastructure and debug tools (e.g. `ARSessionDebugOverlay`).
- **AR/** – AR Foundation code: `PlaneDetectionController` (plane detection, count, visibility), `PlacementReticle` (screen-centre plane raycast), `TapToPlaceTestMarker` (temporary Phase 2 test tool).
- **Gameplay/** – `Target` (floating bullseye) and `TargetSpawner` (keeps N targets around the player); later projectiles, hit detection and scoring.
- **Editor/** – editor-only tools, e.g. `TargetPrefabBuilder` (menu ARTargetShooter > Create Target Prefab).
- **UI/** – HUD, menus, game-over screen.

All scripts use the namespace `ARTargetShooter` and the new Input System only.
