# Arden Space Program

An original rocket-building and spaceflight game made in Unity. Build rockets, fly them to orbit and beyond, explore a small solar system of its own, and plan whole space missions alone or with friends.

Developed by captaincarl24

## What you can do

- **Build rockets and spaceplanes**: stack parts, set up stages, and see your delta-v, thrust-to-weight and burn time before launch.
- **Fly real orbits**: thrust, drag and heating, exact orbits on rails, time warp, a map view, a launch autopilot and a flight computer.
- **Go further**: rendezvous and docking, space stations, landers, rovers, bases, supplies for long voyages, aerobraking and heat shields, and an intercept planner.
- **Crew**: Ardlings with jobs and experience. Walk in first or third person (C), run, jump, plant flags, climb back in; ENTER climbs out
  after any landing the crew lived through. TAB goes inside the cabin: walk round and look out of the windows at the real world.
- **Make it yours**: paint your rocket in 16 colours and stick on decals (built-in ones or your own pictures), save your own presets, and
  dress your Ardling (suit colour, visor note, hat) for multiplayer.
- **Easy to read**: rest the mouse on a game word (like "radial in") and a box explains it, or open the Definitions page; hide the whole
  interface with F1.
- **Career**: science, money, contracts and an upgradeable space centre, plus achievements, a story, replays, photo mode (F2) and weather.
- **Three difficulty levels**: Easy, Standard and Engineer, with explanations that match. Easy also helps you fly: tougher parts, stages
  that fire when they run dry, parachutes and legs that open by themselves.
- **Expedition** (multiplayer, 1-4 players, solo works too): everyone builds one rocket together in the real builder with the real parts,
  then rides in it as their own Ardling through the real, full-size solar system. Pick a mission (Tal, the lost outpost, Vesk, Ochre,
  Brume's moons or a free expedition) and a difficulty; the builder shows the six resources (mass, power, budget, data, comms, time) and
  what stops a launch. Survive on your supplies, scavenge old outposts and wrecks for fuel and food, walk round inside the cabin, climb out
  on the surface, and finish with a scored report and a leaderboard.
- **Rocket rooms** (multiplayer): up to 4 players share one rocket as builder, pilot, engineer and mission control. Host or join from one
  screen.

## The worlds

- **Arden**: home, with oceans, beaches, snowfields and highlands.
- **Tal** and **Vesk**: Arden's two moons.
- **Ochre**: a red desert world with dunes and canyons.
- **Brume**: a gas giant with its own moons.
- Wandering asteroids and comets that you can catch with a grabber.

## Playing the game

Ready-to-play builds go in this repo's **Releases** page.

- **Windows**: extract `ArdenSpaceProgram-<version>-Windows.7z` and run `ArdenSpaceProgram.exe`.
- **Mac** (Apple Silicon and Intel): in Terminal, in the folder with the download, run:

  ```bash
  tar -xf ArdenSpaceProgram-<version>-Mac-Universal.tar.xz
  xattr -cr ArdenSpaceProgram.app
  open ArdenSpaceProgram.app
  ```

  The `xattr` step is needed because the app isn't signed with an Apple developer certificate.

How to play is in the game itself: the first game starts an **interactive tutorial**, and **File > Tutorials** in the builder has guided
lessons too (get into orbit, land on Tal, dock with a station). **How to play** on the title screen covers every part, and **Controls**
lets you change the keys. A game opens at the space centre: click a building to go in.

### Expedition

Host an **Expedition** under Multiplayer (it works on your own too). Everyone in the room builds the same rocket; the host or mission
control picks the mission and difficulty in the plan beside the team. Every player needs a seat. When everyone's ready, LAUNCH: the
host's game flies it and the jobs (pilot, flight engineer, mission control, science) work as in a rocket room. Goals tick off as you go;
land within 300 m of an outpost or wreck (⌂ on the map) to **Scavenge** it; **End expedition** scores the run.

| Key | What it does |
| --- | --- |
| ENTER | Climb out after landing |
| W A S D, Ctrl, Space | Walk, run, jump (on foot) |
| C | First or third person (on foot) |
| G | Plant a flag |
| B | Climb back in |
| TAB | Go inside the cabin (E at the seats flies, E at the hatch goes out) |
| Alt | Free the mouse pointer in first person |

### Builder

| Key / mouse | What it does |
| --- | --- |
| Click a part in the list | Take it in your hand (it stays there after you place it, for more copies) |
| Click on the rocket | Stick the part on (green dots are free stack nodes) |
| Right-click or Esc | Put down what you're holding (a part picked up off the rocket goes back) |
| Backspace / Delete | Delete the part under the mouse |
| Ctrl+Z / Cmd+Z | Undo (Ctrl+Shift+Z or Ctrl+Y to redo) |
| X / R / C | Symmetry count / radial or mirror / angle snap |
| Right-drag, scroll | Turn the view, zoom |

The search box at the top of the parts list finds parts by name or kind. **Presets** has 22 ready-made rockets and slots for your own
(the first is free; a campaign buys more). **Paint** colours parts and sticks decals on them: put your own PNG or JPG pictures in the
Decals folder ("Add your own pictures...") to use them.

### Main keys (flight)

| Key | What it does |
| --- | --- |
| Space | Next stage |
| Left Shift / Left Ctrl | More / less throttle |
| Z / X | Full throttle / cut engines |
| W A S D Q E | Steer (pitch, yaw, roll) |
| T | SAS (hold steady) |
| R | RCS, then H N I K J L to move sideways |
| 1 to 0 | Action groups |
| G / U / B | Gear / lights / brakes |
| P | Launch autopilot |
| M | Map view |
| , . / | Time warp |
| F5 / F9 | Quicksave / quickload |
| F1 / F2 | Hide the interface / photo mode |
| TAB | Go inside the cabin |
| ENTER | Climb out after landing |
| Esc | Menu |

## Opening the project in Unity

1. Install **Unity 6000.3.28f1** (Unity 6.3 LTS) with Unity Hub, adding the Windows and/or Mac build support modules.
2. Install **Git LFS** before cloning. The 124 MB star map (`Assets/Game/Resources/Sky/StarMap.exr`) is stored with LFS, and without it you only get a small placeholder file.

   ```bash
   git lfs install
   git clone https://github.com/012miles/arden.git
   ```

3. In Unity Hub, choose **Add > Add project from disk** and pick the cloned folder. The first open takes a while because Unity rebuilds its `Library` cache.
4. Open `Assets/Game/Scenes/Main.unity` and press **Play**.

### Useful menu items

- **Rocket > Run Sim Tests** runs the headless physics and game tests and writes the results to `TestResults/sim_tests.txt`.
- **Rocket > Build Windows Player** builds to `Build/`.
- **Rocket > Build Mac Player** builds to `BuildMac/`.

### Folder layout

| Folder | What's in it |
| --- | --- |
| `Assets/Game/Runtime/Sim` | The simulation: orbits, physics, career, mission design (no rendering) |
| `Assets/Game/Runtime/Game` | Game modes, interface, multiplayer, how-to-play guide, autotests |
| `Assets/Game/Runtime/View` | Rendering: parts, planets, sky, effects |
| `Assets/Game/Editor` | Sim tests and build commands |
| `Assets/Game/Resources` | Textures, sky, music, sounds and fonts |
| `Tools/art` | Python scripts that packed the downloaded textures and audio into the game's files |
| `ROADMAP.md` | Everything that has been built, milestone by milestone |

Builds, screenshots and Unity's `Library` cache are kept out of the repo on purpose (see `.gitignore`).

## Credits

- **Star map**: NASA/Goddard Space Flight Center Scientific Visualization Studio, from Gaia DR2 (ESA/Gaia/DPAC).
- **Textures**: ambientCG (CC0).
- **Fonts**: Barlow and Exo 2 (SIL Open Font License).
- **Music** (from OpenGameArt.org):
  - CC-BY 3.0: "Space" by lasercheese, "ArcLight" by Matthew Pablo, "Beyond the Stars" and "Drifting Beyond the Stars" by Hitctrl, "Space Atmosphere" by Alexandr Zhelanov.
  - CC0: "Another August" by cynicmusic, "Exploration Theme" by CleytonKauffman, "At Home" by Wolfgang_, "Out There" and "Galactic Temple" by yd, "First Light Particles" by Yoiyami.
- **Sounds** (from OpenGameArt.org):
  - CC-BY 3.0: explosions by Michel Baradari and the Blender Foundation, hangar hum by gryc.
  - CC0: rumble by theMinesAreShakin, engine by gmason, wind by aquinn and remaxim, thrusters by EZduzziteh, metal by BMacZero, crickets by Wolfgang_, launch by qubodup, interface sounds by Kenney.
