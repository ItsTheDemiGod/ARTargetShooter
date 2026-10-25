# Scripts folders

- **Core/** – project-wide infrastructure and debug tools (e.g. `ARSessionDebugOverlay`).
- **AR/** – AR Foundation code: `PlaneDetectionController` (plane detection, count, visibility), `PlacementReticle` (screen-centre plane raycast), `TapToPlaceTestMarker` (temporary Phase 2 test tool).
- **Gameplay/** – targets, projectiles, hit detection, scoring logic.
- **UI/** – HUD, menus, game-over screen.

All scripts use the namespace `ARTargetShooter` and the new Input System only.
