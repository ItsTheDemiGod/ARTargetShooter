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
3. **Target generation  <-- NEXT** (prefabs, spawn on planes, floating motion, respawn)
4. Projectile and touch aiming (Rigidbody, tap to fire, gravity arc, optional swipe power)
5. Hit detection and feedback (collisions, particles, score, sounds)
6. Game loop and UI (start screen, timer, HUD, difficulty, game over/restart)
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

## Current status / next phase
Phase 2 complete and verified on device. Ready for Phase 3: Target generation. Waiting for user to say go.
