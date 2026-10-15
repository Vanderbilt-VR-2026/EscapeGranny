# Escape the Granny - Sprint 2

Escape the Granny is a two-player VR horror game for Meta Quest, inspired by *Granny* on Roblox. One player is Granny, who hunts. The other is the Prisoner, who has five minutes to find a key hidden somewhere in a dark house and unlock the front door before Granny catches them.

The project is built with **Unity 6.3 LTS (6000.3.23f1)**, URP and the **XR Interaction Toolkit 3.3.2** on OpenXR, and targets **Meta Quest 3 / Android**.

Find all presentation, all videos, and documentation here: https://drive.google.com/drive/folders/1F62pOISPDyBI4wjSDYxj0LnaDuqd0bOx?usp=drive_link 


## Getting Started

Install Unity 6000.3.23f1 through Unity Hub with **Android Build Support**, then clone the repo and open the project folder.

The build starts at `Assets/Scenes/Lobby Scene.unity`, which connects both players, then loads `Assets/House_Bishal.unity`. The opening screens (`Assets/_Game/Scenes/MainMenu.unity`) aren't connected to the lobby yet.

**Tools > Escape Granny** has **Build Main Menu** and **Build House**, which generate those scenes from code.

For multiplayer testing, run two headsets (or a headset and the Editor) from the Lobby on a network Photon can reach. Vanderbilt's guest Wi-Fi blocks it, so use a hotspot. On a Mac, use the XR Device Simulator or install the APK with Meta Quest Developer Hub.

See [CLAUDE.md](CLAUDE.md) for the full project guide.

## VR Controls and Interaction

Players point at menu buttons with the controller rays and press the trigger to select. In the house, the left stick moves, the right stick turns, and the grip button picks up interactable objects.

## Opening Screens

`Home > Choose Role > Granny Weapon Select or Prisoner Briefing > House`

The menu opens in a random room of the house and appears in front of the player. Role select shows the chosen character turning on a stand, and the screens have animations and sound.

## House and Interactables

- **The house:** textured, with horror lighting and flickering lights.
- **Furniture and interactables:** doors, a locker, cabinets and a chest you can open, plus the key, padlock and weapons.
- **Granny:** a 3D avatar.

## Networking

Two players join the same room with Photon Fusion 2. The lobby counts down, then loads the house for both.

## Sprint 2 Progress

Completed backlog items:

- **Networking:** Photon Fusion setup, avatar spawn and ownership, head and hand sync, and scene sync (#18, #65, #66, #67, #68)
- **House:** textures and materials, dark horror lighting with lamps in each room (#47, #43, #61, #87)
- **Interactables:** doors that open; a locker, cabinets and a chest (#48, #40)
- **Avatars:** Granny avatar (#36)
- **Opening screens:** a room setting, a 3D role select screen, sound and lighting effects, and the menu placed in front of the player (#89, #90, #100, #104)
- **Docs:** AI usage in the README (#84, #91)

## Sprint 2 Videos

### Opening Screens

[![Opening screens video](https://drive.google.com/thumbnail?id=1rGrUn9ug_bAE0DF3PVv4Oj6K_y5f43z9&sz=w800)](https://drive.google.com/file/d/1rGrUn9ug_bAE0DF3PVv4Oj6K_y5f43z9/view?usp=drive_link)

### Interactable Doors + Furniture

[![Interactable doors and furniture video](https://drive.google.com/thumbnail?id=1lI_uKqbw_icr41ZgJfXpmY30iGwZPjV8&sz=w800)](https://drive.google.com/file/d/1lI_uKqbw_icr41ZgJfXpmY30iGwZPjV8/view?usp=drive_link)

### Textures

[![Textures video](https://drive.google.com/thumbnail?id=1PVr5JyYRTwck39ozpls5Txtp8xmzbIPB&sz=w800)](https://drive.google.com/file/d/1PVr5JyYRTwck39ozpls5Txtp8xmzbIPB/view?usp=drive_link)

### Networking

[![Networking video](https://drive.google.com/thumbnail?id=1QogUwkwfvCt8CpEnhmJ5yaR6vNqyB95J&sz=w800)](https://drive.google.com/file/d/1QogUwkwfvCt8CpEnhmJ5yaR6vNqyB95J/view?usp=drive_link)

## Next Steps

We created a basic Prisoner avatar this sprint; it will be fully developed and added to the game in Sprint 3. Other main goals for Sprint 3:

- Connect the opening screens to the lobby, with roles claimed over the network
- The full round: five-minute timer, random spawns and key placement, Granny's map, hiding spots and creaky floors
- Win and lose conditions and their screens, including the locked front door
- Finish the furniture in every room, then add sound effects, music and animations
- Fix dizzying movement and VR bugs

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

## AI Usage

1. **Personal assistant or team member?** Everyone used AI as a personal assistant for their own tasks (the house, lighting, networking, assets, interactables and the opening screens). We decided what to build; the AI answered questions, wrote scripts and helped us debug. No one shared one AI as a team member.

2. **Which AI systems?** Mostly Claude, in three forms: the Claude Desktop, Claude Code (terminal & VSCode Extension), and Claude Design. One member tried GitHub Copilot in VS Code first but switched to Claude because it explained its decisions more clearly.

3. **AI assisting vs AI starting the work:** Most of the time we started the work ourselves (importing assets, building rooms, setting up the XR rig, importing the Photon template, modelling in Blender) and used AI to debug or finish it. Examples: textures that wouldn't load, merge conflicts, and the app freezing on the Unity logo because the camera had been deleted. The AI started the work from a prompt mainly for scripts: the house builder that generates the whole greybox from the floor plan, the door, chain and padlock behaviour, the horror lighting, and the redesigned main menu (new layout, 3D role select screen, animations, sound, and the menu placed in front of the player). We then ran the tools and tested in Unity and on the Quest.

4. **Example prompts:**
   - "How do I add 2-player networking with Photon Fusion?"
   - "Can we build the house with a script in Unity instead of building from scratch?"
   - "I'm frozen on the Unity logo screen when I build and run."
   - "How do I transfer my Blender files into Unity?"
   - "Explain the merge conflict."

5. **How AI connects to Unity:** No one used MCP. Most of the team worked through the Claude chat app: they uploaded scene files, errors, logs and screenshots, then dragged the scripts Claude wrote into the project and followed its steps in the Inspector. Claude Code (in the terminal and the VS Code extension) edits scripts directly in the project folder. We then run the tools and test in Unity.

## Sprint 2 - Meta Quest Build apk File


