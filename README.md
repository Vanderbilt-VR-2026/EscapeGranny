# Escape the Granny - Sprint 2

Escape the Granny is a two-player VR horror game for Meta Quest, inspired by *Granny* on Roblox. One player is Granny, who hunts. The other is the Prisoner, who has five minutes to find a key hidden somewhere in a dark house and unlock the front door before Granny catches them.

The project is built with **Unity 6.3 LTS (6000.3.23f1)**, URP and the **XR Interaction Toolkit 3.3.2** on OpenXR, and targets **Meta Quest 3 / Android**.

Find all presentation, all videos, and documentation here: https://drive.google.com/drive/folders/1F62pOISPDyBI4wjSDYxj0LnaDuqd0bOx?usp=drive_link 

## AI Usage

### Julia:

1. I used it as my personal assistant.
2. I used Claude..
3. I started importing assets from the Unity store, and then used Claude to help me figure out why the textures weren't loading. I tried to merge, but there was a conflict, which I then asked Claude to explain. However, I did not have AI begin any work in Unity for me..
4. Is this edit supposed to be in the gitignore file, and which lines should I keep in this merge?.
5. Haven't gotten there yet..

### Pham:

1. Did I use the AI as a personal assistant or as a team member?
 I used it as a personal assistant. I decided what we were building: the escape room, the layout, which door and which assets to use. Claude answered my questions, wrote scripts, and helped me debug when something broke. In a few places it felt more like a teammate, such as figuring out why the headset froze on the Unity logo, where we went back and forth until we found the cause. I didn't share the AI with my teammates.

2. Roughly how many and which AI systems did I use?
 I started with the built in Copilot inside VS Code/GitHub but it was very inefficient and didn't clearly explain why it was making some decisions. So I switched to Claude which worked a lot better.

3. When did I start something in Unity and have the AI assist or finish it, versus giving a prompt and having the AI do the work?
 The AI never worked inside Unity itself; I did all the work in the editor.

I started in Unity, and the AI assisted: I imported the environment pack, built the foundation and room, picked and placed the door, and set up the VR rig. When something went wrong I uploaded my scene file or error text, and Claude helped me work out what to fix. For example, it compared my saved scenes and found that I had accidentally deleted the camera, which was why the app froze on the Unity logo.
I gave a prompt, and the AI wrote it: the scripts. I described what I wanted, such as the chain falling and the door opening, the padlock hanging from the chain, or extra chains across the door, and Claude wrote the code. I then added it to my project and hooked it up in Unity.


4. A few examples of prompts I gave

"there are some bushes in front of my room, how do i delete them"
"im frozen on the unity logo screen when i build and run"
"how do i make it so that no matter where i am real life i appear inside the room?"


5. How I connected the AI with Unity
 I didn't connect it directly. There's no MCP, no IDE plugin, and no command-line link between the AI and Unity. I worked manually:

I uploaded my scene files, error messages, logs, and screenshots to Claude.
Claude sent back script files, and I dragged them into my Assets/Scripts folder in Unity.
For setup steps, Claude gave me instructions that I followed in the Inspector.
I used Terminal for Git and GitHub, and Claude explained the commands.
I used Visual Studio Code as my script editor, but it wasn't connected to the AI. I liked this process because I still understood each step going through and implementing it myself. (edited)

### Bishal:

1. Used it as my personal assistant..
2. I used Claude (chat app). I also installed Claude Code in VS Code but haven't used it for project work yet..
3. Instead of building the greybox by hand, Claude wrote an editor script (HouseBuilder.cs) that builds the whole house from the floor plan numbers. The AI never worked inside Unity directly; I ran everything..
4. My prompts:.
    After I clicked Build and Run, nothing happens in VR.
    Can we build the house with a script in Unity instead of building from scratch?
5. Not used these yet.
   
### Divija (add here):

### Yamilet:

1. I used it as my personal assistant. I gave it prompts and it helped me understand issues I was running into and how to implement my ideas...
2. I used Claude..
3. Instead of starting in Unity, I started in Blender and used the agent to determine how to make the model compatible for Unity, and how to edit Unity Asset Store assets in Blender to be able to later configure physics in Unity..
4. Example of prompts I gave the agent:.
◦ How do I transfer my blender files into Unity?.
◦ Is it possible to edit Unity Asset Store assets in Blender?.
◦ How do I bake lighting without generating the bake?.
5. I did not necessarily connect AI with Unity. Instead, I asked the agent how to ensure the work I was doing would be compatible with the assets and code already pushed to Github..

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
