# Escape the Granny — Project Guide

Two-player VR horror game for **Meta Quest** headsets, inspired by *Granny* on Roblox.
Vanderbilt CS 4249 (Fall 2026) group project. Read this before touching the project.
Team roles are in `README.md`. Design sources: the Project Proposal slides, the "VR Project Granny" rules
doc, and `floor_plan.pdf` (repo root) — if this file and those disagree, ask the team.

## The game in one minute
- **Two players, two roles.** One is **Granny** (hunter), one is the **Prisoner** (hunted). The prisoner is
  intentionally at a disadvantage.
- **Prisoner wins** by finding the key and unlocking the **front door** with it, then walking out.
- **Granny wins** by killing the prisoner. **Prisoner also loses** if the **5-minute timer** runs out.
- **Flow:** both headsets open on the same home screen → point at PLAY → pick a role portrait → START.
  A claimed role greys out for the other player, so the two always end up on opposite sides.
- Granny and prisoner each spawn in a **random room** (different rooms). The key spawns at a **random spot**
  from a fixed list of key locations.

### Granny
- Picks **one weapon** at the start from a wall (axe, knife, gun, bomb) and **can't change it**.
  - Axe / knife = near (must be within arm's reach; swing or thrust to kill).
  - Gun = far, limited ammo. Bomb = far, thrown at a target.
- Has full lighting. Moves at **0.75× speed**. Doesn't know where the key is.
- **Map button:** opens the house floor plan and shows where the prisoner is. Usable **once every 30 s**.
- **Milestone 2:** point at a room on the map to **teleport** there (30 s cooldown). Each teleport plays a
  **spatial sound** — loud if the prisoner is nearby, faint if far away.

### Prisoner
- No weapons or powers. Sees only through a **spotlight** (dark house).
- Opens doors (grab and twist the knob) and drawers (grab the handle and pull back), grabs the key,
  **hides** (under beds, in wardrobes, etc.), and inserts the key into the front door.

### Sound rules (important for stealth)
- Opening any door or drawer makes a sound. Dropping the key makes a sound.
- Certain floor areas **creak** when the prisoner walks on them (locations TBD).
- Windows and outside doors can't be opened, except the front door with the key.

### Milestones
- **Milestone 1:** home screen and role select, random key placement, weapon choice, creaky floors,
  doors and drawers, hiding, map with the prisoner's location, win and lose conditions, 5-minute timer.
- **Milestone 2:** Granny map teleport and spatial audio, five-day system, Granny stair lift.
- **Ideas backlog (not committed):** bear traps, stamina bar, prisoner assembles a shotgun,
  weapon cooldown, "don't move" mechanic, extra items (screwdriver, codes).

## Tech stack (don't change without asking the team)
| Thing | What we use |
|---|---|
| Unity version | **6000.3.23f1** (Unity 6.3). Install this exact version through **Unity Hub**, with **Android Build Support**. |
| Render pipeline | URP (Universal Render Pipeline) |
| VR interaction | **XR Interaction Toolkit (XRI) 3.3.2** on OpenXR. Use XRI components (XR Origin, XR Grab Interactable, XR Simple Interactable, sockets). Do **not** mix in Meta Interaction SDK rigs (OVRCameraRig, HandGrab), even though that package is installed. |
| Hand tracking | XR Hands 1.9 |
| Level building | ProBuilder, and `Assets/Editor/HouseBuilder.cs` for the house |
| Input | New Input System (`InputSystem_Actions.inputactions`) |
| Multiplayer | **Not decided yet.** Don't install a networking package on your own; raise it with Divija (networking lead). |
| Target device | Meta Quest (Android build) |

## Unity crash course (for first-timers)
- **Scene** (`.unity`): a level. Contains **GameObjects** (anything in the world).
- **Component:** something attached to a GameObject that gives it behaviour, e.g. a Collider,
  a Rigidbody (physics), an AudioSource, or your own C# script.
- **Prefab** (`.prefab`): a saved, reusable GameObject (e.g. a Drawer). Edit the prefab once and every
  copy updates. **Build things as prefabs**, not by editing the shared scene directly.
- **Scripts:** C# classes that inherit from `MonoBehaviour`. `Start()` runs once and `Update()` runs every
  frame. Public or `[SerializeField]` fields show up in the Inspector so designers can tweak them.
- **Editor windows:** Hierarchy (objects in the scene), Inspector (the selected object's components),
  Project (files), Scene (edit view), Game (play view), Console (errors and `Debug.Log` output).
- **Play mode:** changes you make while ▶ Play is on are **thrown away** when you stop. Stop first, then edit.
- **`.meta` files:** every asset has one, holding its ID. **Always commit them together with the asset.**
  A missing `.meta` breaks references for everyone else.
- `Library/`, `Logs/`, `Temp/` and `UserSettings/` are local caches and are git-ignored. Never commit them.
  If Unity acts weird, close it and delete `Library/`; it rebuilds itself.

## Folder layout
```
Assets/
  House_Bishal.unity        ← MAIN GAME SCENE (the shared scene everyone builds toward)
  Editor/HouseBuilder.cs    ← generates the greybox house (editor-only tool)
  Generated/HouseMaterials/ ← greybox materials made by HouseBuilder
  Scenes/                   ← older and sample scenes (SampleScene, "scene sept 8")
  Granny Project.unity      ← older scene
  Samples/                  ← imported XRI and XR Hands demos. Reference only; don't edit.
  XR/, XRI/, Oculus/, Settings/, Resources/ ← package and config files. Don't edit unless you know why.
floor_plan.pdf              ← the official house layout with measurements
```
New work goes in `Assets/_Game/` (create it if missing) with subfolders `Scripts/`, `Prefabs/`,
`Materials/`, `Models/`, `Audio/` and `Scenes/`. Put your personal test scene at `Assets/_Game/Scenes/Test_<YourName>.unity`.

## The house (greybox)
- **Don't place walls by hand.** Run **Tools → Escape Granny → Build House** in Unity. It builds everything
  under a GameObject called `House (generated)`: 3 levels, walls with door gaps, stairs, a roof, door slots,
  gameplay markers and an `EscapeTrigger` outside the front door. **Remove House** deletes it.
- ⚠️ **Rebuilding deletes and recreates `House (generated)`.** Anything you drag *inside* it is lost.
  Put furniture, props and scripts in a **separate root object** (e.g. `Props`, `Gameplay`) instead.
- To change the layout, edit the numbers in `BuildPlan()` in `HouseBuilder.cs` (keep them in sync with
  `floor_plan.pdf`), then rebuild.
- **Coordinates (meters):** (0, 0) is the south-west outer corner, +x is east, +z is north, +y is up.
  Floors are at y = −3 (basement), 0 (ground) and +3 (upper); the roof is at 6. The house is 18 × 14 m.
- **Markers** (empty objects with coloured labels in the Scene view) show where things go:
  `P1–P3` prisoner spawns (green), `G1–G3` Granny spawns (red), `K1–K15` key spots (yellow),
  `H1–H14` hiding spots (purple). Door slots (`D1_FrontDoor`, `U1_...`) are where door prefabs go;
  their X scale equals the door width.
- Stairs have 15 visual steps with **no colliders** plus one invisible ramp collider. Walking over steps in
  VR is bumpy and causes motion sickness, so keep it that way.
- Default test position for the player rig: the ground-floor hallway at about (7.5, 0, 6.5).

## Git workflow (Personal branch → sprint branch → main)
1. `git switch <your-branch>` (e.g. `divija-dev`, `bishal`, `julia-dev`, `pham_dev`, `yamilet-dev`).
2. Before starting, bring in the latest sprint work: `git fetch origin` then `git merge origin/sprint-<N>`.
3. Commit small and often with clear messages: `git add <files>` then `git commit -m "Add drawer prefab"`.
4. `git push`, then open a **Pull Request into `sprint-<N>`** on GitHub. At the end of a sprint, `sprint-<N>`
   is merged into `main` by PR. **Never push directly to `main`.**
5. Don't use `git add .` blindly. Check `git status` first so you don't commit accidental changes to
   `ProjectSettings/` or scenes you didn't mean to touch.

### Avoiding merge conflicts (Unity-specific, read this!)
- **Scene files can't really be merged by hand.** Only **one person edits `House_Bishal.unity` at a time**,
  so announce in the group chat before you do. Build and test your feature in **your own test scene**
  as a **prefab**, then drop the finished prefab into the main scene in one small commit.
- Unity often changes files just from opening a scene (`ProjectSettings/*.asset`, `Settings/*RPAsset.asset`,
  or re-saving a scene). If you didn't mean to change them, discard those changes: `git restore <file>`.
- If a `.unity` or `.prefab` file conflicts, pick one whole side: `git checkout --theirs <file>` (theirs)
  or `--ours` (yours), then `git add <file>`. Redo the lost changes by hand.

## Running on a Quest
- **Enable developer mode** on the headset (Meta Horizon phone app → Devices → Developer Mode).
- **Quick test in the Editor:** Windows can use **Quest Link** (USB-C cable or Air Link) and just press ▶ Play.
  **macOS has no Quest Link**, so Mac users must build to the headset or use XRI's **XR Device Simulator**
  (keyboard and mouse fake VR) inside the Editor.
- **Build to the headset:** plug in over USB and accept "Allow USB debugging" inside the headset.
  Then File → Build Profiles → **Android** → Switch Platform (first time only; it's slow) →
  make sure the right scene is in the Scene List → **Build And Run**.
- Check setup problems under Edit → Project Settings → **XR Plug-in Management** → Project Validation,
  and fix everything it flags.

## Coding conventions
- C#: `PascalCase` for classes, methods and public members; `camelCase` for private fields; one class per file,
  and the file name must match the class name (Unity requires this for MonoBehaviours).
- Prefer `[SerializeField] private` fields over `public` fields for Inspector values.
- Keep gameplay numbers (cooldowns, speeds, timer, ammo) as Inspector fields, not hard-coded, so
  designers can tune them: 30 s map cooldown, 0.75× Granny speed, 300 s round timer.
- Write role-specific code so it can later be driven by networking. Keep "who is Granny or the prisoner"
  in one place (e.g. a `GameManager`) instead of scattering checks.
- Use XRI events (`selectEntered`, `selectExited`, `activated`) for interactions instead of polling
  controller buttons in `Update()`.
- Editor-only tools go in an `Editor/` folder (they won't be included in builds).

## Open decisions (ask before assuming)
- Networking solution (Netcode for GameObjects vs Photon vs Meta Building Blocks).
- Where the creaky floor areas are; what the "five-day system" means; how the Granny stair lift works.
- Which build scene list is final (currently only `Scenes/SampleScene` is in Build Settings; it should
  become the home menu scene plus `House_Bishal`).

## Notes for Claude / AI assistants
- The team is new to Unity: explain Unity steps in terms of menu clicks and Inspector fields, not just code.
- Don't hand-edit `.unity` or `.prefab` YAML unless asked; tell the user what to do in the Editor instead.
- Never commit `Library/`, and never delete `.meta` files for assets that still exist.
- Before a destructive git operation (reset, force-push, deleting files), confirm with the user.
