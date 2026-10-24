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
1. **Project foundation  <-- CURRENT**
2. Environment detection (plane detection, visible planes, reticle, raycast)
3. Target generation (prefabs, spawn on planes, floating motion, respawn)
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
(none yet)

## Changelog
- **Phase 1** – CLAUDE.md, .gitignore: created project notes and Unity ignore rules; git initialised on branch main.

## Current status / next phase
Phase 1 in progress. Next: Phase 2 (only after the user confirms Phase 1 works on the phone).
