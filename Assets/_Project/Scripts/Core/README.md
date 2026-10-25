# Scripts folders

- **Core/** – project-wide infrastructure and debug tools (`ARSessionDebugOverlay`, `TouchInputService` – reference-counted EnhancedTouch; new input scripts must use it).
- **AR/** – AR Foundation code: `PlaneDetectionController` (plane detection, count, visibility), `PlacementReticle` (screen-centre plane raycast), `TapToPlaceTestMarker` (temporary Phase 2 test tool).
- **Gameplay/** – `Target` (floating bullseye) and `TargetSpawner` (keeps N targets around the player), `Projectile`, `ProjectilePool`, `ShootingController` (tap to fire), `ScoreManager` (scores hits, despawns hit targets), `HitEffects` (burst + sound on a scored hit).
- **Editor/** – editor-only tools, e.g. `TargetPrefabBuilder`, `ProjectileSetupBuilder` and `HitEffectsBuilder` (ARTargetShooter menu).
- **UI/** – HUD, menus, game-over screen.

All scripts use the namespace `ARTargetShooter` and the new Input System only.
