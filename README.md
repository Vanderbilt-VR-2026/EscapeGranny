# Escape the Granny - Sprint 4

Escape the Granny is a two-player VR horror game for Meta Quest, inspired by *Granny* on Roblox. One player is Granny, who hunts. The other is the Prisoner, who has five minutes to find a key hidden somewhere in a dark house and unlock the front door before Granny catches them.

The project is built with **Unity 6.3 LTS (6000.3.23f1)**, URP and the **XR Interaction Toolkit 3.3.2** on OpenXR, and targets **Meta Quest 3 / Android**.

Find all presentation, all videos, and documentation here: https://drive.google.com/drive/folders/1F62pOISPDyBI4wjSDYxj0LnaDuqd0bOx?usp=drive_link 


## Getting Started

Install Unity 6000.3.23f1 through Unity Hub with **Android Build Support**, then clone the repo and open the project folder.

The build starts in the networked lobby (`Assets/Scenes/Lobby Scene.unity`). Once two players join, both claim their roles in the start menu and then load into the house (`Assets/House_Bishal.unity`). The start menu (`Assets/_Game/Scenes/MainMenu.unity`) can also be opened on its own to test with one headset. **Tools > Escape Granny > Build Main Menu** and **Build House** generate those scenes from code.

For multiplayer testing, run two headsets (or a headset and the Editor) on a network Photon can reach. Vanderbilt's guest Wi-Fi blocks it, so use a hotspot. On a Mac, use the XR Device Simulator in the Editor (it is stripped from headset builds automatically), or install the APK with Meta Quest Developer Hub.

See [CLAUDE.md](CLAUDE.md) for the full project guide.

## Sprint 4 Progress

- **Start menu:** the menu now covers a whole wall of the living room, with a bigger title and less text on every screen (professor feedback). Role select shows the Prisoner model, and roles are claimed over the network: once one player takes a role, it greys out for the other.
- **Networking:** each player sees the other's full Granny or Prisoner body, Granny's chosen weapon appears in her hand for both players, and hiding is synced.
- **Hiding:** the Prisoner can hide (for example under a bed), and the other player can't see them while hidden.
- **Combat:** the Prisoner has health, each weapon (gun, knife, axe, bomb) does its own damage, every hit sprays blood, and the Prisoner falls over on death.
- **Round:** a 5:00 timer at the top of each player's view, which turns red in the last minute, and a round end screen.
- **Audio:** 3D sound for footsteps, weapons, dropped items, doors and drawers. Sound is muffled through floors, so Granny can hear the Prisoner moving upstairs.
- **Players:** Granny sees her own hands instead of controllers, and the Prisoner sees the house through a dark flashlight-style vignette.
- **House and performance:** the living room, kitchen, sewing room and basement are furnished, the walls have blood and grime, and the lighting is darker. The frame rate on Quest improved by removing real-time shadows and high-poly items, turning on foveated rendering, and hiding the outdoors until the escape.
- **Escape sequence (in progress):** a locked chest with a VR keypad. Entering the right code opens the lid, and the front-door key is inside.

## Interactions

- **Controls:** the starter XR rig, which most Quest players already know. Left stick moves, the right stick now **snap-turns** (smooth turning made players dizzy), grip grabs and trigger selects.
- **Menus and the keypad use rays.** The menu wall is 4.5 m away, so players point and pull the trigger. The keypad's buttons have slightly larger invisible hit areas than they look, so they're easy to hit with the ray.
- **The house uses direct interaction.** Players walk up to doors, drawers, the key and weapons and grab them by hand. Having to physically reach for things is what makes searching the house tense.
- **Comfort:** the player's head is placed on the spawn point when the round starts, so standing off-centre in the play area can't leave them inside a wall.

## Sprint 4 Videos

### Main Menu

[![Main menu video](https://drive.google.com/thumbnail?id=1l48afC_PCcg8go1RJkrWUxZlr8F0GyFe&sz=w800)](https://drive.google.com/file/d/1l48afC_PCcg8go1RJkrWUxZlr8F0GyFe/view?usp=drive_link)

### Prisoner Hiding

[![Prisoner hiding video](https://drive.google.com/thumbnail?id=15JTHL7xCcJZ4Rk8evnohRpQGoGGQiHnL&sz=w800)](https://drive.google.com/file/d/15JTHL7xCcJZ4Rk8evnohRpQGoGGQiHnL/view?usp=drive_link)

### Audio and Improved Frame Rate

[![Audio and improved frame rate video](https://drive.google.com/thumbnail?id=1MoCbEJQIb__ng5uhHPgMDZkWF_U840-V&sz=w800)](https://drive.google.com/file/d/1MoCbEJQIb__ng5uhHPgMDZkWF_U840-V/view?usp=drive_link)

### Player Vignette and Hands Instead of Controllers

[![Player vignette and hands video](https://drive.google.com/thumbnail?id=1XREVeH1tNcWSAzR1OGVg0bq8JLczZrzE&sz=w800)](https://drive.google.com/file/d/1XREVeH1tNcWSAzR1OGVg0bq8JLczZrzE/view?usp=drive_link)

### Updated Networking

[![Updated networking video](https://drive.google.com/thumbnail?id=1cA3BqUW7wDwh50zw552cL_q-qm5OOO2a&sz=w800)](https://drive.google.com/file/d/1cA3BqUW7wDwh50zw552cL_q-qm5OOO2a/view?usp=drive_link)

### Keypad Mechanism for the Escape Sequence

[![Keypad mechanism video](https://drive.google.com/thumbnail?id=1FTVT8-XnBGKUtfuBaSAIC1MZrk4Wnpdr&sz=w800)](https://drive.google.com/file/d/1FTVT8-XnBGKUtfuBaSAIC1MZrk4Wnpdr/view?usp=drive_link)

## Next Steps

Main goals for Sprint 5:

- **Escape sequence:** put the keypad chest in the house with the front-door key inside, hide code notes at random spots on each floor, show the keypad as a large pop-up screen, and sync it over the network
- **Stealth:** creaky floor areas that Granny can hear
- **Round:** connect the win and lose conditions to their screens for both players
- **Testing:** playtest the full game loop on the Quest, then fix bugs and polish

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
| Divija Katakam | CS | Backend, Software Engineering | GitHub Manager<br>Asset Manager<br> |
| Bishal Panthi | CS | Setting up environment, Modelling near & far interactive objects | Networking |
| Julia Zhang | CS | 3D modeling, Rendering | Asset Manager |
| Isabelle Pham | CS | UI / UX Design<br>Storyboarding<br>Moodboard / Palette | Design Director |
| Yamilet Pineda | CS | 3D-Modeling, Rendering, Programming | Design Director<br>Asset Manager |

## Sprint 4 - Meta Quest Build apk File
https://drive.google.com/drive/folders/1QdCNq7WtCwMw5GTtxHY-8QRp6LtXRMjN?usp=drive_link