# Scripts folders

- **Core/** – project-wide infrastructure and debug tools (`ARSessionDebugOverlay`, `TouchInputService` – reference-counted EnhancedTouch; new input scripts must use it).
- **AR/** – AR Foundation code: `PlaneDetectionController` (plane detection, count, visibility), `PlacementReticle` (screen-centre plane raycast), `TapToPlaceTestMarker` (temporary Phase 2 test tool).
- **Gameplay/** – `Target` (floating bullseye) and `TargetSpawner` (keeps N targets around the player), `Projectile`, `ProjectilePool`, `ShootingController` (tap to fire), `ScoreManager` (scores hits, despawns hit targets), `HitEffects` (burst + sound on a scored hit), `GameManager` (Menu/Playing/GameOver loop, timer, difficulty, best score).
- **Editor/** – editor-only tools, e.g. `TargetPrefabBuilder`, `ProjectileSetupBuilder` `HitEffectsBuilder` and `GameUIBuilder` (ARTargetShooter menu).
- **UI/** – `GameUI` (menu, HUD and game-over panels with TextMeshPro) and `LongPressButton` (hidden debug toggle).

All scripts use the namespace `ARTargetShooter` and the new Input System only.
