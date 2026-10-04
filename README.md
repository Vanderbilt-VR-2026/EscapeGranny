# Escape the Granny - Sprint 1

Escape the Granny is a two-player VR horror game for Meta Quest, inspired by *Granny* on Roblox. One player is Granny, who hunts. The other is the Prisoner, who has five minutes to find a key hidden somewhere in a dark house and unlock the front door before Granny catches them.

The project is built with **Unity 6.3 LTS (6000.3.23f1)**, URP and the **XR Interaction Toolkit 3.3.2** on OpenXR, and targets **Meta Quest 3 / Android**.

## Getting Started

Install Unity 6000.3.23f1 through Unity Hub with **Android Build Support**, then clone the repo and open the project folder.

The game has two main scenes:

- `Assets/_Game/Scenes/MainMenu.unity` is the opening screens. This is the first scene in the build.
- `Assets/House_Bishal.unity` is the greyboxed house where the game takes place. The menu loads it after both players are ready.

The interactables are set up in their own test scene, `Assets/Scenes/InteractablesSprintOne.unity`.

Two editor tools under **Tools > Escape Granny** generate the scenes, so the layout doesn't have to be built by hand:

- **Build Main Menu** creates `MainMenu.unity` with the XR rig, all four menu screens, the button wiring, the theme and the weapon models. Rebuilding replaces the scene, so make lasting changes in the builder code or in `Assets/_Game/Settings/MenuTheme.asset` instead of in the scene.
- **Build House** creates the house as `House (generated)` in the open scene, using the measurements from `floor_plan.pdf`. Running it again replaces the old house, and **Remove House** deletes it. Hide the **Roof** object to see inside, and move the XR Origin to `(7.5, 0, 6.5)` to start in the ground floor hallway.

Quest Link isn't available on macOS, so Mac users can test in the editor with the XR Device Simulator (Window > Package Manager > XR Interaction Toolkit > Samples > XR Device Simulator) or build to the headset.

For headset testing, connect a Quest with Developer Mode and USB debugging enabled, switch the build platform to Android under File > Build Profiles, and use **Build and Run**.

See [CLAUDE.md](CLAUDE.md) for the full project guide, coding conventions and Git workflow.

## VR Controls and Interaction

Players point at menu buttons with the controller rays and press the trigger to select. In the house, the left stick moves, the right stick turns, and the grip button picks up interactable objects.

## Opening Screens

Both headsets start on the same home screen. The flow is:

`Home > Choose Role > Granny Weapon Select or Prisoner Briefing > House`

- **Home** shows the title and a PLAY button.
- **Choose Role** shows Granny and Prisoner portraits. The player picks one and presses START. A role that the other player has already claimed is greyed out, so the two players always end up on opposite sides. For now, the other player is simulated with Inspector checkboxes on the `RoleClaimService` object; it will be replaced once networking is set up.
- **Granny Weapon Select** shows the axe, knife, gun and bomb as 3D models with a short description of each. The choice locks once Granny presses CONFIRM.
- **Prisoner Briefing** explains the goal, the five-minute timer and how to stay quiet, then the player presses READY.

The chosen role and weapon are stored in a `GameSession` object that carries over into the house scene. The screens follow the team moodboard: the Big Shoulders font and a blood red, dark brown, charcoal, navy and mustard palette.

## House and Interactables

The house is greyboxed from the team floor plan. It has three levels (basement, ground floor and upper floor), stairs, door openings, and markers for the Prisoner spawns, Granny spawns, key locations and hiding spots. Furniture has been placed in several rooms, and the remaining rooms will be finished in Sprint 2.

The interactables are the key, padlock, knife, pistol, bomb and axe. Each one can be grabbed with the controllers using XR Interaction Toolkit.

## Sprint 1 Progress

During Sprint 1 the team set up the project, agreed on the game rules and art direction, and built the first version of the house, the interactables and the opening screens.

Completed backlog items:

- #3 Unity empty scene
- #4 Team roles in the README
- #9 Moodboard
- #10 Game rules and mechanics
- #11 CLAUDE.md project guide for AI coding tools
- #17 Floor plan design
- #13 Greyboxed house map
- #5 Greyboxed furniture
- #34 House greybox
- #7 Interactables
- #12 Opening screens
- #20 Game rules slide for the presentation
- #21 Unity screenshots
- #22 Sprint 1 presentation
- #23 Sprint 1 video

Carried over into Sprint 2:

- #18 Setting up the networking package
- #16 Furniture greyboxing for the remaining rooms

## Sprint 1 Videos

### Greybox House

A walkthrough of the greyboxed house and furniture.

[![Greybox house video](https://drive.google.com/thumbnail?id=1vc2AHEuOnZKTBDxM_Eu0IFFg37xEO8kO&sz=w800)](https://drive.google.com/file/d/1vc2AHEuOnZKTBDxM_Eu0IFFg37xEO8kO/view?usp=drive_link)

### Interactables

Grabbing the key, padlock, knife, pistol, bomb and axe.

[![Interactables video](https://drive.google.com/thumbnail?id=1HBHId2_XSmQ3_IFp_nZ-LQ2k-Sd1evAQ&sz=w800)](https://drive.google.com/file/d/1HBHId2_XSmQ3_IFp_nZ-LQ2k-Sd1evAQ/view?usp=drive_link)

### Granny Opening Screen

Choosing the Granny role and picking a weapon.

[![Granny opening screen video](https://drive.google.com/thumbnail?id=10bGwrxnD9Pb3BjoyrA_Xd9RZZevLEWg7&sz=w800)](https://drive.google.com/file/d/10bGwrxnD9Pb3BjoyrA_Xd9RZZevLEWg7/view?usp=drive_link)

### Prisoner Opening Screen

Choosing the Prisoner role and reading the briefing.

[![Prisoner opening screen video](https://drive.google.com/thumbnail?id=1Jo0n6wcj2QOHHUwJAog0McpLKyQTpSke&sz=w800)](https://drive.google.com/file/d/1Jo0n6wcj2QOHHUwJAog0McpLKyQTpSke/view?usp=drive_link)

## Next Steps

Sprint 2 will focus on connecting two headsets and turning the house into a playable round. Planned work from the backlog:

- Networking, so two Quests can join the same game (#18)
- Furniture in every room, plus the key spots and hiding spots (#39, #44)
- Doors that can be opened (#48) and physics on the interactables and furniture (#40, #41, #35)
- Granny and Prisoner avatars and animations (#36, #37, #38, #49)
- Textures and materials for the furniture and assets (#47), and a horror atmosphere for the house (#43)
- The five-minute timer, random spawns and key placement, Granny's map, and the win and lose conditions

## Game Rules

- Two players: Granny and the Prisoner. Each round lasts five minutes.
- The Prisoner wins by finding the key and unlocking the front door. Granny wins by killing the Prisoner, and the Prisoner also loses if time runs out.
- Opening a door or drawer makes a sound. Dropping the key makes a sound. Some floor areas creak when the Prisoner walks on them.
- Windows and outside doors can't be opened, except the front door with the key.
- Granny picks one weapon at the start (axe, knife, gun or bomb) and can't change it. Granny has full lighting, moves at 0.75x speed, and can check the floor plan for the Prisoner's location once every 30 seconds.
- The Prisoner has no weapons, sees only by flashlight, and can hide under beds, in wardrobes and in other hiding spots.

## Team

| Team Member | Major(s) | Relevant Skills | Responsibilities |
| --- | --- | --- | --- |
| Divija Katakam | CS | Backend, Software Engineering | GitHub Manager<br>Asset Manager<br>Networking |
| Bishal Panthi | CS | Setting up environment, Modelling near & far interactive objects | Communications Director |
| Julia Zhang | CS | 3D modeling, Rendering | Asset Manager |
| Isabelle Pham | CS | UI / UX Design<br>Storyboarding<br>Moodboard / Palette | Design Director |
| Yamilet Pineda | CS | 3D-Modeling, Rendering, Programming | Design Director<br>Asset Manager |

## Sprint 1 - Meta Quest Build apk File

https://drive.google.com/file/d/1-J9EVaAf9kg6m4jvDG61llZsUFw22n6w/view?usp=drive_link