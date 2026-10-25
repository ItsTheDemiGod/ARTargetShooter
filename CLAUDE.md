# AR Target Shooter

## Project summary
Unity Android AR game. Floating virtual targets appear in the real world through the phone camera. The player taps to fire projectiles at them and scores on hits.
Concepts: projectile motion, collision detection. Input: tap / touch.
Core steps: 1. Detect environment. 2. Generate targets. 3. Create projectile. 4. Aim using touch. 5. Detect hits.

## Tech stack
Unity 6.3 LTS, URP, AR Foundation, Google ARCore XR Plugin, Input System, Raycast, Rigidbody, Particle System.

## Target device
Samsung Galaxy S23 Ultra (Android, ARCore supported).

## Phase plan
0. Setup (done by user)
1. Project foundation (done)
2. Environment detection (done) (plane detection, visible planes, reticle, raycast)
3. Target generation (done) (prefabs, spawn on planes, floating motion, respawn)
4. Projectile and touch aiming (done) (Rigidbody, tap to fire, gravity arc, optional swipe power)
5. Hit detection and feedback (done) (collisions, particles, score, sounds)
6. **Game loop and UI  <-- NEXT** (start screen, timer, HUD, difficulty, game over/restart)
7. Polish and delivery (bug fixes, performance, final APK, demo video, report notes)

## Working rules
- Work only on the current phase. Never start the next phase on your own.
- Log every change in the Changelog below: phase number, files created/changed, and why.
- Anything the user must do manually (Unity editor, Inspector wiring, Package Manager, build settings, phone) goes under a "MANUAL STEPS FOR USER" heading in the final message, as numbered steps.
- Claude cannot operate the Unity editor. Never hand-edit .unity or .prefab YAML. Write C# scripts and explain how to attach/configure them.
- Do not edit Packages/manifest.json. The user installs packages via Package Manager.
- Every script must compile with Unity 6.3, use namespace ARTargetShooter, have explanatory comments, and use the new Input System (UnityEngine.InputSystem), never legacy UnityEngine.Input.
- Ask before deleting any file or running any command that changes things outside this project folder.

## Decisions
- Render pipeline: URP (Universal 3D template).
- Input: new Input System only; legacy UnityEngine.Input is never used.
- All scripts use namespace ARTargetShooter.
- Folder structure: Assets/_Project/{Scripts/{Core,AR,Gameplay,UI},Prefabs,Materials,Audio,Art,Scenes}.
- Git repo root is the Unity project folder (D:\ARTargetShooter\ARTargetShooter), where Assets/ lives.
- Unity version: 6.3 LTS (6000.3.26f1). Test device: Samsung Galaxy S23 Ultra.
- Graphics API: OpenGLES3 only (Vulkan removed) for AR background compatibility.
- PC_Renderer is the renderer used by the active quality level on Android, so the AR Background Renderer Feature lives there.
- Phase 2: AR Foundation 6 `ARPlaneManager.trackablesChanged` (never `planesChanged`); `ARRaycastManager` with `PlaneWithinPolygon`.
- Phase 2: touch via `UnityEngine.InputSystem.EnhancedTouch` (`Touch.onFingerDown`), enabled/disabled in OnEnable/OnDisable.
- Phase 2: plane detection mode Horizontal + Vertical.
- Phase 2: reticle and test marker are built at runtime from primitives (no prefab); materials use `Universal Render Pipeline/Unlit` with a fallback to the default primitive material.
- Phase 2: `Shader.Find("Universal Render Pipeline/Unlit")` at first returned a stripped shader in the build (purple cubes). Adding URP Unlit and Lit to Always Included Shaders (Project Settings > Graphics) fixed it. Any future runtime-created material must use a shader that is included in the build, ideally a Material asset referenced from the scene or a prefab.
- Phase 3: targets live in world space around the player (1.5-4 m from the camera) under a runtime "Targets" root, never parented to planes/trackables. A plane is only the start signal and a floor-height reference.
- Phase 3: Target prefab and Material assets are built by an Editor script (ARTargetShooter > Create Target Prefab). No runtime Shader.Find; runtime code only uses Material assets referenced from prefabs/serialized fields.
- Phase 3: physics layer "Target" for targets. `Target.Despawn(bool wasHit)` is the hook Phase 5 calls on a projectile hit.
- Phase 3: the user must disable the TapTestMarker GameObject in Unity before Phase 4 (and during Phase 3 testing).
- Targets float around the player in world space; a detected plane is only the start signal and floor reference.
- TapTestMarker must stay disabled from now on.
- The "Target" layer exists (layer 8). Phase 4 projectile and aim raycasts must use a layer mask that includes Target.
- Check `Target.IsAlive` before scoring a hit (a target stays hittable while shrinking).
- Projectiles should use Continuous Dynamic collision detection.
- Phase 4: new input scripts must use `TouchInputService.Acquire()/Release()` (reference-counted EnhancedTouch) instead of calling EnhancedTouchSupport directly.
- Phase 4: projectiles are Rigidbodies with gravity, ContinuousDynamic collision plus a sphere-cast safety between FixedUpdate positions, pooled via ProjectilePool.
- Phase 4: layer "Projectile"; the collision matrix makes projectiles ignore Default (AR planes), built-in layers and each other, but not Target. Projectile.Awake also calls Physics.IgnoreLayerCollision as a safety net.
- Phase 4: hits are only logged ("Projectile hit target: name at X m"). Phase 5 hook point: `Projectile.OnHitTarget` event and `Target.Despawn(true)`. `Target.IsHittable` (alive and past 60% of scale-in) guards hits.
- Phase 5 must subscribe to `Projectile.OnHitTarget` once per pooled projectile (not per shot), use the target's position for effects (the projectile is recycled right after the hit), re-check `Target.IsAlive` inside the handler, and call `Target.Despawn(true)`.
- TapTestMarker stays disabled.
- The hit log in Projectile.cs is `Debug.Log` only and is not visible on the phone screen.
- Phase 5: each pooled projectile is subscribed exactly once, via `ProjectilePool.OnProjectileCreated` (pool is prewarmed after ShootingController subscribes). `ShootingController.OnTargetHit` is the single forwarding event; other systems subscribe to it, not to projectiles.
- Phase 5: `ScoreManager` is the only thing that calls `Target.Despawn(true)` on a hit. Only wasHit=true scores; expiry (Despawn(false)) never scores or plays effects.
- Phase 5: scoring 10 points (red/white ring) and 25 (centre). Centre = hit point within `centreRadius` (0.05 m) of the target centre, measured in the target's plane (depth ignored). Thresholds are serialized fields.
- Phase 5: effects spawn at the target position captured before despawn (`OnTargetScored`).
- Phase 5: hit particle material, HitBurst prefab and the two WAV sounds are generated by an Editor menu (ARTargetShooter > Create Hit Effects Assets); the WAVs are synthesised from code. No runtime Shader.Find.
- Phase 6 requirements (planned, NOT done yet):
  - The yellow plane visuals, cyan reticle ring and yellow debug overlay are debug-only and must be hidden in the final game (use `PlaneDetectionController.SetPlanesVisible(false)` and a debug toggle); plane detection keeps running underneath.
  - `ShootingController` needs a reset method for `ShotsFired` so Misses does not carry over between rounds.
  - `TargetSpawner` needs `autoStart` turned off and public setters for difficulty (maxActiveTargets, target lifetime, respawn delay).
  - A real HUD (score, timer, start and restart buttons) needs a Canvas plus an EventSystem with the Input System UI module.
  - `ScoreManager.centreRadius` (currently 0.05) may be raised if centre hits feel too rare.

## Changelog
- **Phase 1** – CLAUDE.md, .gitignore: created project notes and Unity ignore rules; git initialised on branch main.
- **Phase 1** – Assets/_Project/** folders with .gitkeep: agreed folder structure.
- **Phase 1** – Assets/_Project/Scripts/Core/ARSessionDebugOverlay.cs: OnGUI overlay (ARSession.state, smoothed FPS, device model) to verify AR on the phone. Requires the AR Foundation package to compile.
- **Phase 1** – Assets/_Project/Scripts/Core/README.md: describes the Scripts subfolders.
- **Phase 1** – CLAUDE.md: filled in Decisions and status.
- **Phase 1 (verified)** – Unity configuration, verified on device: AR Foundation and Google ARCore XR Plugin installed; Input System set as the active input handler; Android platform; OpenGLES3 only; IL2CPP; ARM64 only; ARCore enabled in XR Plug-in Management; AR Background Renderer Feature added to PC_Renderer; scene Assets/_Project/Scenes/Main.unity with AR Session, XR Origin (Mobile AR) and a DebugOverlay object using ARSessionDebugOverlay. Files: ProjectSettings/, Packages/manifest.json + packages-lock.json, Assets/Settings/, Assets/XR/, Main.unity and .meta files.
- **Phase 1 (verified)** – .gitignore: added `.utmp/` (Android Gradle/CMake temp output).
- **Phase 2** – Scripts/AR/PlaneDetectionController.cs (new): sets Horizontal+Vertical detection, counts planes via trackablesChanged, exposes PlaneCount / OnPlaneCountChanged / SetPlanesVisible / PlanesVisible.
- **Phase 2** – Scripts/AR/PlacementReticle.cs (new): screen-centre plane raycast, reticle pose + HasValidPose/CurrentPose; runtime ring visual if none assigned.
- **Phase 2** – Scripts/AR/TapToPlaceTestMarker.cs (new): **temporary test tool**, to be removed or replaced in a later phase. Tap places a 5 cm cube at the reticle (max 20).
- **Phase 2** – Scripts/Core/ARSessionDebugOverlay.cs (changed): also shows plane count and reticle pose validity ("n/a" if missing).
- **Phase 2** – Scripts/Core/README.md (changed): describes the AR scripts.
- **Phase 2 (verified)** – Unity wiring, verified on device (planes, reticle "valid pose", tap-placed cubes, FPS ~30): AR Default Plane prefab (Assets/_Project/Prefabs) assigned to ARPlaneManager (Horizontal + Vertical); ARRaycastManager and PlaneDetectionController on XR Origin; Reticle object with PlacementReticle; TapTestMarker object with TapToPlaceTestMarker; Universal Render Pipeline Unlit and Lit added to Always Included Shaders (ProjectSettings/GraphicsSettings.asset). Files: Main.unity, AR Default Plane.prefab, script .meta files, GraphicsSettings.asset.
- **Phase 3** – Scripts/Gameplay/Target.cs (new): floating bullseye (bob + figure-eight drift in world space), faces camera around Y, scale-in/out, lifetime, Initialize/Despawn(bool wasHit)/OnDespawned/IsAlive, sets "Target" layer.
- **Phase 3** – Scripts/Gameplay/TargetSpawner.cs (new): spawns/respawns up to N targets around the camera, plane-count start signal, floor clamp from lowest HorizontalUp plane, separation check, public Start/Stop/ClearAll API and events.
- **Phase 3** – Scripts/Editor/TargetPrefabBuilder.cs (new, Editor-only): creates the "Target" layer, Target_Red/White/Center materials, Target.prefab; plus a Verify Phase 3 Setup menu.
- **Phase 3** – Scripts/Core/ARSessionDebugOverlay.cs (changed): added "Targets: n" line.
- **Phase 3** – Scripts/AR/TapToPlaceTestMarker.cs (changed): comment about disabling it before Phase 4.
- **Phase 3** – Scripts/Core/README.md (changed): Gameplay and Editor folders.
- **Phase 3 (verified)** – Unity wiring, verified on device (targets spawn after the first plane, "Targets: 5", correct colours, floating, facing the player, respawning, FPS ~30): Target prefab and the three materials built via ARTargetShooter > Create Target Prefab; "Target" layer added (ProjectSettings/TagManager.asset); TargetSpawner object with Target Prefab, AR Camera (Main Camera under XR Origin) and Plane Controller (XR Origin) assigned; TapTestMarker object disabled. Files: Target.prefab, Target_Red/White/Center.mat, Main.unity, TagManager.asset, URPProjectSettings.asset (Unity auto-updated), script/folder .meta files.
- **Phase 4** – Scripts/Core/TouchInputService.cs (new): reference-counted EnhancedTouchSupport enable/disable.
- **Phase 4** – Scripts/Gameplay/Projectile.cs (new): ballistic Rigidbody projectile, lifetime/distance limit, sphere-cast + collision hit detection, one hit per shot, OnHitTarget event.
- **Phase 4** – Scripts/Gameplay/ProjectilePool.cs (new): Queue-based pool under a runtime "Projectiles" root.
- **Phase 4** – Scripts/Gameplay/ShootingController.cs (new): tap to fire toward the touch point, gravity-compensation angle, cooldown, optional hold-to-charge, ShotsFired/OnFired/SetFireEnabled, "Fire Test Shot" context menu.
- **Phase 4** – Scripts/Editor/ProjectileSetupBuilder.cs (new, Editor-only): creates the Projectile layer, Projectile_Material, Projectile.prefab and the collision rules; Verify Phase 4 Setup menu.
- **Phase 4** – Scripts/Gameplay/Target.cs (changed): added IsHittable only.
- **Phase 4** – Scripts/Core/ARSessionDebugOverlay.cs (changed): added "Shots: n".
- **Phase 4** – Scripts/Core/README.md (changed).
- **Phase 4 (verified)** – Unity wiring, verified on device (tap fires arcing cyan projectiles, Shots count increments, projectiles ignore AR planes, no cubes on tap, FPS ~30): Projectile prefab and Projectile_Material built via ARTargetShooter > Create Projectile Prefab; "Projectile" layer added (layer 9); collision matrix saved in DynamicsManager.asset; ShootingController object with Projectile Prefab, AR Camera and Target Mask assigned. Files: Projectile.prefab, Projectile_Material.mat, Main.unity, TagManager.asset, DynamicsManager.asset, ProjectSettings.asset (Unity auto-updated), script .meta files.
- **Phase 5** – Scripts/Gameplay/Projectile.cs (changed): added `LastHitPoint`, set before OnHitTarget; hit path passes the contact point.
- **Phase 5** – Scripts/Gameplay/ProjectilePool.cs (changed): `OnProjectileCreated` event; constructor no longer prewarms, `Prewarm(int)` does.
- **Phase 5** – Scripts/Gameplay/ShootingController.cs (changed): subscribes once per pooled projectile, forwards hits via `OnTargetHit`, unsubscribes in OnDestroy.
- **Phase 5** – Scripts/Gameplay/ScoreManager.cs (new): scoring, Score/Hits/Misses, despawns the target, `OnTargetScored`/`OnScoreChanged`, `ResetScore()`.
- **Phase 5** – Scripts/Gameplay/HitEffects.cs (new): pooled particle burst + pop sound at the scored target position.
- **Phase 5** – Scripts/Editor/HitEffectsBuilder.cs (new, Editor-only): creates HitParticle_Material, HitBurst.prefab, HitSound.wav, CentreHitSound.wav; Verify Phase 5 Setup menu.
- **Phase 5** – Scripts/Core/ARSessionDebugOverlay.cs (changed): Score/Hits/Misses lines; label height computed from the text.
- **Phase 5** – Scripts/Core/README.md (changed).
- **Phase 5 (verified)** – Unity wiring, verified on device (shot targets despawn with burst and sound; overlay Score 90, Hits 6, Shots 9, Misses 3 = 4 normal hits x10 + 2 centre hits x25; expired targets do not score; FPS ~30): hit effects assets built via ARTargetShooter > Create Hit Effects Assets (HitParticle_Material, HitBurst.prefab, HitSound.wav, CentreHitSound.wav); ScoreManager object with Shooting Controller assigned; HitEffects object with Score Manager, HitBurst prefab, Hit Clip and Centre Hit Clip assigned. Files: Main.unity, the assets above, script .meta files.

## Current status / next phase
Phase 5 complete and verified on device. Ready for Phase 6: Game loop and UI. Waiting for user to say go.
