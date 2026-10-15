# Escape the Granny — Project Guide

Two-player VR horror game for **Meta Quest** headsets, inspired by *Granny* on Roblox.
Vanderbilt CS 4249 (Fall 2026) group project. Read this before touching the project.
Team roles are in `README.md`. Design sources: the Project Proposal slides, the "VR Project Granny" rules
doc, and `floor_plan.pdf` (repo root) — if this file and those disagree, ask the team.

## The game in one minute
- **Two players, two roles.** **Granny** (hunter) and the **Prisoner** (hunted, intentionally disadvantaged).
- **Prisoner wins** by finding the key, unlocking the **front door** with it, and walking out.
  **Granny wins** by killing the prisoner. The prisoner also loses if the **5-minute timer** runs out.
- **Flow:** home screen → PLAY → pick a role portrait → START. A claimed role greys out for the other player.
- Granny and prisoner spawn in **different random rooms**; the key spawns at a random spot from a fixed list.
- **Granny:** picks **one weapon** (axe, knife = near; gun = far, limited ammo; bomb = thrown) and can't change
  it. Full lighting, **0.75× speed**, doesn't know where the key is. **Map button** shows the prisoner's
  location, **once every 30 s**. *Milestone 2:* point at a map room to **teleport** (30 s cooldown), with a
  **spatial sound** that is loud if the prisoner is near and faint if far.
- **Prisoner:** no weapons. Sees only by **spotlight**. Opens doors (grab and twist the knob) and drawers (pull
  the handle), grabs the key, **hides** (under beds, in wardrobes), and inserts the key in the front door.
- **Sound rules:** opening doors or drawers and dropping the key make sounds; some floor areas **creak**
  (locations TBD). Windows and outside doors can't be opened, except the front door with the key.
- **Milestone 1:** menu and role select, random key, weapon choice, creaky floors, doors and drawers, hiding,
  map, win and lose conditions, 5-minute timer. **Milestone 2:** map teleport and spatial audio, five-day
  system, Granny stair lift. **Backlog (not committed):** bear traps, stamina, shotgun parts, weapon cooldown.

## Tech stack (don't change without asking the team)
| Thing | What we use |
|---|---|
| Unity | **6000.3.23f1** (Unity 6.3), installed via **Unity Hub** with **Android Build Support** |
| Render pipeline | URP 17.3 |
| VR interaction | **XR Interaction Toolkit (XRI) 3.3.2** on OpenXR 1.16. Use XRI components (XR Origin, XR Grab/Simple Interactable, sockets). Do **not** use Meta Interaction SDK rigs (OVRCameraRig, HandGrab), even though `com.meta.xr.sdk.*` 205 is installed. |
| Hand tracking / input | XR Hands 1.9; New Input System 1.20 (`InputSystem_Actions.inputactions`) |
| Multiplayer | **Photon Fusion 2, Shared mode** (in `Assets/Photon/`) + **Photon Voice for Fusion** (git UPM package in `Packages/manifest.json`). Networking lead: Divija. Don't add other networking packages. |
| Level building | ProBuilder 6.1; editor tools in `Assets/Editor/` and `Assets/_Game/Editor/` |
| Target device | Meta Quest 3 (Android build) |

## Unity crash course (for first-timers)
- **Scene** (`.unity`) = a level full of **GameObjects**. **Components** (Collider, Rigidbody, AudioSource,
  your C# script) give them behaviour. **Prefab** (`.prefab`) = a reusable saved GameObject; build features
  as prefabs, not by editing the shared scene.
- Scripts inherit `MonoBehaviour` (`Start()` once, `Update()` every frame); networked scripts inherit Fusion's
  `NetworkBehaviour` (`Spawned()`, `FixedUpdateNetwork()`, `Render()`). `[SerializeField]` fields show in the Inspector.
- Changes made during ▶ Play mode are **thrown away**. Stop first, then edit.
- **`.meta` files** hold asset IDs. **Always commit them with the asset**; a missing `.meta` breaks references.
- `Library/`, `Logs/`, `Temp/`, `UserSettings/` are git-ignored caches. If Unity acts weird, close it and
  delete `Library/`.

## Folder layout
```
Assets/
  House_Bishal.unity          ← MAIN GAME SCENE (house, doors, lighting, "Gameplay" root)
  Scenes/Lobby Scene.unity    ← network lobby (Fusion Network Manager lives here)
  Scenes/InteractablesSprintOne.unity ← test scene for key, padlock, weapons
  Scenes/Game Scene.unity, SampleScene, "scene sept 8" ← old / template scenes, not in use
  _Game/                      ← team code: Scripts/Menu/, Editor/, Scenes/MainMenu.unity,
                                Settings/MenuTheme.asset, Fonts/, Materials/
  Scripts/                    ← gameplay scripts (interactables, doors, lights, network interactables)
  0 Prefabs/                  ← game-ready interactables: Key_Root, Padlock_Root, Bomb_Root, Bullet, flashes
  Fusion and Essential Spawned Player Stuffs/ ← network player, spawner/lobby prefab, hand presence
  Editor/HouseBuilder.cs      ← house generator + horror lighting tool
  Avatars/                    ← GrannyAvatar.fbx + GrannySkin.mat
  Photon/                     ← Fusion SDK + PhotonAppSettings/NetworkProjectConfig. Don't edit SDK code.
  ShootingDemo/, Photon/FusionDemos/ ← Fusion sample code; reference only
  1 Knife … 6 Axe, BathroomPack, Locker_HQ, RawWoodenFurnitureFree, Wooden Boxes, Yughues… ← store assets
  Samples/, XR/, XRI/, Oculus/, Settings/, Resources/ ← package samples and config; don't edit casually
```
New work goes in `Assets/_Game/` (`Scripts/`, `Prefabs/`, `Materials/`, `Models/`, `Audio/`, `Scenes/`).
Personal test scenes: `Assets/_Game/Scenes/Test_<YourName>.unity`.

## Scenes and build order
- **Build Settings (enabled):** index **0 = `Scenes/Lobby Scene`**, index **1 = `House_Bishal`**.
  `FusionPlayerSpawnerAndLobbyController` uses `LobbySceneBuildIndex = 0` and `GameSceneBuildIndex = 1`, so
  **reordering the build list breaks networking** unless you update the prefab's fields too.
- `_Game/Scenes/MainMenu.unity` (role and weapon menu) is **not yet wired into the networked flow**: it's
  disabled in the build list, `MenuController.StartGame()` only logs, and role claims use
  `LocalRoleClaimService` (Inspector checkboxes fake the other player). The plan is to replace it with a
  networked `IRoleClaimService` and merge the menu into the lobby flow.
- `GameSession` (DontDestroyOnLoad singleton) carries `SelectedRole` (`PlayerRole`) and `SelectedWeapon`
  (`WeaponType`) across scenes. This is the single source of truth for "who is Granny"; read it from there.

## Networking (Photon Fusion 2, Shared mode)
- **`FusionNetworkManager`** (Lobby scene, DontDestroyOnLoad) owns the `NetworkRunner`, joins session
  `"Room 1"` (MaxPlayers 2) in `GameMode.Shared`, and auto-reconnects by reloading the Lobby (runners are
  single-use). It also runs Photon Voice, which is **off while `AppIdVoice` is empty** in
  `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` (`AppIdFusion` is set; don't change it).
- The **shared-mode master client** spawns one **`Fusion Player Spawner and Lobby Controller`** prefab
  (DontDestroyOnLoad + `SharedModeStateAuthMasterClient`). It assigns players to slots in a networked array,
  runs the lobby countdown (30 s, or 5 s once the room is full), loads the game scene with
  `Runner.LoadScene`, and sends everyone back to the Lobby if only one player remains in the game.
- Each client spawns **its own** `Network Player` avatar (`FusionNetworkPlayer`), which copies the local
  rig's head and hands every tick and hides its own renderers locally.
- ⚠️ Scripts find the rig by name: **`GameObject.Find("XR Origin (XR Rig)/Camera Offset/Main Camera")`**
  (and Left/Right Controller). Every networked scene needs exactly one rig with these exact names.
- **Grabbable networked objects:** add `XRGrabNetworkInteractable` (needs NetworkObject, NetworkTransform,
  Rigidbody, XRGrabInteractable, XRGeneralGrabTransformer). Grabbing calls `RequestStateAuthority()`, so tick
  **Allow State Authority Override** on the NetworkObject. `XRSimpleNetworkInteractable` shows the RPC
  pattern (`[Rpc(RpcSources.All, RpcTargets.All)]`) for one-off events.
- Rules: only change `[Networked]` state when `HasStateAuthority`; use `Runner.SimulationTime` for shared
  timers (not `Time.time`); spawn with `Runner.Spawn` (not `Instantiate`) for anything others must see.
  Most gameplay scripts (`Door`, `Key`, `Padlock`, `Bomb`, `Gun`, `Scratcher`) are **still local-only
  MonoBehaviours** and need network versions before multiplayer play.

## Existing gameplay pieces (`Assets/Scripts/`)
- `Key` + `Padlock`: bring the key's Tip to the keyhole; the padlock seats the key model, turns, and toggles
  `IsLocked`. Keys match by ID (`Key.Matches(acceptedId)`), and `Key.All` lists every key.
- `Door` (implements `IInteractable.Interact()` to toggle): goes on the **hinge pivot**, not the mesh. It's
  used on 12 doors in `House_Bishal`.
- `Gun` (fires on XRI `activated`, spawns `Bullet` + muzzle flash), `Bomb` (pull the pin child to arm; the
  fuse starts on release; respawns at the hand), `Scratcher` (knife and axe leave scratch lines and gashes),
  `FlashEffect` (shared flash for muzzle, impact and explosion), `FlickerLight`, `CanvasController` (UI faces camera).

## Editor tools (Tools → Escape Granny)
- **Build House** / **Remove House**: generate or delete `House (generated)` (3 levels, walls with door gaps,
  stairs, roof, door slots, markers, `EscapeTrigger`). ⚠️ **Rebuilding deletes everything inside it.** Put
  props and scripts under separate roots (`House_Bishal` uses `Gameplay`). Change the layout in
  `BuildPlan()` in `HouseBuilder.cs`, keeping it in sync with `floor_plan.pdf`.
- **Apply Horror Lighting**: exponential fog, near-black ambient, weak moonlight, one dim point lamp per room
  (some with `FlickerLight`). Shadows are off because they're too expensive on Quest.
- **Build Main Menu** (`_Game/Editor/MainMenuSceneBuilder.cs`): regenerates `MainMenu.unity` from code,
  including the XR rig, panels, wiring, theme (`MenuThemeApplier` + `MenuTheme.asset`) and weapon models
  (`WeaponModelBuilder`). **Hand edits to that scene are lost on rebuild**, so change the builder or theme asset.

## The house
- **Coordinates (m):** (0, 0) is the south-west outer corner; +x east, +z north, +y up. Floors at y = −3
  (basement), 0 (ground) and +3 (upper); roof at 6. The house is 18 × 14 m. Test spot: hallway (7.5, 0, 6.5).
- **Markers:** `P1–P3` prisoner spawns, `G1–G3` Granny spawns, `K1–K15` key spots, `H1–H14` hiding spots.
  Door slots (`D1_FrontDoor`, `U1_...`) mark where door prefabs go (their X scale = door width).
- Stairs: 15 visual steps with **no colliders** plus one invisible ramp collider (avoids VR motion sickness). Keep it.

## Git workflow (personal branch → sprint branch → main)
1. Work on your branch (`divija-dev`, `bishal`, `julia-dev`, `pham_dev`, `yamilet-dev`).
2. Sync first: `git fetch origin` then `git merge origin/sprint-<N>`.
3. Commit small and often; `git add <files>` (check `git status`, never a blind `git add .`).
4. `git push`, then open a **PR into `sprint-<N>`**. Sprints merge into `main` by PR. **Never push to `main`.**
- **Scenes can't be merged by hand.** Only **one person edits `House_Bishal.unity` at a time** (announce it
  in the group chat). Build in your own test scene as a prefab, then drop it in with one small commit.
- Unity rewrites files just from opening things (`ProjectSettings/*.asset`, ProBuilder `Settings.json`,
  `Settings/*RPAsset.asset`, scenes). If you didn't mean to change them: `git restore <file>`.
- For a `.unity`/`.prefab` conflict, pick one side (`git checkout --theirs|--ours <file>`, then `git add`)
  and redo the lost changes by hand.

## Running on a Quest
- Enable Developer Mode (Meta Horizon phone app → Devices → Developer Mode).
- **Editor testing:** Windows can use Quest Link and press ▶ Play. **macOS has no Quest Link**: build to the
  headset or use XRI's **XR Device Simulator** (Package Manager → XR Interaction Toolkit → Samples).
- **Multiplayer testing** needs two clients in the same Fusion room (two headsets, or a headset plus the
  Editor). Start from the **Lobby Scene**, not `House_Bishal`, or no runner or avatar is created.
- **Build:** USB + "Allow USB debugging" → File → Build Profiles → **Android** → Switch Platform → check the
  Scene List → **Build And Run**. Fix anything flagged under Project Settings → XR Plug-in Management →
  Project Validation.

## Coding conventions
- `PascalCase` classes, methods and public members; `camelCase` private fields; one class per file, with the
  file name matching the class. Prefer `[SerializeField] private` over `public`.
- Gameplay numbers (30 s map cooldown, 0.75× Granny speed, 300 s timer, ammo) are Inspector fields, never hard-coded.
- Keep role logic centralized (`GameSession` now, a networked game manager later); don't scatter role checks.
- Use XRI events (`selectEntered`, `selectExited`, `activated`) instead of polling buttons in `Update()`.
- Short "why" comments in plain English, matching the existing scripts (the team is new to Unity and Fusion).
- Editor-only code goes in an `Editor/` folder.

## Open decisions (ask before assuming)
- How the menu, role claiming and weapon choice merge into the Fusion lobby; whether Photon Voice stays.
- Creaky floor locations; what the "five-day system" means; how the Granny stair lift works.

## Notes for Claude / AI assistants
- Explain Unity steps as menu clicks and Inspector fields, not just code.
- Don't hand-edit `.unity`/`.prefab` YAML unless asked; tell the user what to do in the Editor.
- Never commit `Library/`; never delete `.meta` files of existing assets; confirm before destructive git ops.
- Large script files: read `/// <summary>` headers first (most gameplay scripts document setup there).
