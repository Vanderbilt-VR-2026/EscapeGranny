# Escape the Granny - Sprint 3

Escape the Granny is a two-player VR horror game for Meta Quest, inspired by *Granny* on Roblox. One player is Granny, who hunts. The other is the Prisoner, who has five minutes to find a key hidden somewhere in a dark house and unlock the front door before Granny catches them.

The project is built with **Unity 6.3 LTS (6000.3.23f1)**, URP and the **XR Interaction Toolkit 3.3.2** on OpenXR, and targets **Meta Quest 3 / Android**.

Find all presentation, all videos, and documentation here: https://drive.google.com/drive/folders/1F62pOISPDyBI4wjSDYxj0LnaDuqd0bOx?usp=drive_link 


## Getting Started

Install Unity 6000.3.23f1 through Unity Hub with **Android Build Support**, then clone the repo and open the project folder.

The build starts at the opening screens (`Assets/_Game/Scenes/MainMenu.unity`), which load the house (`Assets/House_Bishal.unity`) for one player. The networked lobby (`Assets/Scenes/Lobby Scene.unity`) isn't in the build right now; it will come back once the opening screens are connected to it.

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
- **Avatars:** a rigged Granny and an orange-jumpsuit Prisoner, both playing idle animations in the basement.
- **End of the round:** time-out screens for both roles, and a success sequence where the Prisoner escapes the house.

## Networking

Two players join the same room with Photon Fusion 2. The lobby counts down, then loads the house for both.

## Design

### Start Screen

**What the player sees:** the game opens with the player already standing inside Granny's house, in a dark room picked at random from the spawn rooms. The menu appears 2 m in front of them once the headset is tracking, facing the most open side of the room (if a wall is close, the menu moves in front of it and shrinks so it looks the same size). The title "Escape the Granny" flickers like a failing bulb with a red glow behind it, dust floats past, and a low drone with wind and a faint heartbeat plays underneath. The home screen has one line, "Two players. One key. Five minutes.", and a single PLAY button.

**Why:** we wanted the first thing the player feels to be the game itself, not a flat title card. Starting in the real house shows them where they'll be trapped, and because the room changes every time, the house never feels fully known. The flicker, sound and dim light set the horror tone before anyone presses a button, and the one-line tagline explains the whole game without a wall of text.

**What it tells the player:** this is a tense, dark, two-player game with a clear goal and a time limit. One person hunts and one hides, so the start screen leads straight into choosing a side.

**What's next:** PLAY opens role select. Granny ("The Hunter") and the Prisoner ("The Hunted") each have a box on the left, and the picked character turns on a stand on the right with three short lines about how they play. A role the other player has claimed greys out with a "TAKEN" stamp. After START:

- **Granny** picks one weapon (axe and knife are near, the gun is far with limited ammo, the bomb is thrown), then watches a short tutorial for that weapon.
- **The Prisoner** reads a briefing with the goal, the 5:00 time limit and stealth tips.

READY drops them into the house.

### Interactions

**Are we using the default settings?** Mostly, yes. We use the XR Interaction Toolkit's starter XR Origin: the left stick moves smoothly, the right stick turns, the grip button grabs and the trigger selects. That's the control scheme most Quest players already know, so nobody has to learn new controls in a horror game where they're already under pressure. It also let us spend our time on the house and the game instead of rebuilding the rig.

What we changed:

- **Turning:** smooth turning at 90° per second, faster than the default 60°, with snap turning off. Granny has to whip around fast when she hears a noise, and the Prisoner has to check behind them.
- **Comfort:** we fixed dizzying movement this sprint (#105). The stairs have one invisible ramp instead of 15 bumpy steps, and the rig steps up small ledges (0.1 m) so the camera doesn't jolt.
- **Holding items:** Granny's weapon starts in her right hand, but after her first squeeze it's a normal grab. Letting go drops or throws it, and she has to pick it back up.

**Distance grab or direct interaction?** We use both, for different things:

- **Menus use rays (distance).** The menu floats 2 m away, so the player points with a laser and pulls the trigger. Buttons grow and tick when the ray hovers them, so it's clear what you're about to press.
- **The house uses direct interaction.** You grab a door knob and twist it, pull a drawer handle, push the key into the padlock and swing the axe. Direct interaction is the better match for this game: having to walk up to things and physically reach for them is what makes searching the house tense. Distance grab would let the Prisoner pull the key from across the room and skip the danger. The starter rig still allows far grabbing by default, so next we're turning that off in the house so every grab has to be within arm's reach.

### Hands and Controllers

**What they look like:** both players see their own hands instead of controller models. The fingers close when you squeeze the grip and curl when you pull the trigger. Hands make grabbing a knob or a key feel natural, and they make the player feel like a person stuck in the house rather than someone holding a remote.

**Does it change through the experience?** Yes:

- **In the menu:** the hands also cast a laser ray for pointing at buttons.
- **In the house:** things are grabbed with the hand itself. Granny starts with her weapon in her right hand, and the Prisoner starts empty-handed, which fits their roles: one is armed and one has to search.
- **Next:** each role gets its own hands (the Prisoner's matching the orange-jumpsuit avatar, Granny's older and wrinkled), and the Prisoner will carry a spotlight as their only light. The other player already sees your head and hands move; in Sprint 4 they'll see your full avatar.

## Sprint 3 Progress

Completed backlog items:

- **Avatars:** the Prisoner avatar (Mixamo model, URP material, breathing idle) and a rigged Granny with an idle animation (#37, #123)
- **End of the round:** the time-runs-out failure screen, and the success screen joined to the house (#86, #128)
- **Granny:** a tutorial for each weapon, and an improved Granny spawn (#127, #129)
- **House:** textures and materials on the furniture, and interactable basement furniture and items for the Prisoner's success sequence (#47, #118)
- **VR comfort:** fixed dizzying movement (#105)

## Sprint 3 Videos

### Prisoner Character

[![Prisoner character video](https://drive.google.com/thumbnail?id=1H5j3w3-yu88Rt689mpg3N2b7PsvqBUhx&sz=w800)](https://drive.google.com/file/d/1H5j3w3-yu88Rt689mpg3N2b7PsvqBUhx/view?usp=drive_link)

### Granny Time Out (Lose)

[![Granny time out video](https://drive.google.com/thumbnail?id=1hDvRZvr4gagT1a_t-cyfgEWET_nj5cXv&sz=w800)](https://drive.google.com/file/d/1hDvRZvr4gagT1a_t-cyfgEWET_nj5cXv/view?usp=drive_link)

### Prisoner Time Out

[![Prisoner time out video](https://drive.google.com/thumbnail?id=13c3VZdtfxN0avPtd62PUmy93e4neqIpl&sz=w800)](https://drive.google.com/file/d/13c3VZdtfxN0avPtd62PUmy93e4neqIpl/view?usp=drive_link)

### Weapons Time Out

[![Weapons time out video](https://drive.google.com/thumbnail?id=1jX18TqekOSCyFvCWWCOWuGbxxxk-luf_&sz=w800)](https://drive.google.com/file/d/1jX18TqekOSCyFvCWWCOWuGbxxxk-luf_/view?usp=drive_link)

### Success Sequence: Prisoner Escapes the House

[![Prisoner success sequence video](https://drive.google.com/thumbnail?id=1S7r-0d9RUrznBYbPgrTs57r0MlyOVuCN&sz=w800)](https://drive.google.com/file/d/1S7r-0d9RUrznBYbPgrTs57r0MlyOVuCN/view?usp=drive_link)

## Next Steps

The Prisoner and Granny avatars are in the house as display models; in Sprint 4 they become the players' networked bodies. Other main goals for Sprint 4:

- Connect the opening screens and all scenes into one flow, with avatar spawn, ownership and scene sync over the network
- Win and lose conditions, including the kill failure sequence, connected to their screens
- Finish the round: Granny's map, hiding and movement animations for the Prisoner, creaky floors, and random item spawns
- Sound effects for interactables, background music, and the final game UI
- Build, test and playtest the full game loop on the Quest

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
https://drive.google.com/drive/folders/1fGFAT8R9jPw4YiYHkeoxlIzQ28Mq9Ea5?usp=drive_link

