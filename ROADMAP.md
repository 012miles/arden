# Arden Space Program roadmap

An original rocket-building and orbital-flight game that plays like the genre classics, with its own planets, parts and look.
Every milestone ships with headless physics tests (`Rocket > Run Sim Tests`, or batch mode) and an in-game autotest flight.

## Done

### Milestone 1: core flight (2026-11-08)
- Vertical-stack rocket builder: 17 parts, staging, Δv / TWR / burn time per stage (sea level and vacuum), save/load
- Rigid-body flight: thrust and Isp that change with air pressure, drag, fins and centre of pressure, gimbal, reaction wheels, SAS
- Exact Kepler orbits on rails, time warp up to 100,000x, physics warp 4x
- Map view with orbit, apoapsis and periapsis; parachutes, landings, crashes; launch-to-orbit autopilot (P)

### Milestone 2: going to the moon (2026-11-08)
- **Tal**, a moon 12,000 km out, with a sphere of influence. Patched-conic trajectories through SOI changes, encounter, escape and impact markers, and a "ghost" of the moon where you'll meet it
- **Maneuver nodes**: click your orbit to add one, drag the 6 handles (prograde, normal, radial) or fine-tune in the panel; planned path in orange, burn timer, warp-to-maneuver, SAS points at it, and the burn ends itself when complete
- **Navball** with prograde/retrograde, normal/anti-normal, radial in/out, maneuver and target markers; clickable SAS mode buttons
- Surface / Orbit / Target speed modes, radar altimeter, air-pressure and heat gauges
- **Target** a moon: target marker on the navball, relative speed, closest-approach distance and time
- **Reentry heating**: per-part skin temperature, parts burn up, reentry plasma and glow, thermal view (F)
- **Ablative heat shield**: a bare capsule survives coming home from orbit but burns up coming home from Tal; the shield gets it home
- Engineer's report in the builder (no parachute, no heat shield, low TWR, engine without fuel); "Moon rocket" preset (about 5,900 m/s)

### Milestone 3a: builder overhaul (2026-11-08)
- Rockets are now **part trees** with full 3D positions. Old saved rockets convert automatically.
- **3D drag-and-drop builder**: pick a part, then snap it to green stack nodes or stick it to the side of any part. Ghost previews show green where it can attach and red where it can't.
- **Radial symmetry** 1/2/3/4/6/8 (X) and **mirror symmetry** (R); **angle snap** (C)
- Tools: **Place, Offset, Rotate, Root** (re-root keeps geometry); W/S A/D Q/E rotate or nudge; undo (Ctrl+Z)
- Right-click a part: **paint variants**, **action-group assignment**, **save as subassembly**, delete, make root
- **Staging editor** (move any action up or down a stage, or go back to auto); auto staging understands side boosters
- **Action groups 1-0** plus **Abort** (Backspace), **Gear** (G), **Lights** (U), **Brakes** (B), **RCS** (R), **SAS** (T); HUD toggle buttons
- New parts: **radial decoupler**, **radial fin**, **RCS block**, stack and radial **monopropellant tanks**, **landing legs**, **spotlight**, **launch escape tower**, **Wisp** lander engine
- Physics per part in 3D: off-centre thrust spins the rocket, drag acts on each part with covered stack faces shielded, heat goes to whichever exposed face leads, landing legs touch down first and take 12 m/s, and RCS translates (H/N, I/K, J/L)
- Engineer's report also flags an off-centre CoM or thrust, RCS without monopropellant, and an escape tower with no decoupler
- "Side boosters" preset (Broadside); the autopilot drops spent boosters, limits g-load and jettisons the tower at 25 km

### Milestone 3b: spaceplanes (2026-11-08)
- **Runway** east of the pad (2.4 km, curved with the planet); launch from the Pad or the Runway (builder toggle)
- **Wings** (delta, straight) with lift that grows with angle of attack, incidence and parasitic drag; each part feels its own airflow, including the plane's rotation, so planes damp and weathervane naturally
- **Control surfaces** (elevon, all-moving tail fin) that work out pitch, roll and yaw from where they're mounted; W nose down, D nose right, Q roll left
- **Landing gear**: spring-damper wheels, brakes (B), nose-wheel steering, retract (G); hard landings collapse the gear
- **Gale turbojet**: needs air, spools up over ~2 s, thrust rises with speed and dies near its top speed and above ~25 km
- **Payload fairing**: shields the payload from drag and heat, jettisons as two halves
- **Plane builder view**: the design lies flat; **CoM / CoL / CoT markers** with a stability readout; engineer's report checks wing and gear placement
- "Plane" preset (Kite); HUD shows angle of attack and sideslip
- Mac build (universal, Intel + Apple Silicon) alongside Windows

### Milestone 4a: a persistent universe and rendezvous (2026-11-08)
- **Persistent universe**: every vessel you leave behind (satellites, landers, spent stages) stays where it is, saved to disk with its parts, fuel, staging and switches. Universe time carries on from flight to flight.
- Vessels you aren't flying coast on **exact orbits in the background** and change SOI by themselves. Landed ones ride the planet. Anything in the air when you time-warp is lost, as in the classics.
- Vessels within 2.5 km fly with full physics alongside yours and are drawn within 30 km
- **Switch vessels**: [ and ] for nearby ones; the vessel list (V) for any of them. The builder's **Vessels** panel can Fly, Recover or Delete, and has "start over".
- **Target vessels**: orbit lines and labels on the map, **closest-approach markers** (where you and the target will be), distance and closing speed, and closest approach after a planned maneuver. In Target speed mode (automatic within 5 km), prograde/retrograde follow the relative velocity.
- Launching recovers anything parked on the pad or runway; Revert puts the universe back to just before launch
- Fixes: maneuver nodes can sit any number of orbits ahead (also fixes "+1 orbit"), and a burn now stops when it's done instead of re-planning forever

### Milestone 4b: docking, crew and structure (2026-11-08)
- **Docking ports** (small 0.65 m and standard 1.3 m). Fly two free ports together face to face, lined up within 15 deg, slower than 1.5 m/s, and they latch. The two vessels become one (mass and momentum conserved) and fuel flows across. If you miss, a hint says why ("too fast", "out of line").
- **Undock** from the docking panel: both vessels come back with their names and stages, pushed apart at 0.3 m/s
- **Docking panel**: distance, off-axis offset, angle and closing speed to the target's port (green when it will latch), "Align ports" SAS mode, RCS toggle
- **Crew**: every seat is filled at launch with a named crew member. Move people between capsules, cockpits and habitats (across docked vessels too). A crewed capsule with nobody aboard can't steer; probe cores always can.
- New parts: **Habitat Module** (4 crew), **Solar Panel** and **Antenna** (fold out with Y / the new Deploy group). Power and comms come later.
- **Structure**: every joint has a rating (stack joints scale with their size, side mounts are weaker, wings are rated for roughly 10 g). Loads from thrust, air, acceleration and spin are worked out each step, and an overloaded joint tears off as a separate piece. Deployed panels and antennas rip off in thick air.
- "Docking ferry" preset (capsule with a nose port and RCS, on a lifter)

### Milestone 5a: career (2026-11-08)
- **Career or Sandbox** on a new game (or "start over"). Sandbox keeps every part and no money; Career starts with 25,000 funds and the First Steps parts.
- **Money**: every part has a price, and launching pays for the rocket (Revert gives it back). Recovering a vessel on Arden refunds its value (dry parts plus fuel left): 98% at the launch site, falling to 20% a quarter of the way round the world.
- **Tech tree**: 13 nodes over 9 tiers, from First Steps to Space Habitation. Each costs science and needs a node before it, and only researched parts show in the builder.
- **Science**: Thermometer, Barometer, Sample Canister, Ground Probe, plus crew reports from crewed capsules. Value depends on where you are: launch site, surface, flying low or high, near or high space, on Arden or Tal. Repeating a reading gives what's left. Results go home 100% when recovered, or partly by transmitting with a deployed antenna.
- **Contracts** (Mission control): reach 20 km, reach space, orbit, fly to Tal, orbit and land on Tal, bring a lander home, place a satellite in a given orbit (probe core + antenna), science from a place, test an engine in a situation, dock in orbit. Each has an advance, a reward, a deadline and a failure penalty, and better pay as reputation grows.
- **Records**: first launch, space, orbit, crewed orbit, Tal flyby/orbit/landing/return, docking; each pays once
- "First flight" preset (tier-0 parts only)

### Milestone 5b: running the space center (2026-11-08)
- **Space center** screen with seven facilities, three levels each:
  - Launch Pad: 20 t, 150 t, unlimited
  - Runway: 12 t, 60 t, unlimited
  - Assembly Building: 30 parts, 120, unlimited
  - Mission Control: 3 active contracts and 4 offers, then 7 and 6, then unlimited and 8
  - Research Lab: up to tier 3, 6, all
  - Astronaut Complex: roster of 5, 12, unlimited
  - Administration: 1, 2, 4 strategies at once
  Upgrades cost money; the builder says which limit a design breaks.
- **Strategies**: Outsourced research (funds to science), Patent licensing (science to funds), Public relations (funds to reputation), Bulk purchasing (20% cheaper launches, less reputation), Student research (reputation to science). Each has a setup cost and applies to contract and record rewards.
- **Astronauts**: start with four, hire more (each costs more than the last, up to the complex's limit). Capsules are seated from the roster at launch; habitats launch empty for crew to move into. Recovery brings people home with a flight logged. Losing a vessel loses its crew (-5 reputation each). The builder warns when there's nobody to fly.
- **Rescue contracts**: accepting puts a stranded capsule with a small docking port into orbit. Dock, move the pilot across, recover them, and they join your corps.
- **Base contracts**: an outpost on Arden within 10 km of given coordinates (2+ crew seats, an antenna, a solar panel), or a 4-seat base anywhere on Tal

### Milestone 6a: power and comms (2026-11-08)
- **Electricity (EC)**:
  - Storage: capsules, cockpits, probe cores and the habitat hold some; Battery Pack (100 EC) and Battery Bank (600 EC) add more.
  - Production: solar panels make up to 1.2 EC/s when deployed, facing the sun and out of Arden's or Tal's shadow; engines charge while they fire.
  - Use: probe cores draw 0.03 EC/s, reaction wheels draw while working, lights and transmitting cost power.
  - Empty: a probe core can't steer, and reaction wheels stop (a crewed capsule still flies on engines, fins and RCS). Unloaded vessels keep charging and draining in the background.
- **Comms**:
  - A ground station at the launch site. Two antennas reach sqrt(power x power) apart, if Arden or Tal isn't in the way. A probe's own antenna reaches 2,000 km; the deployable Antenna 20 Mm (Tal); the Relay Dish passes other vessels' signals on (45 Mm to the ground).
  - The best path through relays is found automatically, and its strength multiplies hop by hop.
  - HUD shows signal and route ("Signal 36% via Relay-1") and power. The map draws the link.
  - A probe with no signal has partial control: it steers but can't throttle up, stage or start the autopilot. Crewed vessels always have full control.
  - Sending science needs a link and 10 EC.
- Tech tree: battery packs come with Flight Control; Electrics (solar, battery bank) moves to tier 4; new Relay Networks node at tier 6; Landing moves to tier 5.
- Engineer's report: how long a probe lasts without solar panels, and how far its built-in antenna reaches

### Milestone 6b: resources (2026-11-08)
- **Ore** lies in patchy deposits: up to about 10% on Arden's dry land (none under the seas), and up to about 23% on Tal, richest in its dark plains.
- **Survey Scanner**: unfold it in orbit (above the air, up to three radii high) and it maps a swath under your ground track, wider the higher you fly. A polar orbit maps a whole world as it turns underneath (Tal in about 17 hours). It keeps mapping in time warp and while you fly something else.
- **Ore map** (O, or the button in map view): mapped ground is shaded faint blue (no ore) through yellow to red (rich); unmapped ground stays dark. Readouts show how much of each world is mapped and the ore under you.
- **Drills** (small and heavy): unfold once landed, with the bit able to reach the ground (mount them low). They dig in proportion to the ore there: the small drill gives 5.4 t an hour at 10%.
- **Ore tanks**: a radial pod (0.4 t), 1.3 m (1.5 t) and 2.6 m (8 t). They launch empty.
- **Ore Converter**: makes rocket fuel (0.8 t per t of ore) or monopropellant (0.6 t), 7.2 t of ore an hour, into any tank aboard. It works anywhere, so orbital refineries work too.
- **Heat**: drills and converters heat a shared core. Machines slow down above 700 K and stop at 1000 K. A converter alone settles near 870 K at about 40% speed; one radiator panel keeps it near 640 K at full speed. The **Radiator Panel** (120 kW unfolded) and the **Radiator Strip** (40 kW, fixed) do the cooling. Radiators glow as they work.
- **Power**: machines share whatever the batteries and panels can give, and run slower when short. The new **Solar Array** gives 5 EC/s. All solar panels now pivot on their arm to follow the sun.
- Everything runs the same whether you're flying, the vessel is unloaded, or in time warp. Long warp steps settle the heat straight to its balance point.
- **Mining panel** in flight: ore under you, the ore tank, drill status, converter Off / Fuel / Monoprop buttons with their rates, core heat and radiators, mining power use, and scanner progress.
- **Engineer's report**: flags drills or a converter with no ore tank, more heat than the vessel can shed, and more mining power than the solar panels give.
- **Career**: Ore Mining (tier 6) and Ore Refining (tier 7) research nodes. New contracts: "Map Arden's ore", "Map Tal's ore", "Dig ore on Arden", "Mine ore on Tal". New records: first ore mined, first fuel made from ore.
- **"Tal miner (Prospector)" preset**: a probe lander with 2 drills, an ore tank, a converter, 2 radiators, 4 solar arrays, an antenna and a scanner, on a lifter with about 5,750 m/s. That's enough to land on Tal; refuel there for the trip home.

### Milestone 7a: crew roles and experience (2026-11-09)
- **Roles**: every astronaut is a **pilot**, **engineer** or **scientist**. A career starts with two pilots, an engineer and a scientist, and you hire by role in the Space center.
- **Experience**: astronauts gain experience by coming home from somewhere new: space (1 point), orbit (2), Tal (3), Tal orbit (4), Tal's surface (5). Levels come at 1, 3, 6, 10 and 15 points, so a level-5 veteran has done it all. Promotions show in the news; the roster shows role, level, stars and XP.
- **Pilots and SAS**:
  - Any working command part can hold attitude. A level-0 pilot adds prograde/retrograde, level 1 normal/radial, level 2 target and port alignment, and level 3 maneuver.
  - The **Spark** probe core holds up to normal/radial. The new **Vireo Guidance Core** (Fine Control) holds every mode without a pilot.
  - Modes you can't use are greyed out. Clicking one says what it needs, and SAS falls back to "hold" if the pilot moves out.
- **Engineers**: drills and converters produce 20% more with an engineer aboard, plus 10% per level, for the same power and heat.
- **Scientists**: results sent home keep 10% more of their value, plus 5% per level (samples go from 30% to up to 65%). The **Sample Canister** is now single-use: once its result is sent or thrown away, a scientist aboard has to clean it out (Reset) before it collects again.
- **Crew picker** in the builder (Crew button): one row per seat, filled automatically with a pilot first, then an engineer and a scientist, or pick anyone free. The engineer's report warns when no pilot or probe core is aboard.
- **Flight HUD**: the crew panel lists everyone aboard with role and stars, and the SAS line says which modes you have.
- **Sandbox**: launches are crewed by fully trained (level 5) astronauts, hired automatically as needed, with habitats filled too.
- Old careers get roles on load (pilot, engineer, scientist, pilot for the first four).

### Milestone 7b: EVA (2026-11-09)
- **EVA**: an "EVA" button beside each crew row sends that astronaut out of the hatch, as their own vessel you control. You can't open the hatch flying fast through air.
- **On the ground**: W/S walk, A/D turn, SPACE jumps (about 1.5 m on Tal, landing softly), SHIFT fires the jetpack straight up.
- **Jetpack**: 6 kg of monopropellant at about 2.6 m/s². It lifts off Tal but not Arden. In space, W/S A/D and SHIFT/CTRL fly it, Q/E turn, and T holds attitude.
- **Boarding**: B (or the panel button) climbs into the nearest hatch with a free seat within 3 m. Standing beside a landed vessel, astronauts can climb its side to reach the hatch, legs or parachute.
- **Science**: an EVA report anywhere and a surface sample on the ground (worth a lot from Tal). Results ride in the suit and go aboard when the astronaut does.
- **Flags**: G plants a flag a step ahead. It stays as its own marker in the vessel list and on the map.
- **Hard landings**: landing legs now give way (up to 60% over their rating, so 12 to 19 m/s) instead of destroying the vessel. They fold, and the vessel settles on what's left. Broken legs won't deploy; **an engineer on EVA fixes them**, and they come back to the gear switch's position.
- **Parachutes**: anyone on EVA can **repack** a used parachute, which then opens on the vessel's next stage.
- **EVA panel**: name and role, jetpack fuel, controls, and buttons for boarding, flags, and every repair or repack within reach.
- **Records**: first EVA, and first flag on Tal.
- Experience carries over: astronauts keep their flight log while out and bring it back aboard.

### Milestone 8a: sky and light (2026-11-09)
- **Atmosphere glow**: Arden has a blue rim of air, brightest at the edge, lit by the sun and warm along the day-night line. Seen from orbit and in the map.
- **Clouds**: a weather layer 5 km up, lit by the sun (dark at night) and seen from above or below. Climbing past it, the close-up ground takes on the cloud pattern too, so there's never a hole in the clouds under you.
- **Sky**: blue at the ground, deepening through dark blue to black as the air thins, with orange at sunrise and sunset.
- **Stars and the sun**: stars come out in space and at night and fade in daylight under the air. The sun is a bright disc with a soft glow, hidden behind planets.
- **Light**: engine exhaust lights up the pad, the tower and the rocket. Spotlights point out and down to light the ground around a lander. Parts have a faint sunlit sheen.
- **Night is dark**: fill light drops when you're in a planet's shadow, so lights matter on the night side.

### Milestone 8b: real terrain (2026-11-09)
- **Arden**: the continents rise into hills and ridged mountain ranges up to about 3.5 km. Seas stay flat at sea level, and the land around the space center is flat for 25 km.
- **Tal**: rolling highlands, low dark plains, and craters at four sizes (from ~1 km to ~30 km across), with bowls and raised rims.
- It's real ground: landings, landing legs, walking astronauts, drills, jumps and lift-offs all use the actual height. You can land on a 3 km peak or in a crater floor; "Radar altitude" reads off the real ground.
- **Meshes** follow the terrain from orbit down to the close-up ground, with slopes shaded by the sun. The close-up ground gets finer when you're low over it.
- Vessels saved landed before the terrain had height are set back down on the ground when you load.
- Fixed while doing this: a flat-ground check could keep "re-landing" anything trying to lift off uneven ground.

### Milestone 8c: sound (2026-11-09)
- Every sound is made by the game itself, with no audio files:
  - **Loops**: a rocket roar, a turbojet whine, wind and an RCS hiss.
  - **One-off sounds**: a staging clunk, a docking clank, an explosion and a parachute whoosh.
- **Rocket roar** follows thrust and throttle. In vacuum it dulls to a rumble through the hull, opening up as the air thickens.
- **Jets** whine up as they spool. **Wind** builds with dynamic pressure, rising in pitch with speed. **RCS** thrusters and the EVA jetpack hiss.
- **One-off sounds**:
  - Staging and action groups that drop parts clunk; touchdowns thud; the hatch clunks on EVA and boarding.
  - Docking and undocking clank, and parachutes whoosh open.
  - Explosions boom, quieter with distance and silent beyond about 5 km.
- Sounds drop under the map view, go quiet in time warp and pause with the game.
- **Volume** setting in the pause menu (ESC), kept between sessions.

### Milestone 8d: detailed parts (2026-11-09)
- **Capsules**: a framed window, a hatch on the side, a docking collar on top, RCS ports and panel seams.
- **Probe cores**: an equipment band with vents, a green status light and stub antennas. The Vireo has a sensor dome.
- **Tanks**: panel seams, stringers (16 on the 2.6 m tanks) and a feed line down the long ones.
- **Engines**: thrust plate, three turbopumps with feed pipes, a combustion chamber and a curved bell with cooling bands. Solid boosters get segment joints and a curved nozzle.
- **Decouplers**: a ring of bolt heads.

### Milestone 9: a second moon and quicksave (2026-11-09)
- **Vesk**, Arden's second moon: a small ball of ice (110 km radius, 0.07 g) 26,000 km out, tidally locked, about 106 h per orbit. Its sphere of influence (2,060 km) sits well clear of Tal's.
  - **Ground**: smooth ice plains crossed by raised crack ridges, soft craters, and dusty patches. Physics, walking and drilling all use the real height.
  - **Getting there**: about 1,000 m/s from low Arden orbit, a little more than Tal, then an 8-20 hour coast. Plan it in the map like a Tal trip; Vesk shows in the map, target list and TAB focus.
  - **Science** from Vesk is worth 1.6x Tal's.
  - **Career**: Vesk opens once Tal has been orbited. Contracts to fly by, orbit, land, come back from the surface, map its ice and mine it, plus science-gathering offers.
  - **Records**: first trip, orbit, landing, return and flag.
  - **Crew experience**: 4 / 5 / 6 XP for visiting, orbiting and landing on Vesk.
- **Quicksave / quickload**: F5 saves and F9 loads, or use the buttons in the pause menu (ESC).
  - A quicksave stores the whole universe: every vessel, the career, the ore survey, the time, and which vessel you were flying.
  - The crash screen offers "Load the quicksave". Loading during the same flight keeps "Revert to launch".
- Fixes:
  - On the ground the camera can no longer swing under the terrain (it used to show only black).
  - A first flag on Vesk no longer counts as "first flag on Tal".
  - Return contracts check the moon they name.
  - The flight help bar fits on two lines again.
  - Tal's map globe was checked from two sides: no seam line.

### Milestone 10a: regions (2026-11-09)
- **Named regions** on every world, worked out from the same noise that paints and shapes it, so a region is what you see:
  - **Arden**: Deep Ocean, Shallows, Shores, Grasslands, Forests, Highlands, Mountains, Ice Caps
  - **Tal**: Bright Highlands, Low Hills, Dark Plains, Great Craters
  - **Vesk**: Ice Plains, Blue Ridges, Dust Fields, Ice Craters
- **Science per region**: results taken landed or flying low count separately in each region, so every region is fresh science. Results from the launch site, flying high or in space aren't regional. Old results still count.
- The HUD's Body line names the region underneath when you're low or landed ("Tal · Great Craters"), and so does the science panel.
- **Region map**: O now cycles the map layers (ore map, then region map, then off), or use the Ore / Regions buttons. The map panel shows a colour key for the focused world.
- **Region contracts**: science flying low over one of Arden's regions, landed in one (once you've reached space), or landed in a region of Tal or Vesk once you've landed there.
- Fixes:
  - Map labels no longer pile on top of each other: they step down or move to the other side of their dot. Your own apoapsis, periapsis and encounter labels come before other vessels' names, and labels hide behind HUD panels instead of drawing over them.
  - The records list in mission control is in two columns, so it fits with Vesk's records.
  - Map layers sit above Arden's clouds.
  - The antenna and relay dish descriptions say which one reaches Vesk.
  - Landed science contracts read "on Tal's surface" (they used to say "on the surface over Tal").

### Milestone 10b: rovers (2026-11-09)
- **Rover parts** (new "Rovers" tech node):
  - **Rover Wheel**: 0.3 m, 1 kN motor, top speed 10 m/s.
  - **Big Rover Wheel**: 0.55 m, 3.5 kN motor, top speed 14 m/s.
  - **Rover Frame**: a flat chassis with a 250 EC battery and a small reaction wheel.
- **Wheels**:
  - Mount them on the sides with the axles pointing out. Each one rolls across its axle on its own spring and damper.
  - They follow the real slope of the ground, so hills slow you down and you can park on them.
  - Motors push toward the wheel's top speed, limited by grip: low gravity means slow starts and long slides. Fast on Arden, careful on Tal, slippery on Vesk.
  - They run on electric charge; with a flat battery they do nothing.
- **Driving**: W/S drive (and let the brakes off), A/D steer, B brakes.
  - Front and back wheels steer opposite ways, more at a crawl and less at speed.
  - With the brakes on and stopped, it parks so time warp works.
  - The HUD shows speed and distance driven; the help bar switches to rover keys; the tyres spin and steer.
  - Near any ground the navball's speed switches to Surface.
- **"Rover (Tumbler)" preset**: probe core, frame, four big wheels, antenna, solar panels, headlight. Rovers default to a runway launch, standing on their wheels and facing down the runway.
- **Career**:
  - Contracts: "Test drive" (1 km on Arden), "Drive on Tal" (2 km) and "Drive on Vesk" (3 km). Distance counts on one world at a time and is saved.
  - Records: first drive on Tal, first drive on Vesk.

### Milestone 11a: the sun (2026-11-09)
- **Helion**, the sun, is a real body now (200 Mm across the middle). Arden goes round it 13.6 Gm out at 11.5 km/s: a year is 343 Arden days.
  - Arden's sphere of influence reaches 80 Mm, comfortably holding Tal and Vesk.
  - Burn fast enough (about 3.5 km/s from low orbit) and you leave Arden's pull for an orbit round the sun. The planned path shows the escape and the orbit after it; time warp and the background vessels follow it too.
- **Seasons**: Arden's spin axis is tilted 29 deg to its orbit, so over the year the sun swings from 29 deg south to 29 deg north. Sunlight, solar power, shadows, the sky and the lighting all follow the sun's real position.
  - At time 0 the sun is exactly where it always was, so nothing about an existing game changes until the months go by.
- **The map** zooms out to the whole system (TAB can focus Helion). Arden's orbit round the sun shows when zoomed far out. Distances read in Gm.
- Flying within 20 Mm of Helion's surface burns a vessel up.
- **Career**: a "First escape from Arden" record, and crew experience for going round the sun.
- Fixes:
  - Rover HUDs no longer show an angle of attack.
  - Rovers start with the throttle at zero.
  - The navball switches to surface speed near any ground.

### Milestone 11b: Ochre (2026-11-09)
- **Ochre**: a dusty orange desert planet 21.5 Gm from the sun, in Arden's orbital plane. A 170-day year, 0.31 g, 18 h days.
  - **Air**: thin (15% of Arden's at the surface), up to 50 km, under a butterscotch sky with a warm glow round the rim. Parachutes work, but you land at about 8 m/s.
  - **Ground**: rust plains and highlands cut by deep winding canyons, pale dune seas, craters and polar ice.
  - **Regions**: Rust Plains, Dune Seas, Canyons, Highlands, Polar Ice.
  - Science there is worth 2.5x.
- **Mote**: Ochre's little moon, a 45 km lump of rock 3,000 km out, always facing Ochre. Regions: Ochre Face, Far Face, Rim.
- **Transfer planner** (target Ochre in the map while orbiting Arden):
  - The map panel shows how far ahead Ochre is, the angle to leave at, and the time to the next window (day 40 of a new game, then every 173 days).
  - **"Plan transfer"** puts a maneuver node on the best escape burn at the window (about 1,210 m/s from 100 km), checked against the real predicted path.
  - On the way, **"Fine-tune arrival"** plans a small correction (about 5 m/s, 40% of the way) that brings the arrival down to about 80 km above Ochre's air, ready to capture or aerobrake.
  - In the tests a planned trip reaches Ochre after about 61 days, passing 80 km up.
- **Deep Space Dish** (new "Deep Space" tech node): reaches the ground station from 40 Gm, so Ochre is always in range unless the sun or Arden is in the way. It relays for other vessels too.
- **Career**:
  - Contracts: once you've escaped Arden (or orbited Vesk), "Fly to Ochre", then orbit, land, land on Mote, come back from the surface, plus science and region work there.
  - Records: first trip, orbit, landing and return for Ochre, and first landing on Mote.
  - Crew experience: up to 9 XP.
- Fixes:
  - Air pressure is now measured against Arden's sea level everywhere, so engines and the HUD read Ochre's thin air right.
  - Paths round the sun no longer slash across the view when zoomed in near a planet.
  - Distances read in Gm from 1 Gm up.

### Milestone 12: getting there in style (2026-11-10)
- **Aerobraking forecast**: whenever your path dips into a planet's air, the map panel says what the pass will do: leave the air in an Ap x Pe orbit, still escaping at so many m/s, slow into the air for a landing, or hit the ground too hard. It also shows the peak g.
  - It steps through the air with your vessel's real mass and drag, with the air turning with the planet.
  - It assumes you hold retrograde (base or heat shield into the air). A craft that wobbles brakes less.
- **"Aerocapture"** button on the way to a planet with air: it plans the mid-course correction to the middle of the safe corridor.
  - The pass must capture you even with 20% less drag than expected, and must not bring you down if you tumble side on.
  - It refuses long, tumble-prone craft: use a capture burn, or a heat shield up front.
- **Comms map** (O now cycles ore, regions, comms, off): every working link between your relays, the active vessel and the ground stations, coloured by strength.
- **Deep space stations**: two more big dishes a third of the way round Arden's equator. They only track craft beyond Arden's sphere of influence, so Arden's turning no longer cuts off a probe at Ochre, while relays still matter close to home.
- **"Ochre probe (Wayfarer)"** preset: 7,500 m/s in five stages. A probe lander with the deep space dish, solar panels, a parachute, legs and its own engine, on a transfer stage and the moon rocket's lower stages.
- Fixes:
  - A vessel coasting perfectly straight up or down is no longer put on exact rails (that degenerate orbit could teleport it into the ground).
  - Planets' orbit lines are a little bolder when zoomed far out.

### Milestone 13: polish and stability (2026-11-10)
- **Heat shields that work like real ones**: the pressure on a blunt shield acts well behind its face, so a capsule flies shield first by itself and even turns round if it hits the air backwards. Aerobraking passes now match the forecast almost exactly (within 15% in the test; it came out spot on).
- **First-flight guide**: a new game shows a small panel that walks you through launch, lift-off, the climb, reaching space and getting into orbit. Each step ticks off when you actually do it.
  - The advice fits the rocket: the starter rocket can't reach orbit, so the guide tells you to go up and bring it home on the parachute instead.
  - It notices a crash and a landing, and starts again when a new rocket is on the pad.
  - Close it with x; the ESC menu brings it back. Older saves don't get it.
- **Any window shape**: the interface now shrinks to fit narrow windows (4:3, 5:4), not just short ones, so the builder's top bar no longer runs into itself. A new autotest visits every main screen at five window sizes and reports any panel that runs off the screen or lands on another.
- **Map lines at the furthest zoom**: orbit lines no longer vanish when the map is zoomed out to the whole solar system (Unity's line renderer lost its way at those distances; the lines now face the camera directly).
- Fixes (found by the window-size tour):
  - The flight help bar wraps to fit between the side panels instead of running under the Stages panel.
  - The research tree scrolls down when a tier has three nodes; the Rovers node at the bottom was cut off.
  - The pause menu no longer covers the top of the navball.
  - The "Add maneuver" button no longer shows while you're still on the pad or landed.

### Milestone 14: wanderers (2026-11-10)
- **Wanderers**: small rocky bodies that pass through Arden's space on open paths. One arrives every couple of days (two at most at a time). Each comes in from near the edge of Arden's sphere of influence, swings past between 400 km and 6,000 km up a day or so later, and leaves for good unless someone catches it.
  - Three sizes: class A (3 m, 18 t), class B (5 m, 60 t) and class C (7 m, 170 t), each a lumpy flat-shaded boulder.
  - The vessel list shows their mass, and their situation reads "Passing Arden: closest 1,234 km" or "Leaving Arden's space". You can target them, but not fly them.
- **Wanderer Grabber** (tech node Wanderers, tier 8, after Docking): three claws on the nose. Fly its open end into a wanderer, square on (within 30 degrees) and slower than 1.5 m/s, and it latches on.
  - The rock then flies as part of your vessel: thrust works through the combined mass, and mass and momentum are kept.
  - "Release" in the Docked panel lets go.
- **Rock samples**: the grabber takes a sample (40 base science) while it holds a wanderer. Each wanderer is its own subject.
- **Contracts**: "Grab a wanderer", and "Capture Wanderer B-KR3": push a named one into a closed orbit (periapsis above the air, apoapsis under 40,000 km). A capture fails if the wanderer leaves first.
  - Records: first wanderer grabbed, first wanderer put in orbit.
  - Caught at its closest pass, a wanderer needs only a few tens of m/s to be captured; matching its speed is the hard part.
- **"Wanderer tug (Wrangler)"** preset: grabber, probe core, RCS, solar power and an antenna, with 12 t of fuel in 2.6 m tanks and a Kestrel, on a four-tank Heron core. It reaches orbit with about 2,900 m/s. Pushing a class B rock it still has 500 m/s (2.6 m/s² of push); with a class C rock, 200 m/s.
- Fixes: the map now draws an open path (a wanderer passing through, or a vessel escaping) all the way to the edge of the sphere of influence, not just the next six hours of it.

### Milestone 15: time and planning (2026-11-10)
- **Plan intercept**: target a vessel or a wanderer and the map panel offers "Plan intercept". It finds the cheapest single burn that meets the target, counting the speed match at the end too, and sets it as a maneuver node.
  - It searches every burn time over a full catch-up cycle and every flight time from a tenth of an orbit to one and a half.
  - It skips any path that dips into the air, and folds a plane change into the burn.
  - It uses a Lambert solver: the orbit that gets from here to there in a given time.
  - A wanderer is met near its closest pass (within twice that distance), where capturing it is cheap.
- **Match speed**: once you're on the way and the closest approach is under 10 km, the same button plans the burn that cancels the speed difference there. It starts half the burn early.
  - In the autotest, the planned intercept closed to 20 m. After the speed match the tug was 2.6 km off at 4 m/s, close enough to finish with RCS.
- **Coming up**: click the clock for a list of the next events, soonest first, each with a Warp button:
  - the maneuver node, and the next apoapsis and periapsis
  - entering or leaving a sphere of influence, or hitting the air
  - the closest approach to the target
  - each wanderer's closest pass
  - the next transfer window to each other planet
  - Warping stops a minute before each event (half the burn plus 20 s before a node), and says what's coming.
- The closest approach to a target on an open path is now searched until it leaves (up to two days), not just over the next hour.

### Milestone 16: a giant (2026-11-10)
- **Brume**: a banded gas giant 48 Gm from the sun, 3.5 times farther out than Arden, with a 568-day year.
  - It's 3,000 km to its cloud tops, with 0.82 g there and a 9 h day.
  - Its bands are cream, tan, rust and sea-green, with polar haze and a great red storm.
  - It has rings from 4,600 to 7,600 km out, with a gap: lit, see-through and seen from both sides.
  - **No surface**: sink 2 km into the clouds and the pressure wins. Its deep air (220 km) makes aerocapture easy: arriving at 2 km/s, any pass between 170 and 195 km slows you into orbit.
  - Four regions, one per cloud band.
- **Pyre**: the inner moon, 190 km of black lava fields with glowing cracks, grey ash plains, and broad volcanoes up to nearly 3 km with bright summit calderas. Pull is 1.4 g.
- **Rime**: the outer moon, a 260 km ball of cracked ice at 1 g. Brume hangs in its sky.
- Getting there: the first window opens on day 60. The trip takes 146 days and about 2,050 m/s from low Arden orbit. The transfer planner (target Brume) works there too.
- **Far Reaches** tech (tier 8, after Deep Space):
  - The **Lumen Drive**: 880 s, 45 kN, useless in air.
  - The **Great Dish**: 110 Gm range, so it reaches home from Brume even at its farthest (60 Gm). The deep space dish only managed 7 of 24 sample days.
- Contracts once you've been to Ochre: fly to Brume, orbit it, land on Rime, land on Pyre. Records for the first trip, the first orbit, and the first landing on one of its moons. Crew earn 9-12 XP for each.
- **"Brume probe (Long Haul)"** preset: a Vireo probe with the Great Dish, solar panels, two FT-4s and a Lumen Drive, on a Kestrel stage and a Heron core. It reaches orbit with 4,900 m/s.
- Fixes:
  - Long messages in flight and in the builder now wrap instead of being cut off.
  - The aerobraking forecast no longer promises a "landing" on a gas giant.

### Milestone 17: the flight computer (2026-11-10)
- **Queued burns**: maneuver nodes can wait in line, each planned from the path the one before leaves. "Plan intercept" now sets both: the burn to get there, and the speed match on arrival.
  - The maneuver panel shows the next one ("Then: 256 m/s in 39m"), and so does the events list ("Next burn").
  - When a burn is done (or deleted), the next one takes its place.
  - A speed match is re-aimed from your real path when its turn comes, since the burn before it is never perfect.
- **Auto-burn**: a button on the maneuver panel hands the burns to the flight computer.
  - It warps to each one, points along it (and won't open the throttle until it's within about 6 degrees), and eases off near the end.
  - It stops each burn on the dot, then goes on to the next. A speed match burns against the target's own velocity, so it ends truly alongside.
  - X takes back control. To start it you need a probe core or pilot that can hold a maneuver, a signal, and a lit engine.
  - Burns handed over while there's a signal still run after it's lost: the computer flies them from memory. That's the way to burn on the far side of a world.
  - Fixed while testing: a burn the engine never made (throttle held at zero) could be counted as done and deleted once its moment passed.
  - Fixed while testing: during auto-burn the ship was steered along the planned-orbit direction, not the one the computer burns along, so a speed match never lined up. In the game it now flies the whole intercept by itself and ends 2.4 km from the station at 0.25 m/s.
  - A burn also ends when what's left has swung mostly sideways (drift between two nearby craft does that); that's RCS work.
  - In the tests it flew a whole intercept with real, finite burns: 166 m/s, then 256 m/s 39 minutes later. It ended 3.4 km from the station at 0.24 m/s, ready for RCS.
- Fixed: a speed-match burn's direction was worked out at the meeting point but applied half a burn earlier, where the orbit's frame has turned a little. That alone left several m/s.
- **Close-pass contracts**: skim Tal below 15 km, Vesk below 10 km, Mote within 5 km, or Pyre below 40 km, without landing. One is offered at a time, once you've been there.

### Milestone 18: saves and settings (2026-11-10)
- **Title screen**: "Rocket Game", with Arden turning slowly behind it. Continue (the last game, with its mode, funds, day and vessel count), New game, Load game, Settings, Quit.
- **Several saved games**: each game keeps its own universe and quicksave, so starting a new one no longer wipes the old.
  - New game asks for a name and career or sandbox. A new career starts with the First Flight rocket and the guide.
  - Load game lists every save, last played first, with Load and Delete (Delete asks first).
  - Designs are shared between games.
- Your existing save moves into a game called "Game 1" the first time the new version runs, quicksave and all.
- **Settings** (kept between games): interface size (80-130% on top of the automatic fit), sound volume, and autosave during flights (off, 2, 5 or 10 minutes; 5 by default).
- A **Menu** button in the builder (and "Main menu" in the vessel list, where "Start over" used to be) goes back to the title, saving first.
- Not done, on purpose: **gravity assists**. A planner was built and tested: two Lambert legs through a flyby that has to stay above the planet's air. The tests showed an assist past Ochre never pays: the best trip to Brume past Ochre cost 3,515 m/s, against 2,386 going direct. Ochre is too small to bend a fast path much (about 9 degrees at 3 km/s). The planner was dropped rather than shipped as a button that never helps.

### Bug fixes and the new name (2026-11-09)
- **The game is now Arden Space Program** (title screen, window title, and the files ArdenSpaceProgram.exe / .app). Unity names the save folder after the game, so the first run copies your saves, designs and quicksaves from the old Rocket Game folder (the old folder is kept as a backup).
- **Ships collide.** They used to pass straight through each other, a limit noted at milestone 4b.
  - Every part is a capsule or a ball. Where two vessels touch they're pushed apart and bounce off with a rigid-body impulse, spin included; a landed vessel doesn't budge.
  - Hit harder than a part can take and that vessel is wrecked, as on the ground.
  - Open docking ports and an open grabber don't collide, so they still latch. Pieces that just came apart (staging, undocking, EVA) ignore each other until they're clear.
- **Docking ports work on a ship's side**, the other milestone 4b limit. A side-mounted port (or grabber) now stands off the hull facing outward, instead of lying flat along it facing up and down the ship.
- Zoomed right out in the map, a moon's name no longer sits on top of its planet's.
- The selected button on the new-game page is centred like the others.

### Milestone 19: supplies for long voyages (2026-11-11)
- **Crew need supplies** (food, water and air), counted in crew-days: each crew member uses one a day, anywhere except on Arden's surface. Supplies weigh 5 kg a crew-day.
  - The Wren capsule carries 5 crew-days, the cockpit 3, the habitat 40, and an astronaut's suit half a day (taken from the ship, given back on boarding).
- **New parts**:
  - **Supply Crate**: side-mounted, 30 crew-days, in Survivability (tier 1).
  - **Supply Module**: 150 crew-days.
  - **Greenhouse**: grows food for two while it has 4 EC/s of power. It and the Supply Module are in the new Life Support node (tier 5).
- **Running out is deadly** (captaincarl24's call). When a ship's supplies hit zero, the crew have two days to get more (dock a supply ship, or get home). Then they die: they're lost from the roster, the news says so, and a career loses reputation. The ship itself stays.
- You can see it coming:
  - The crew panel shows the days left (orange under a day, red with a countdown once they're out).
  - A warning comes up with less than a day left, and the events list shows when the supplies run out.
  - If the crew die during time warp, the warp stops and says so.
  - The builder shows how long a design's supplies last its crew.
- **A choice per game**: "Crew need supplies" on the new-game page (on for careers, off for sandbox). Games saved before this keep it off.
- Stranded capsules in rescue contracts come with a supply crate, so they last until you get there.
- Fixed while testing: the builder's delta-v ignored the weight of supplies (it showed 2,219 m/s instead of 2,210 for the starter rocket).

### Milestone 20a: the realistic look (2026-11-11)
- **Light and lens**: filmic (ACES) tone mapping, bloom, colour grading, a soft vignette and 4x anti-aliasing; exposure eases like eyes adjusting (brighter at night or in a world's shadow). Soft sun shadows on rockets, the pad and the ground.
- **Real skies**: each world's air is drawn with single Rayleigh + Mie scattering (Rocket/Atmosphere and RkCommon.hlsl): blue days, golden sunsets, the blue rim of Arden from orbit, Ochre's thin butterscotch air. Sunlight is reddened by the air on its way down, so the ground, clouds and the rocket turn gold at sunset; distant ground fades into haze through the same air.
- **The Milky Way**: NASA's star map (Goddard SVS, from Gaia DR2) on the sky instead of dots.
- **Physically based parts** (Rocket/Lit): paint, gloss, metal, dark heat-stained engine bells, gold foil on probes, glass windows, glowing lamps; parts reflect the sky, the world below or black space. Free CC0 photo textures from ambientCG for foil, metal, concrete and asphalt.
- **Ground**: realistic brightness (seas ~5%, forests ~8%, Tal's dust as dark as worn asphalt), sea glint, dusty airless ground that stays bright up to the terminator.
- **Exhaust**: glowing plumes that are tight with shock diamonds in thick air and balloon out in vacuum, coloured by engine type; smoke trails in air that billow along the ground (thick white for solid boosters).
- **The assembly building**: polished concrete floor with painted lines, ribbed steel walls, catwalks, roof trusses with lamps, the big door.
- **The crew are Ardlings** (captaincarl24's design, 2026-11-11): big round helmets with the visor blacked out and a hand-drawn sticky note stuck on it; a small suited body. Everyone has their own suit colour, note face (10 faces, 5 note colours) and sometimes a hat (beanie, party hat, propeller cap, antenna, wide brim, crown, flower, top hat), chosen from their name. Their note shows in the EVA panel and the astronaut list.
- Settings: Graphics Low / Medium / High; credits for the star map and textures.
- `-autotestGraphics` (screenshots 280-290).

### Milestone 20b: sharp worlds and a real space centre (2026-11-11)
- **Sharp worlds from orbit**: each world's colours, relief and (Arden's) clouds are baked into cubemaps from the same functions as the terrain, on background threads after the game starts (home first, all eight worlds in about a minute and a half; until then they keep their vertex colours). Coastlines, mountain shading and clouds are crisp from orbit and on the map.
- **Ground up close**: CC0 photo textures (ambientCG) of grass, rock, beach sand, snow, grey gravel, ice, desert sand and rocky dirt, blended by each world's kinds of ground (Arden's beaches, snowfields and highlands, Tal's rocky highlands, Ochre's dunes and canyon floors...), bare rock on steep slopes, at three sizes that fade in as you get closer. The photos only add texture: the map colours stay in charge.
- **Clouds** are sharper and cast shadows on the ground; seen from above the cloud layer the close ground shows them too.
- **The space centre**: an open steel lattice tower with walkways, swing arms and a lightning mast, floodlight masts, pad markings and a flame trench, propellant spheres and a water tower, a striped assembly building, mission control with windows and a dish, runway edge lights.
- **Ardlings move**: arms and legs swing as they walk, they breathe standing still, and they float with limbs spread in the air or in space.
- 4 new tests (cube-face layout, ground kinds, Ardling looks, suit and faces); `-autotestGraphics` adds the low flight, Ochre's ground and the walk.

### Milestone 21: sound and music (2026-11-11)
- **A soundtrack** (37 minutes, free CC0 and CC-BY music from OpenGameArt, credited in the game): a grand theme on the title screen; calm pieces in the assembly building; an orchestral piece from liftoff until you reach space; six quiet ambient pieces in space, shuffled, with long silences between them. The pad itself is quiet, just the space centre around you.
- **Recorded sounds** layered over the old synthesized ones: a deep rocket rumble and a crackling roar in air that thin out to the muffled engine hum you hear through the hull in vacuum; an ignition roar when engines light in air; wind over the hull and a deep rising roar during reentry; the RCS hiss with a puff as it fires; metal thuds and clinks for staging, touchdown and the hatch, a deep clank for docking, recorded explosions.
- **Places**: the assembly building's hum, a breeze at the space centre by day and crickets at night.
- **Interface clicks** for every press on the interface.
- Settings and the pause menu: a Music volume under the master Sound volume. A Credits page on the title screen (star map, textures, music, sounds).
- `Tools/art/build_audio.py` builds the game's audio from the downloads (loudness-matched music, crossfaded loops). `-autotestSound`; 1 new test (every track and effect loads; music streams).

### Milestone 22: bases (2026-11-11)
- **Ground cables**: an astronaut on EVA takes a cable from a landed module, walks it to another within 60 m and plugs it in (EVA panel buttons; unplug the same way). Cabled modules form a **base**. A cable comes loose if a module lifts off or ends up out of reach.
- **Sharing**: a base evens out its electricity, supplies and ore; fuel and monopropellant flow from the base's own tanks into any landed ship plugged in (a refuelling depot), never back out of the ship.
- **Crew** walk between cabled modules ("Bring 1 here" in the Base panel) without going outside.
- **Base parts** (tech node "Outposts", after Life Support): the **Base Hub** (a squat module for four with a big battery, supplies and a docking collar), the **Research Lab** (two seats; about 2 science a day per scientist, more on far worlds and landed, career only) and the **Base Storage Tank** (10 t of fuel and 4 t of ore).
- **Base panel** while you fly any module of a base: its modules, crew and seats, power, supplies (and how long they last), ore, fuel and research per day.
- **Contracts**: research bases on Tal, Vesk and Ochre (3 cabled modules, crew seats, a lab, an antenna and solar power); the older outpost and base contracts now count every cabled module.
- The baked planet maps are now kept on disk, so after the first start the worlds are sharp at once.
- 5 new tests; `-autotestBases` (screenshots 310-313).

### Milestone 23: polish for release (2026-11-12)
- **A new interface look**: rounded panels and buttons, Barlow and Exo 2 fonts (SIL Open Font License), a big title.
- **How to play**: an 8-topic guide on the title screen and in the pause menu (building, flying, orbits, landing, science, crew, bases, controls).
- **Key rebinding**: every game key can be moved (Settings > Controls), with shared keys flagged and a reset; the help bars and guide name the keys in use.
- **Achievements** (captaincarl24's idea): 28 badges, from "Liftoff!" and "Exited the Stratosphere" to "Giant Leap" and "Lithobraking", each paying funds and science in career (badge only in sandbox), saved per game. A pop-up when you earn one; a Badges screen in the builder and the pause menu.
- **Explosions**: a rolling fireball, flung glowing debris and a dark smoke cloud.
- **Reentry fire**: a glowing shock layer cupping the leading face with a hot rim, a streaked sheath streaming back and sparks; it widens when a ship comes in sideways.
- **Scorching**: hot parts pick up soot and streaky scorch marks that stay (saved).
- **Pressure limits and chain reactions**: every part has a pressure limit (140-300 kPa) as well as a heat limit; flooring it through thick air or slamming in too steeply crushes or tears off exposed parts. Parts holding fuel explode instead of just burning up, heating and scorching their neighbours, and full tanks next door can go off a moment later.
- **A real space centre**: headquarters with a flag, an astronaut complex, an R&D campus with a big dish, an airport by the runway (control tower, arched hangars, apron, a parked plane), roads, car parks and trees, with Ardlings walking between the buildings and cars, trucks, forklifts and tugs driving round.
- **A busy assembly building**: workers on their rounds and at the benches, forklifts, a truck coming in through the big door, a tug, an overhead crane, crates, pallets, spare tank sections, parts racks along the walls and access towers. The assembly building outside is now as big as the hall you build in.
- **Hills**: the space centre sits in rolling green country (flat only round the buildings), and Arden's land has smaller hills and bumps everywhere.
- **Title screen**: the space centre on a sunny morning, your last rocket on the pad, the camera drifting slowly round it.
- Bug sweep: all 30 screenshot tours run clean (one crash in the docking panel fixed).
- 6 new tests; `-autotestPolish` (screenshots 330-343).

### Milestone 24: friendlier (2026-11-12)
- **Steady reentries**: a capsule's blunt base (or heat shield) now gets a righting push when it's side-on, so it never hangs sideways; a bare capsule flies base first too. SAS hold no longer fights the air all the way down: once the air pushes harder than the controls should resist (or the hold slips 10 degrees), it lets go, damps the swing and takes a new hold shield-first. Spins dropped from up to 257 deg/s to under 20.
- **New Ardlings** (same big helmet, blacked-out visor and sticky note): a shaped suit with shoulder pads, elbow folds, knee pads, mitten gloves with thumbs, chunky moon boots with tread and a coloured toe, a belt with pouches, a chest box with lights, ribbed hoses, ear pieces, a helmet lamp, a crown stripe, a mission patch and a wrist pad. Crowds swap to a simple version far away.
- **A calmer interface**: telemetry shows the basics with a More button for the rest; the controls box only lists what matters right now; SAS buttons only for modes the ship can use; the key list tucks away after 25 seconds (F1 brings it back); the builder's file actions are in one File menu and its two rows have room to breathe; no more cut-off text.
- **Three wording levels** (captaincarl24's idea), on the new-game page, in Settings, the pause menu and the guide: **Engineer** (the proper terms, as before), **Standard** (plain words with the numbers: highest point, speed budget, push vs weight, Celsius) and **Easy** (short words for young players: big Steady / Forward / Backward buttons, fewer numbers, its own first-flight guide and How to play pages, parts described by what they do).
- **Speed check**: `-autotestPerf` times seven busy scenes at each graphics setting. On an RTX 2060 with a 2017 quad-core, everything runs at 190-290 fps; the far-away Ardlings halved the triangles (the busy hall 2.2 M to 0.4 M), and Low now draws 70% resolution with one small shadow map.
- **A new headquarters** (captaincarl24's photo as the guide): a five-storey white block banded with green glass, fins and sun louvers, a curved all-glass wing, a glass lobby under a deep canopy, a busy roof (penthouse, solar arrays, cooling plant, an antenna mast with a dish and a warning light), and a round plaza with a fountain, three flags, palms, hedges, lamps, benches and Ardlings strolling round it.
- **Badges slide in like on Steam**: from the right edge near the bottom right, the badge pops in with a bounce and a shine sweeps across, then it slides back out.
- **The builder is a real high bay** (captaincarl24's photo again): two 120 m steel work towers flank the rocket with braced columns, a deck every 8 m, yellow work platforms reaching in, handrails, lamps under every deck, lit work cabins, zig-zag stair towers, coloured service pipes, cable trays, level markers and a bridge across the top. A tower that stands between the camera and the rocket hides itself, so building is never blocked; plane builds hide both.
- **One joined-up space centre** (captaincarl24's picture as the guide, in our own shapes): every building now stands on paved plazas with kerbs, lawn islands and park trees, linked by wide lit walkways and a paved campus core with a cafe pavilion, a fuel depot and a maintenance shed. A round apron with a keep-out ring circles the pad, and a two-lane rocket road runs to the assembly building. A tracking station of three big dishes round an octagonal control building stands to the west; the research campus has two labs joined by a glass skybridge, a sawtooth workshop and an observatory dome; the runway moved in from 1.5 km to 680 m north of the pad, with a big arched hangar, a control tower, an ops building, an apron and taxiways beside it.
- **Buildings and grounds drawn in the shader** (after some research on making low-poly scenes look good): facades with floors and windows in four styles (ribbon windows, punched windows, curtain walls, ribbed industrial cladding), grime streaks, darker feet and gravel roofs, and lit windows at night; paving in 5 m slabs with joints and slab-to-slab colour; striped lawns; stronger, wider ambient occlusion; avenue trees along the campus road and a denser wood round the compound.
- **Semi-realistic buildings** (captaincarl24: "like Kerbal's graphics"): CC0 photo textures from ambientCG mapped at real scale: an office window grid, a concrete-frame facade, a glass curtain wall and ribbed metal cladding on the walls (the glass in the photos reflects the sky, and windows light up at night), gravel roofs, concrete paving slabs and real grass. Trees grow lumpy, shaded leaf clumps on limbs; the headquarters' palms have thin drooping fronds and its hedges are rounded bushes. The part texture set is now a 12-layer grid (Tools/art/build_part_textures.py; Editor/GraphicsImport.cs sets 6 rows x 2 columns).
- (Downloaded Sketchfab models were tried for the big buildings and taken out again: captaincarl24 thought they looked worse than ours. They're kept in Downloads/sketchfab/removed_from_game; glTFast is out of the project.)
- **captaincarl24's map** (his own picture of the space centre seen from the sea): the space centre stands on a **peninsula** about 5 km across, with a wide sand beach and the sea to the west, south and east, and the mainland (and the hills) to the north. Terrain.ArdenField is the one land/sea field that the ground colours, photo textures, water glint, hills, regions and ore all read, so they agree on the coast. The **main runway** now runs east-west 325 m south of the pad (its middle 740 m west), crossed near their south ends by **two parallel north-south runways** with taxiways; the **airport** (arched hangar facing the runway, control tower, operations building, apron with two planes) sits between them and the campus, with two small hangars between the runways and a fire station to the south. **Helipads** with a parked helicopter beside mission control; the **tracking station** (with a white radome) moved north; the **research campus** moved out past the runways; an **east launch complex** with its own assembly hall, a rollout shed and garages along the road out, and two rocket roads to **two more pads** near the coast with a tank farm; a **north-west complex** with a hall, a pad and a dish; warehouses and offices north of the campus; roads out to the mainland; palms along the top of the beach. All our own buildings (the game draws east on the left when you face north, so the layout is flipped east-west to look like the picture from the sea).
- **A busier space centre** (captaincarl24: "it looks so empty"): a staff town north of the campus (streets of about 80 houses with pitched roofs, apartments, a school with a sports field, shops with an awning, a supermarket, a park with a pond and a water tower, street trees and lamps); a visitor centre with a rocket garden of four rockets and coaches in its car park; an industrial park along the east road (warehouses, workshops, a propellant plant with a pipe rack and a vent stack); a substation and a solar farm with pylons; a harbour on the east coast with a long pier, two cranes and the landing barge with a booster standing on it; a lighthouse on the south-east point; an engine test stand; offices and car parks at the outer complexes; roundabouts and dashed centre lines on the roads, and many more roads. Traffic on every road (cars, vans, buses, trucks and tankers out along one side and back along the other), a plane taxiing beside runway 1, a fuel truck on the apron, forklifts, two crawlers taking rockets out to the east pads, and people in the town, the shops, the school field, the rocket garden, on the pier and round the complexes. Parked cars are simple boxes (the site mesh is about 500k vertices; the space centre still runs at 167-220 fps on an RTX 2060).
- **The builder's great door is open** (captaincarl24: "make it look like a rocket hangar ... the vertical doors open so you can see the outside"): the wall behind the rocket has an 84 m wide, 138 m high doorway in a teal frame with lift tracks, cable drums and hazard stripes, the door leaves raised and nested under the header. Through it: the apron, the two-lane rocket road out to the pad with its red service tower, floodlights and propellant spheres, mission control, headquarters and other buildings, lamps and trees, grass down to a beach and the sea, under a sky dome; a truck drives in and out and people cross the apron. The hall's walls are light corrugated steel now instead of dark metal. `-autotestHangar` (screenshots 380-387).
- **The hall fits the rocket** (captaincarl24: "the rocket is supposed to be as big as the hangar"): the building is sized to what's on the floor (HangarMode.HallFor): the work towers stand about as tall as the rocket, the roof a little above, the floor about as wide as the hall is high, the door nearly the full height. The Pathfinder gets an 84 m hall 30 m high with 16 m towers; the Longshot a 97 x 46 m hall with 32 m towers; the biggest rockets the old 340 m one. It's rebuilt as the rocket grows past each step. People, benches, crates and vehicles stay their real size (props and routes that don't fit are left out; the racks follow the walls), the outside moves with the door, and the camera stays inside: it comes in along its line of sight at the walls and roof.
- 3 new tests; `-autotestWords` (screenshots 350-356), `-autotestCampus` (359-376: the peninsula from the sea, the buildings close up and from the air, menu hidden).

### Milestone 25: overnight improvements (2026-11-13, captaincarl24 asleep: "keep improving")
- **Launch from any pad** (captaincarl24's pick): four pads (the main one, two beach pads out east, the north pad), each with a real deck at pad height. The builder's "From:" button goes main pad, beach pad 1, beach pad 2, north pad, runway (remembered). The two side runways are solid ground now, not just paint.
- **Roll-out** (captaincarl24's pick): a rocket going to a pad rides a crawler out of its hall's door (the assembly building for the main pad, the east hall for the beach pads, the north-west hall for the north pad), then comes up the last stretch of the rocket road to the pad, the camera swinging round it; no time passes meanwhile. SPACE, ESC or Skip jump straight to the pad; Settings has Roll-out On/Off. `-autotestRollout` (390-392).
- **Ordinary building colours** (captaincarl24: "not white and cyan"): stone, sand, warm grey, brick and darker stone walls chosen per building (WallTint), metal sheds in grey, sand, green-grey, brown or slate (CladTint), dark grey roofs and trims instead of cyan, a sandstone headquarters, a red fire station.
- **Better buildings outside**: office and lab blocks have gravel roofs with parapets and copings, rooftop plant with fan grilles, stair housings, a darker plinth and glass doors under a canopy; sheds have pitched metal roofs with gables and gutters, framed roller doors with slats, a person door with a lamp, and window strips.
- **Better builder hall inside**: a lit glass control room with a balcony along one wall, the emblem above the far door, air ducts along the side walls, red fire points on the columns, yellow walkway lanes, and stairs up to the catwalk.
- **More detailed rocket parts**: bolt rings, cable raceways with clamps and vent valves on tanks; gimbal actuators, propellant lines and a bolted plate on engines; bolted segment joints and attach lugs on solid boosters; little nozzle clusters, extra windows and an antenna on capsules; a bolted rim on heat shields, straps and a lid ring on parachutes, a tip on nose cones, leading edges and root fillets on fins.
- **Better people**: crowds dress for the job: the hangar crew and the workers round the pads, airport, harbour and industry wear hard hats over their helmets, hi-vis vests with reflective bands and tool belts and carry toolboxes; office staff wear lanyards with badges and carry clipboards; visitors have cameras and souvenir bags, and some are children. Everyone is a slightly different height, and they bounce and lean as they walk.
- **Rocks on other worlds**: stones and boulders round wherever you are near the ground, coloured from the ground they lie on (grey on Tal, rust on Ochre, pale on the ice worlds, black on Pyre), placed by a hash of world-fixed cells so they're the same every visit; Arden gets them only on high rocky ground away from the space centre.
- **Graphics**: the space centre's lights at night (floodlights on every pad, the assembly building's door, the campus road, the airport, helipads, town streets, the pier: about 40 real lights, on after sunset); the sea ripples and glitters near you; the close-up ground fades into the far planet at its edge, so there's no square seam from high up.
- **Physics**: splashdowns are gentler than landing on rock (a part takes 2.2 times the speed in water) with their own message; the harbour's barge deck and pier are solid, so a booster can land on the barge (a new badge, Sea Landing, 25,000 funds).
- **A clickable space centre** (milestone 25b): the builder's new Space Centre button flies up over the site, each building labelled (labels nudge apart so they never cover each other, a line down to each). Click one to go where it leads: the assembly building builds rockets, the airport hangar planes, mission control shows missions, the tracking station your ships, the research campus the tech tree, headquarters the facilities (the career button is called Facilities now), the astronaut complex your crew, the visitor centre your badges, and each pad sets where you launch from. Right-drag turns the view, scroll zooms, ESC goes back. `-autotestHangar` shot 388.
- Better conifers everywhere: four drooping tiers in varied greens, leaning a touch.
- 1 new test (LaunchPads: four pads, side runways, the barge). The landing-legs test now lands on grass (its old spot is sea) and also checks a splashdown survives.

### Milestone 26: countdown, booster recovery, craters and mountains (2026-11-13)
- **A real countdown** (captaincarl24's pick): on a pad, SPACE starts T-10 with a beep each second; the swing arms on the service tower pull away (T-8 to T-5; every pad's arms are their own moving parts now), water floods the pad and white steam billows out and rolls along the ground (from T-6), and at zero the first stage fires. SPACE again launches at once; Settings has Countdown On/Off. `-autotestCountdown` (393-396).
- **Booster recovery** (captaincarl24's pick): coming down over Arden near the space centre, a landing guide shows the nearest place to land (the barge or one of the pads), how far, an arrow toward it turned to match the camera, and how far off the ship will come down if it keeps falling as it is; a tall orange beam of light marks the spot. Spent stages that land on the barge or a pad deck are recovered for their value by themselves.
- **Craters and mountains** (captaincarl24: "craters like on the moon ... mountains and hills"): Tal and Mote get ridged mountain ranges across parts of the highlands (up to 2.6 km on Tal, less on small worlds), rolling hills, deeper mid-size craters and a new layer of small craters; Vesk and Rime get ice mountains, hills and two more crater layers; Ochre gets mountain ranges, hills, more craters and the Great Mound, a shield volcano about 6 km high and 70 km across with a summit caldera; Pyre gets hills. The close-up ground is finer when you're low over other worlds, so the small craters show. Highest-ground limits raised to match. From high up, crater rims and ridges are shaded from the baked relief (sharper than the close-up ground's vertices then), and the rocky worlds you land on (Tal, Ochre, Pyre, Rime) have maps twice as sharp (1024 per face; Vesk 512). The first start after this update spends about 3 minutes baking them in the background (then they're saved). `-autotestScenery` (397-399b): Tal's crater fields and mountains, Ochre's Great Mound and its caldera, without the HUD.
- Fixes: rocks never lie right where the ship is (it lands on smooth ground); fewer, softer lit windows at night that fade with distance, so far-off buildings don't look like glowing slabs.
- 1 new test (WorldTerrain).

### Milestone 27: places to discover, engine dust and prints, photo mode, weather, docking guide (2026-11-13)
captaincarl24: "add all of this" (my five ideas).
- **Places to discover**: nine on seven worlds: the Old Stones on a hilltop 53 km from the space centre, the Fallen Probe in a crater on Tal and the Needle (a 180 m rock spire), Frost Geysers on Vesk, the Great Arch (400 m across) and the Great Mound's caldera on Ochre, the Balanced Stone on Mote, Lava Caves on Pyre (glowing inside), the Crystal Crater on Rime. Flying low over one (under 80 km, within 60 km) or mapping it with a survey scanner puts a "?" on the map and a guide on screen (distance, arrow, a blue beam within 80 km); landing beside it finds it: science, funds, its story, and the Explorer (1) and Globetrotter (5) badges. Kept in the save.
- **On the ground**: engines firing near the ground blow dust out along it (straight out on airless worlds, billowing where there's air, white spray over water; none on pads, where the countdown has its steam). Ardlings leave boot prints and rovers leave tyre tracks (up to 2,400 marks, lying on the ground as it's drawn).
- **Photo mode** (F2, or the pause menu): freezes the flight, hides the HUD, the camera flies free (WASD, Q/E, SHIFT; right-drag, scroll); looks (normal, vivid, warm, cool, black and white, old photo, dramatic, dreamy), time of day (the light only), zoom, brightness; SPACE saves a picture to the Photos folder. (Found on the way: post effects only run on the last camera of the stack, so the game's post look had never been on in flight; photo mode turns it on for itself only.)
- **Weather** on Arden and Ochre, the same in every game: drifting weather systems on Arden (clear, cloud, rain, stronger wind in storms; rain about a fifth of the time), dust storms on Ochre. Wind pushes ships in the lower air (more with height, none above 25 km): a capsule under its chute drifts about 1.2 km a minute in a strong wind. Rain streaks, grey haze and dimmer light; orange haze and blowing dust in storms; dust devils on calm Ochre days. The countdown holds once when the wind at the pad is over 15 m/s. A weather line above the navball. Badge: Storm Chaser.
- **Docking guide**: near a target with a free port, a scope shows where you are off their port's line and which way your port faces, the closing speed against a comfortable one and a line of advice; a box marks their port. **Space station contracts** at Arden, Tal and Ochre (ships docked into one, seats, a free port, antenna, solar). Badges: Space Station, Say Cheese.
- 6 new tests. `-autotestExplore` (400-416).

### Milestone 28: full graphics, brighter nights, the story, replays, practice tools (2026-11-13)
captaincarl24: "do those all" (my five ideas).
- **Full graphics on**: the post effects (film-like tone mapping, bloom on lights, flames and lava, colour, vignette, eyes adjusting to the dark at night) now run everywhere; they never had in flight, because URP does them on the last camera of the stack and that camera had them off. Bloom is capped so engine flames and the sun don't wash the screen white.
- **Brighter nights**: more starlight, and planetshine: light off a big world in the sky. A full Arden lights Tal's night side (up to a sixth of sunlight, shown as a glowing disc in the sky), Tal lights Arden's nights a little, Brume its moons. With the eyes adjusting, night landings are possible.
- **The story**: twenty missions in five chapters (First Steps, To Tal, Building Up, Ice and Dust, The Giant), from leaving the pad to finding five hidden places. Mission control (Rosa the flight director, Bram the chief engineer, Dr Tilly the scientist) briefs each one in flight and says something when it's done; a goal line above the navball; a Story screen in the builder; pays in career; Settings has Story missions On/Off; a save that's already done some of it skips ahead quietly.
- **Flight replays**: every flight is recorded (ten samples a second, a copy of the ship whenever it changes shape, thinned past 20,000 samples). "Watch replay" (pause menu, or after a crash) plays it back: the Director (a tower camera tracking the launch with a long lens, then flybys and chase shots in turn), or Chase, Tower and Flyby by hand; 0.25x to 8x; a timeline to jump about; stages dropping, chutes opening, engines lit; a crash ends in its explosion.
- **Practice tools** (sandbox, pause menu): refill everything; orbit any world at any height; drop 3 km above any world by day to practise landing, or beside any hidden place; jump to sunrise, noon, sunset or night (by time warp, or a straight clock jump when flying).
- Fixes: hidden places aren't spotted from the launch pad (sight grows with height); the guide shows once you're flying.
- 4 new tests. `-autotestReplay` (420-429).

### Milestone 29: Mission Design (for the NASA Space Apps "Space Mission Design Game" challenge) and team rooms (2026-11-13)
captaincarl24: "for multiplayer make it so like you can create a room and other people join your room ... 4 people playing on their own device; add the rest".
- **Mission Design** (title screen): plan a whole mission on the main game's real worlds. Pick the goal (Storm Watch, Map Tal, Talk to the Far Side, Ice Hunter, Red Planet Orbiter, or a free design: any world, anything to study); build the spacecraft (frame, solar wings or a nuclear battery, battery, pointing, computer, engine, heaters); choose instruments (each with its mass, power, cost, data and science, and what it's good for), who uses the data, the orbit (low, polar, sun-synchronous, medium, high, stationary, each explained, with its real height, period, shadow, coverage and detail), the rocket (what it lifts to each destination, its price and how often it works), the antenna and the ground stations. Advanced parts hide on Easy words.
- **The dashboard** shows mass against the rocket, power by day and the battery through the night, budget (building plus running costs over the lifetime), data made against data that can get home (falling off with the square of the distance), contact time, fuel (to slow into orbit and keep it up) and lifetime, with every problem explained in plain words, and the design's maturity (after NASA's Concept Maturity Levels). Pointing at anything says why it matters. An example design for each mission to start from.
- **Fly it**: a day-by-day simulation (launch reliability, the trip and orbit insertion, power and night-time shortfalls, data lost when it can't be sent, blur from too high or rough pointing, coverage, drag, cold, wear, solar storms, meteoroid hits, computer restarts) played back over the world with a log and live gauges; then a **report**: goals met, a score out of 1000 and stars, what went well, what to change and why, charts, and the best before. A **leaderboard** on each computer; designs can be **saved** and **shared** as a code.
- **Team rooms** (up to 4, each on their own device): create a room (it appears in everyone's list on the same network, and has a code like 38RL-DF8P to type), join from the list or with the code. The four jobs (mission lead, spacecraft engineer, science lead, comms and ground) are shared out (with fewer players, people hold two) and can be swapped; each player may only change their own part, everyone sees the same design live, can chat, ask a job for something (with quick asks) and answer yes or no; everyone says ready, the host flies it and every screen gets the same result, with up to +100 for teamwork. Works between Windows and Mac on the same network (some school networks block it).
- 6 new tests, including a real four-player room over the network. Tours: `-autotestMission` (430-438), and four copies at once with `-autotestMissionHost` / `-autotestMissionJoin -mdName X -mdCode 127.0.0.1:47770` (440-445).

### Milestone 30: Multiplayer: build and fly a rocket together (2026-11-13)
captaincarl24: "rename it to multiplayer ... the whole game is supposed to be based off this ... all 4 people have different roles: one builds the rocket while the other 3 comment on it and suggest changes, one drives the rocket, one does stuff".
- The title button is now **Multiplayer**: create a **rocket room** or a **mission design room**, join one from the list (it says which kind) or with its code, or do mission design on your own.
- **Rocket room, building**: the **builder** builds in the hangar as usual; everyone else watches the same rocket appear on their own screen with its stages and speed budget, clicks a part to **comment** on it (or the whole rocket) and has quick suggestions ("Add more fuel", "It needs a parachute"...); the builder ticks comments off or turns them down. **Mission control** picks the goal (10 km, space, orbit, Tal, land on Tal, there and back, or free flight) and where to launch from. Everyone says ready; the builder launches. Jobs can be swapped while building; with fewer than four players people share them.
- **Rocket room, flying**: the host's game flies the rocket and sends it to everyone twenty times a second (smoothed), with a full copy of the ship whenever it changes and twice a second for fuel and power. The **pilot** steers and works the throttle and SAS; the **flight engineer** stages, aborts and runs the gear, lights, brakes, chutes and RCS (and the countdown); **mission control** plans maneuvers, sets targets, runs time warp, watches the goal and ends the flight; the **builder** runs the science and the radio. Each player's game only lets them do their own job ("That's the pilot's job (Mia)") and passes it to the host, which checks it again. The host's messages appear on every screen; the flight carries on when someone pauses; a goal reached is announced; ending the flight takes everyone back to building with a summary. Rooms use their own sandbox (nothing saved).
- 2 new tests over the network; a tour with four copies of the game (`-autotestCoopHost`, `-autotestCoopJoin -mdName X -mdCode 127.0.0.1:47770`, 450-455).
- **New title menu** (captaincarl24: "singleplayer which branches to sandbox and campaign, and multiplayer which branches off into also sandbox and campaign and then making a room or joining a room, up to 4 people with a room code"):
  - **Singleplayer**: Campaign or Sandbox (then the new-game page, already set to that mode), Load a game, Mission design on your own.
  - **Multiplayer**: your name, then Campaign or Sandbox, then **make a room** (its name, and the kind: Rocket or Mission design) or **join a room** (the list shows each room's kind and mode, the chosen mode first; or type the code). Up to 4 players.
  - **Campaign rocket rooms**: the team plays the host's own space program, kept on the host's computer as the save "Multiplayer: room name" (made the first time, carried on next time with the same room name; it can also be loaded on its own from Singleplayer). The host's money, science, research, contracts and crew go to every player once a second when they change, so the builder only sees parts the team has researched and the launch costs real money. Research, missions and facilities are the host's to run (from the builder's bar, or from the top of the watch screen when someone else builds); everyone else sees the money. A launch the team can't afford is refused with a message. Ending a flight on the ground at home recovers the ship for its refund. Sandbox rocket rooms still play in their own fresh sandbox (nothing saved).
  - **Campaign mission design rooms**: missions open one by one (Storm Watch, Map Tal, Talk to the Far Side, Ice Hunter, Red Planet Orbiter); 1 star or more opens the next, from the host's results; locked ones show as locked and can't be picked; no free design. Sandbox rooms and mission design on your own have everything open.
- 2 more tests (the career reaching a player; missions opening in order). Tours: `-autotestMenus30` (460-467); four copies with `-coopCampaign` on the host (450-456).

### Milestone 31: the Space Apps check (2026-11-14)
captaincarl24: "review the resource and check if our game matches; also remove the mission design game thing from single player".
- **Checked against the challenge page and its Resources tab.** Every objective was already covered: design, manage and simulate a whole mission; mission objective and requirements first; spacecraft parts (structure, power, batteries, communications, computer, attitude control, instruments) with simple and advanced versions; instruments with mass, power, cost, data and science value; orbits explained, with coverage, shadow, contact and lifetime; launch vehicles with capacity, cost and reliability, and a check that the mass fits; the six resources on a dashboard; up to four players with roles, requests to negotiate, and a teamwork bonus; a simulation with random events, a score and a post-mission report; scenarios, difficulty (word levels), visuals, a leaderboard, save/share, sandbox (free design); the four mission segments (launch, space, ground, user) and concept maturity levels from the resources.
- **Filled the gaps:**
  - **Compare two designs** side by side (the challenge asks to "save, share and compare"; the Mission Design Lab calls this a trade study): in Designs, tick two (this design and a saved one, or two saved ones); 13 lines (rocket spare, cost, spare power, night battery, data home, contact, spare fuel, lifetime, science a day, build time, maturity, problems), the better value in green, and a verdict ("there's rarely one perfect design").
  - **Real missions like this** (after the resources' telescope game, which shows the real NASA missions a design resembles): on the Mission tab and in the report, e.g. NOAA-20/21 for Storm Watch, the Lunar Reconnaissance Orbiter for Map Tal, Queqiao and Mars relays for the far side, Lunar Prospector, MRO's radar and Europa Clipper for ice, Mars Reconnaissance Orbiter for Ochre; plus MarCO for a CubeSat frame and Voyager/Curiosity/Perseverance for a nuclear battery.
  - **Teams racing** (the challenge's "competitive scenarios where different teams design missions to solve the same objective"): each mission design room announces its mission and its best score; other rooms on the same network on the same mission show up under "Other teams on this mission" (Mission tab and report), with who's ahead.
  - **How to play** has a new **Multiplayer** page (Easy: "Playing together"): rooms, the jobs, mission design, comparing designs, racing.
- **Singleplayer** is now Campaign, Sandbox and Load only. Mission design is under Multiplayer; a mission design room on your own still works (you do every job).
- 4 new tests. Tours: `-autotestMatch31` (470-476); two copies racing with `-autotestRival -mdName Comets` / `-mdName Rockets` (477-478).

### On GitHub (2026-11-14)
- The project is in a GitHub repo with a README. Only Assets, Packages, ProjectSettings, Tools and this roadmap go in; builds go in Releases. The 124 MB star map is stored with Git LFS.
- The maker is credited by GitHub username (captaincarl24) everywhere, including the Credits page.
- **New company name**: captaincarl24 (app ID `com.captaincarl24.ardenspaceprogram`). Unity keeps saves in a folder named company/game, so the first launch copies saves, designs, sub-assemblies and photos from this game's folder under the old company name (the old folder stays as a backup; test runs aren't copied). Settings kept by Unity itself (volumes, changed keys, the countdown, rollout and story switches, the last pad) start from their defaults once.
- 1 new test (the folder copy).

### Milestone 32: an easier builder, an interface that fits, a tutorial, steadier multiplayer (2026-11-14)
captaincarl24: "make it easier to build ... sometimes the entire rocket likes to disappear, run your system and find every single bug", the
interface overlapping on a 15" MacBook Air, off-centre button text, a parts search, 10 more presets, an interactive tutorial, Backspace to
delete, parts staying selected, more parts, better physics, Easy-mode building help, and "fix multiplayer ... insanely buggy and too much buttons".

**Builder bugs found and fixed**
- **The vanishing rocket**: clicking the root part picked up the *whole rocket*; the next click on the parts list (or ESC) threw it away
  without a word. The root now stays put (it explains why); "Pick up all" became "Clear the rocket", which asks twice.
- A part picked up off the rocket is no longer thrown away when you let go: ESC or right-click puts it back where it was; choosing another
  part from the list puts it back too. Deleting is on purpose: Backspace, or dropping it on the parts list (undo brings it back).
- Deleting a symmetry group whose ids the root shared deleted the root as well, breaking the rocket (`DesignOps.RemovalSet` never includes it).
- The see-through "ghost" part stayed behind in rocket rooms when the builder's rocket arrived; it's cleared now.
- 3,000 random edits (add, symmetry, delete, move, re-root, save/load) never break a rocket's tree (`DesignOps.TreeProblem`).

**Easier building**
- A part you add from the list stays in your hand for more copies (right-click or ESC puts it down). Backspace (the Mac's delete key) deletes;
  Cmd+Z undoes on a Mac as well as Ctrl+Z; Ctrl+Shift+Z / Ctrl+Y redo. Stack nodes catch the mouse from further away (48 px).
- **Search** at the top of the parts list (name, kind, category, description; several words narrow it down). Taller part cards: the second
  line isn't cut off any more.
- **Easy words**: no Offset/Rotate/Root tools, mirror, snap or marker buttons; "One at a time / 2 at once" instead of Symmetry; a
  **checklist** (capsule, tank, engine, parachute, fins, strong enough to take off) with "Show me" buttons that find the parts; plain-words
  tips on every part card and when pointing at a part of the rocket (`BuildHelp`).
- **10 more presets** (22 in all, scrolling): Sky Hopper, Science Hopper, Abort test, Twin Booster, Orbiter, Tal Lander, Satellite launcher,
  Big Lift, Quad Booster, Station Core. Each is checked to be valid, lift off (push/weight 1.15+) and have the speed budget for its job.
- **10 more parts**: FT-3, FT-2L and FT-16L tanks, Swift Sustainer and Condor Vacuum Engine, Flint and Blaze solid boosters, a 2.6 m nose
  cone, a 2.6/1.3 adapter and a big radial fin, with prices and places in the tech tree.

**The interface**
- It fits any screen by itself: the layout space is never smaller than 1480 x 900, scaled to the window; the size setting is Auto / 90% /
  80% / 70% (sizes over 100% overlapped, so old ones read as Auto).
- The builder's top bar fits the width: what doesn't fit moves into "More ▾". (At MacBook shapes the second row used to overlap.) The money
  line clips instead of running under the buttons; warnings, hints and the guide stay in the open middle.
- Switched-on buttons (the tool in use, the chosen option) had their text in the top-left corner (they used the list-row style); they have
  their own centred style now, and every button's text is centred with even padding.
- Clicking anywhere leaves a text box; while you type, the game's keys stay quiet (typing in the flight chat used to stage the rocket).
- The layout check now sees every button (`Ui.Press`) and reports buttons landing on buttons; the window-size tour adds MacBook shapes.

**Interactive tutorial** (`Tutorial`): the first game opens a box that points at each part of the screen, dims the rest and waits for you:
the parts list, the search, put down a capsule, a tank, an engine and a parachute (the list filters itself to the right parts), the stages,
undo, LAUNCH; then in flight light the engine, read height and speed, steer, open and close the map. Skip any time; File > Play the tutorial
plays it again. A rocket already in the hangar? Start fresh, or fly that one.

**Physics**: drag now rises through the speed of sound (x1.45 just past Mach 1, easing back to normal by Mach 3) with the speed of sound
falling with height, so max Q is the roughest part of a launch, as in real life. Reentry and aerocapture (far faster) are unchanged, and the
planners' drag forecasts use the plain drag. The flight display shows the Mach number (under More).

**Multiplayer**
- One screen instead of three: your name once, host a game on the left (campaign/sandbox, rocket/mission design), the games on your network
  on the right with Join, and a code box. It listens the whole time it's open.
- The host's game no longer freezes when a player's connection is slow: each connection has its own sending thread and queue; flight frames
  that back up are skipped. Both ends send a heartbeat, so a player whose Wi-Fi drops (or laptop sleeps) is noticed in 15 s.
- Jobs are kept when someone joins or leaves (they used to be dealt out again from scratch); taking a job never leaves anyone with none.
- A room that closes takes you back to the multiplayer screen saying why (it used to drop you on the title screen). Rooms are announced on
  each network's own broadcast address too, and stay listed through a lost announcement or two.
- A crash at the end of a rocket room's flight (the flight recording looking at a ship being taken apart) is fixed.
- Fewer buttons: the job buttons hide behind "Change jobs"; four quick comments instead of seven; the goal and launch site are plain words for
  everyone but mission control; the team, comments and chat no longer overlap each other.
- 8 new sim tests; `-autotestBuilder32` checks the builder fixes in the running game (480-492); `-coopPlayers` runs 2-4 player rooms.

### Milestone 33: Expedition, first try (2026-11-14, replaced by milestone 34)
A co-op survival road trip "but rocket and plane version" (captaincarl24's earlier design): a small arcade ship built from loose parts on
an asteroid, flown along a lane of stops. captaincarl24 turned it down ("we're building a rocket and flying it like the rocket builder in
singleplayer ... and why is every planet so close to each other, it's supposed to be very large"), so milestone 34 rebuilt Expedition on
the real game and the arcade version was removed. It was never released.

### Milestone 34: Expedition on the real game, cabins, a friendlier interface (2026-11-14)
captaincarl24: "yes like that" to the Expedition redo (the real builder and parts, the real full-size solar system, walk-in interiors,
survival, outposts to scavenge, a budget and a report), plus "can't see the flames for people joining the rooms, hide UI option, better
menu screen, update log, research good UIs ... the chatroom blocks half the screen", and later: definitions on hover, your own presets
bought with money, Ardlings getting out in first and third person, an Among Us-style Ardling menu, paint and decals, and "make the weather
more random, every time I start a new world it's always storming".

**Expedition** (Multiplayer: Expedition or Rocket room; works alone too)
- **Everyone builds** the one rocket in the real builder with the real parts (each change goes to the room; someone else's change comes in
  when you're not holding a part). Expeditions play in their own sandbox with supplies on.
- **The objective first**: six missions (First steps on Tal, The lost outpost, Ice of Vesk, The red planet, The giant's moons, Free
  expedition), each with goals worth points (land, walk, plant a flag, orbit, scavenge, bring the crew home), a budget, a speed budget and
  trip time, and the real mission it's like (Apollo 11, Artemis, Europa Clipper, Perseverance, Galileo/Juno/JUICE, Voyager). Easy has half
  as much money again, Hard 20% less (and scores 15% more).
- **The six resources** in the plan beside the team: budget, mass and push at lift-off, speed budget, time (supplies against the trip),
  power, data (instruments), comms (antennas), and seats; what stops a launch (over budget, not enough seats for every player) and what's
  risky, in plain words.
- **The players are the crew**: launching seats every player in the rocket by name, wearing the Ardling they made. The flight is the real
  game (the host flies it; pilot, flight engineer, mission control and science jobs as in a rocket room).
- **Survival**: food, water and air from the ship's supplies (and the suit's on foot), health falling once they run out; your Ardling's
  supplies and health in the expedition panel. Lose Ardlings and the report says so.
- **Outposts and wrecks** on Tal, Vesk, Ochre, Mote and Rime (⌂ on the map): land within 300 m and Scavenge for fuel (into the tanks, not
  boosters), thruster fuel, supplies and science, once per game. In every game, not just expeditions.
- **The report**: a score out of 1000 (goals 800, money left 100, everyone alive 100), stars, what went well, what to change and why
  (with numbers: "the rocket had 4,100 m/s and the trip needs about 5,500"), and a leaderboard per mission on each computer.

**On foot and inside**
- **ENTER climbs out** after any landing the crew lived through, a crash included if the capsule's in one piece.
- **First and third person** (C): through the helmet (the mouse looks round, A/D step sideways, ALT frees the pointer) or behind (the
  camera swings round behind you as you walk). CTRL runs; jumps are higher (half a metre on Arden, nearly 3 m on Tal).
- **Walk-in cabins** (TAB): a room for each crewed part (round capsule cabins with seats, controls and a hatch; long habitats with bunks, a
  table and plants), bigger than the part, drawn by its own camera over the world's. Its windows are holes, so through them you see the
  real world from inside the part, looking the way you look. E at the seats takes the controls; E at the hatch goes out on EVA. Crew sit in
  their seats; in a room the other players walk round with you.

**Make it yours**
- **My Ardling** (Multiplayer screen): suit colour, the face on your visor note, the note's colour and a hat, with your Ardling turning in
  a preview. It goes with you into every room: the team list shows everyone's note and colour, and their Ardlings look the way they made them.
- **Paint and decals** (Paint in the builder): 16 colours on most body parts (click parts, or paint the whole rocket); decals (stars, rings,
  stripes, numbers, Ardling faces and more, or your own PNG/JPG pictures from the Decals folder) stuck where you click, sized and turned.
  Your own pictures are stored small in the rocket's design, so they go into saves and to the other players.
- **Presets** is its own window: the 22 ready-made rockets, and up to 12 slots for your own (the first free; in a campaign the next costs
  5,000, then 2,500 more each; a sandbox has them all).

**Easier to read and play**
- **A new title screen** (after looking at how good game menus work): Continue first and biggest, saying which game it opens; one column
  of big buttons, each saying what it's for; the newest update's highlights in a **What's new** card (marked NEW until read) and a full
  **Update log**.
- **Definitions**: rest the mouse on a game word anywhere (every label and button) for 2 seconds and a box says what it means; 101 words
  in all three word levels; a searchable Definitions page on the title screen, in the builder's File menu and the pause menu.
- **Hide the interface** with F1 (or the Hide UI button in flight, the File menu, the pause menus); a faint reminder shows how to bring it
  back.
- **Multiplayer panels fold away** into one slim bar (the team, comments and chat used to cover half the screen); the choice is kept.
- Settings: how see-through panels are, how fast definitions pop up.

**Fixes**
- Players who join a room now see the engine flames, smoke, reentry glow, parachutes and legs (they were only worked out in the host's
  physics).
- **Weather is different in every game**: it followed only the time and place, and every new game starts at the same time on the same pad,
  so every game began in the same storm. Each game now has its own weather seed (older saves get one too), and storms are a little rarer.
- The version number is the milestone again (0.34).

- 7 new sim tests (definitions, the update log, preset slots, paint and decals, the expedition's rules, outposts, the weather);
  `-autotestExpedition` plays a whole expedition (600-606); the size tour covers the new title screen, settings, update log, definitions,
  presets, paint, decals and My Ardling.

### Milestone 35: lessons, the space centre first, better crashes, the sea, Ardlings (2026-11-14)
captaincarl24: "add more tutorials like how to get into orbit, how to land on the moon, how to dock with another space station; make easy
mode even simpler, not the text but the mechanics as well; once you load into a single player game you can select which buildings, like a
Kerbal type main menu; make the launch button green; an item rated 150 breaks between about 140 and 170; more realistic crashes instead of a
circle; land in the ocean and bob around, and swim if you leave; why do Ardlings have infinite flags, give them a limit of 3; better
Ardling animations, a slightly smaller helmet and a better sticky note face."
- **Lessons** (File > Tutorials in the builder, beside the first tutorial): *Get into orbit* (on the pad with an orbiter: lift off, the
  gravity turn, raise the highest point, coast, round it out), *Land on Tal* (a lander 25 km up round Tal: point back, slow down, legs,
  the landing burn, touch down), *Dock with a station* (a ferry 70 m behind a station: SAS and RCS, line up, drift in, latch). Each step
  checks the flight and moves on by itself; crash and Try again. Lesson ships are free and don't stay in the save.
- **The space centre first**: loading or starting a game opens the space centre from the air; click a building (or Rocket builder).
- **Easy helps you fly**: parts half as tough again; a stage that runs dry with engines next fires the next one; parachutes open once
  they won't tear; legs come down near the ground; SAS starts on.
- **The Easy autopilot** (captaincarl24: "for easy mode, make it so it can autopilot to the moon and autoland"): a bar above the navball
  on Easy: *Fly to Tal* / *Fly to Vesk* launches to orbit if needed (the launch autopilot), plans the transfer (a half-orbit transfer
  timed for the moon, then fine-tuned over nearby times and sizes with the trajectory predictor to arrive 20-60 km up), auto-burns it,
  warps to the moon's gravity, burns into a low orbit at the closest point, then lands: a burn to fall, a part-way warp, and a landing
  burn that starts while there's room to stop (all the speed counts) and holds a falling speed down to about 1.6 m/s, legs down.
  *Land* does the last part from any moon orbit; round a world with air, *Come home* drops into the air for the parachutes. X stops it.
- **Parts vary**: each part's limits (crash speed, heat, air pressure) are its rating times 0.93-1.13, picked when it's built and kept in
  saves; the crash message says the rating and where this one gave way. (Fixed at exactly the rating in the tests and tours.)
- **Crashes break apart**: every part flies off on its own from the blast, tumbles, falls, bounces, skids and comes to rest on the ground
  (worked out over the turning ground), scorched, the biggest trailing smoke; the fireball is a cluster of bursts instead of one round
  flash, with a smaller smoke cloud so the wreckage shows.
- **The sea**: a ship down on the water bobs and rocks; an astronaut swims (slower than walking, no jumping out), front crawl when moving
  and treading water when still, head just above the surface.
- **Ardlings**: new poses for running (bigger strides, leaning in), jumping (arms up, knees bent), treading water and swimming, an idle
  sway; a smaller helmet (0.27 m, a taller collar); sharper sticky notes (256 px) with shine in the eyes, rosy cheeks on the happy faces
  and a curled corner. 3 flags per trip outside.
- **Colours**: green LAUNCH (builder and the pad), Start, Done and Ready; red Abort and End flight.
- **Credits roll at the end** (captaincarl24: "the credits on the title screen are kinda bothering me ... once you complete the game it shows
  the credits, Minecraft type"): the Credits button is gone from the title menu (a small link in the corner plays them); finishing the
  story rolls them once, after mission control's last word, scrolling up over the dark (hold SPACE to speed up, ESC to skip).
- **Cabins you can use** (captaincarl24: "for multiplayer, make the inside really detailed and interactive, super interactive"): look at
  something (the dot turns yellow and says what it is) and press E or click. *The controls work*: STAGE (a big red button), ABORT (under
  a clear cover: E lifts it, E again aborts), SAS / RCS / LIGHTS / GEAR switches that flip, a throttle lever (scroll on it, or E for
  full and cut), the map screen; in a room each still needs that player's job. *Live screens*: FLIGHT (world, height, speed, climb, g),
  ORBIT (high and low points, tilt, the next burn), FUEL (fuel, stage, throttle, signal, food), a mission clock (T+), the radio's last
  messages, and warning lamps (FUEL, HEAT, SAS, RCS, GEAR, LIGHTS). *Things to play with*: the radio (quick calls: to the team's chat
  in a room, or mission control answers), cabin lights (bright, dim, night red), a blind on every window, the snack locker (its door
  swings open; grab a snack), a plant that grows when watered, an Ardling plushie on a string (it swings under thrust and floats in
  space), a fire extinguisher; in habitats a bunk to nap in, an exercise bike with turning pedals and a game of chess. The hatch wheel
  spins as you open it. *Detail*: rivets and seams on every wall, gas and water pipes, handrails, cables, vents, floor plates, a ring of
  lights, oxygen tanks, a first-aid box, harnesses and headrests, a clipboard checklist, labels everywhere. *Multiplayer*: the others
  walk round with their arms and legs swinging and their names over their heads; the blinds, locker, lights, plant, plushie, bike and
  chess are shared (a `cs` message the host passes on as `csr`; each cabin remembers how it was left, for whoever comes in next).
  (The habitat's window bays now fit the room too.)
- 4 new sim tests (flags, toughness, lessons, swimming); `-autotest35` tours the space centre, tutorials, lessons, a crash, the sea,
  swimming and the new look (700-713); `-autotestTrip` flies a Tal Lander from the pad to Tal and down on the Easy autopilot (720-729);
  `-autotestCabin` uses everything in a capsule (and launches from its STAGE button), then a habitat, and goes out of the hatch (730-747).

### Milestone 36: a cabin you could live in (2026-11-14)
captaincarl24: "more detailed bro, search online to see how other popular games did it and do it". The research, and what came of it:
- **A kit of props, used everywhere** (cockpit builders for the genre's interiors work from prop packs; big space games mix the
  same parts per ship): FlightCabinKit.cs makes toggle switches (some under hinged guards), circuit breakers (a white collar
  shows when pulled), rotary knobs with tick marks, backlit push buttons, indicator lights and needle gauges, and puts them in
  panels. Every one can be pressed, with its own sound (clink, thud, click).
- **The overhead panel**: 16 circuit breakers, and the real ones work (FANS stops the streamers and the hum, PANEL LTS darkens
  the labels, FLOOD the flood light, RADIO kills the radio, NAV freezes the attitude ball and the keypad, CO2 SCRUB sets off the
  alarm; the rest answer back: the heater, the coffee, the toaster...); FLOOD, PANEL, VOLUME and TEMP knobs (scroll to turn);
  guarded O2 PURGE and WATER DUMP, FAN BOOST; LAMP TEST lights every lamp in the ship; a cabin temperature readout; and a
  keypad computer (programs 1-5 show height, speed, fuel, the high point and the mission clock; 0, 7, 42 and 1234 do other
  things). Its cables run to the walls, zip-tied.
- **The numbers on real instruments** (flight games that keep everything in the cockpit rather than on a floating HUD): an
  instrument strip on the desk with needle gauges for fuel, power, climb and g (red ends), and an attitude ball, the game's own
  navball, turning with the ship. MASTER ALARM flashes and beeps for new trouble (low fuel, heat, no signal, food, power, CO2)
  until pressed; CAUTION stays lit while any of it lasts.
- **Light in layers** (flight sims split panel backlight, flood and dome light): bright/dim/night modes, the FLOOD knob on top,
  the PANEL knob for the backlit labels.
- **Shake the cockpit, not the camera** (the advice from VR cockpit games: a shaking view makes people sick): thrust, thick air
  and reentry rattle the loose things, sway the cables and wiggle the needles, the lights flicker; the view stays still.
  Reentry glows orange in the windows.
- **Lived in** (photos of the ISS and Apollo cabins: cloth bags, Velcro, bungees, cables, labels everywhere): stowage bags with
  straps and tags, a Velcro strip with a tablet (its checklist ticks itself), a camera and a torch, bungee cords holding a food
  pouch and a drink bag, a photo from home, a mission patch with the crew's name, quilted wall pads, placards, sticky notes
  (Ardlings would), glass with a glint and bolts in every window, vent streamers fluttering in the air flow, the fans' hum.
  Seats got bolsters, armrests, foot rests and a hand controller; habitats a food warmer.
- **Weightlessness**: a pen, a pouch, a note, a sock and a bolt float about in space, drifting on the fans' breeze and bouncing
  off the walls (push them!); with weight (thrust, landing) they fall and settle on the floor.
- The world's own ambient occlusion stays off inside; the cabin's fill light was lowered to match. `-autotestCabin` now also
  tests the overhead panel, the gauges and the clutter, floats things in orbit and glows in reentry (750-757).

### Milestone 37: realistic cabins (2026-11-14)
captaincarl24: "make the interior more detailed and more realistic, add some textures".
- **Real light**: the cabin material no longer takes the sun (`_SunScale` 0 in RocketLit, which shone through the walls) and has
  its own even indoor fill instead of the sky's; real point lights that only light the cabin layer do the rest: the ceiling
  lamp(s) (white, off when dim, red for night), a flood lamp over the desk (the FLOOD knob) and a blue screen glow. Light
  pools under the lamps and fades into the corners; the lamps flicker in reentry and light up for LAMP TEST.
- **Sunlight through the windows**: each window's opening is projected along the real sun direction (turned into the cabin's
  frame) onto the floor as a warm patch that moves as the ship turns, only when the sun is outside that window and its blind
  is open.
- **Corners like a real room** (`_CabinAO`): vertical surfaces darken toward the floor and ceiling, floors and ceilings toward the
  walls, worked out from the room's shape in the shader, smooth (none of the screen-space grain).
- **Textured surfaces drawn in the shader** (RocketLit ids 11-16, nothing to download): woven cloth (seats, harnesses, bags,
  bedding, bungees), studded rubber floor with scuffs, painted panels (orange peel, seams every 60 x 50 cm, fine scratches),
  quilted pads, perforated metal (vents, the radio's speaker), hook-and-loop. Each has real relief (bumped normals), fading
  out with distance so nothing shimmers. Rails, floor plates and window frames use the photographed brushed metal.
- The crew and the plushie are lit by the lamps too.

### Milestone 38: the survival kit (2026-11-15)
captaincarl24: "add the rest of all the systems in the doc". Stage one of three, in Arden's own style (our own items, numbers
and look; nothing copied from the game the doc described):
- **Health, hunger and stamina** for every Ardling (Sim/Survival.cs, kept in the career). On foot the stomach empties in about
  18 game hours; aboard a ship with supplies the crew eat and it fills. Running (CTRL) uses stamina, which comes back resting
  (slower when hungry); run dry and you walk until it's back to 30. Landing faster than 5.5 m/s hurts, so does more than 8 g
  (12 on Easy) and a cabin hotter than 92% of its limit; an empty stomach slowly does too; fed Ardlings heal (four times
  faster resting in a habitat). Fresh on the pad, everyone's well.
- **Knocked out** at no health: a revive kit in the Ardling's own backpack is used by itself; otherwise a teammate with one has
  90 seconds (F beside them on foot, or Use aboard the same ship), or they're lost.
- **Crouch** (Z): half speed, a lower view.
- **Things on the worlds** (Universe.items, saved): searching an outpost or wreck scatters 2-5 things round you, picked by
  rarity (common 60, uncommon 28, rare 10, very rare 2). They show with a glowing ring. On foot F picks up the nearest within
  2.5 m (or puts down what you hold), R throws it (it flies and bounces in the world's frame), I puts it in the backpack.
- **29 items of our own**: ten foods (each fills a different amount; some give energy), bandage, med kit, revive kit; fuel
  canister (250 kg), thruster fuel can, battery pack, supply crate, air tank; repair kit (anyone can fix a broken part with
  it), scanner (what's lying about within 3 km, for a minute), torch, spare flag; five valuables to sell; backpack straps (6)
  and the big backpack (8).
- **The backpack** (I, and 1-8 to use): 4 places, kept between flights. Fuel, thruster fuel, batteries and supplies go into the
  ship in reach (or the one you're in).
- **The supply store** at the space centre (a new building): buy (into the backpack, or the locker when it's full), sell (valuables
  for their worth, the rest at half), move things between backpack and locker (12, 24 once extended). Free in a sandbox.
- **Rooms**: the host keeps everyone's numbers, backpacks and what they hold, and the things lying about, and sends them round;
  players' F/R/I/1-8 go to the host as commands.
- 8 new sim tests (vitals, hunger over time, stamina, falls, backpack, items, loot, saves); `-autotestSurvival` (760-768).
- Next: stage two (random outposts and wrecks along the way, random events, engine heat with coolant and radiators, repairs),
  then stage three (rogue drones, fighting back, traders, the mystery vending machine, daily and weekly quests).

### Milestone 39: out there (2026-11-15)
Stage two of the survival systems (captaincarl24: "add the rest of all the systems in the doc"), in Arden's own style:
- **Random places** (Outposts.RandomSites, picked by each game's seed): 10 round the space centre (4 to 60 km out, on dry land:
  rover trips) and 8 anywhere on every other world with ground (none on the sun or the gas giant). Supply caches, abandoned
  camps, crashed probes, research huts, fuel depots, relay masts, survey rovers and cargo pods, each with a code name. They're
  drawn like the outposts and searched the same way (a little fuel, food and science, and loot to pick up).
- **Hazards** (Sim/Hazards.cs, Game/FlightHazards.cs): one every 3-8 minutes (half as often again on Easy), never while
  warping, in a lesson or a replay, announced on screen with a banner. Where you are decides what can happen:
  - thick air (Arden's ground): **thunderstorms** (lightning strikes nearby: a bolt, a flash, thunder; it hurts walkers within
    10 m and damages ships it hits);
  - any air: **dust devils** (a spinning column crossing close by, flinging loose things and Ardlings), **strong wind** (things
    blow about, walking into it is tiring), **heat waves** (walkers get hungry fast, engines run 50% hotter, an orange tint);
  - airless ground: **meteor showers** (15 meteors over 45 s: streaks, impacts, damage; a third leave a meteorite chunk to sell),
    the **strange shimmer** (a glowing light that lifts loose things and Ardlings off the ground, then drops them);
  - airless ground and space: **solar flares** (10 s warning, then radiation hurts anyone outside);
  - space: **micrometeoroids** (pings that damage parts or hurt a spacewalker), **comms blackouts** (the signal drops for 30 s).
- **Engine heat** (VesselSim.CoolEngines): running engines make waste heat (8 kW per kN; six times that when wrecked; 1.5x in a
  heat wave), less what their own cooling carries off (fuel run round the nozzle: up to 1.6x the normal heat from 45 to 60% of
  the limit, broken along with the engine when it's damaged). So a healthy engine settles near 55% of its limit, even in a heat
  wave, and never overheats; a damaged one climbs past 85% and shuts itself down (no thrust) until it's below 60%, instead of
  exploding. The existing radiators now cool engines too (five times their rating, unfolded; a fifth folded), and the new
  **coolant tank** (120 L, Ore Mining tech) cools an engine past 70%; a **coolant canister** item tops it up.
- **Part damage** (PartState.damage, saved): lightning, meteorites and micrometeoroids damage parts. A damaged engine pushes less
  (40% when wrecked) and runs hotter, damaged tanks leak, damaged parts look scorched, wrecked legs break. A repair kit (anyone)
  or an engineer on foot fixes them.
- 4 new sim tests (places, hazards, engine heat, damage; the older tests run with engine heat off so their numbers stay put);
  `-autotestHazards` (770-778).

### Milestone 40: drones, traders and quests (2026-11-15)
Stage three, the last, of captaincarl24's "add the rest of all the systems in the doc", in Arden's own style:
- **Rogue drones** (Sim/Drones.cs, Game/FlightDrones.cs): maintenance drones left at the outposts and wrecks have gone haywire.
  Come within 800 m of a place nobody's cleared and its guards wake (1-3 at a random place; a warden and two scouts at the big
  outposts). Each hovers at home, notices an Ardling inside its sight (crouching halves it), waits a moment (1.5 s to react), chases, zaps on a
  cooldown, gives up if led more than 70 m from home (or out of sight for 8 s), and floats home healing. Seven kinds: scout
  (30 hp, zaps 5), hauler (120 hp, slow, hits 16), dart (18 hp, very fast), sparker (saps 25 stamina), mimic crate (looks like
  loot until you're 5 m away), leaker (hurts anyone within 4 m) and the warden (400 hp, zaps 10 from 10 m, calls a scout).
  Health bars show over drones that are after someone.
- **Fighting back**: with a zapper in your backpack, left click zaps the drone you're facing (18 m, 20 damage); without one, a
  kick up close (6); thrown things hit for 15. A drone knocked out drops a drone core (a valuable) and sometimes loot; once a
  place's guards are all down it stays clear (saved).
- **Traders** (Game/FlightTrade.cs): an Ardling trader at each big outpost; F within reach opens trading. Six things in stock,
  new every day, at 1.25x the store's price; they buy everything at 0.6x, valuables at 1.15x (more than the store at home).
- **The mystery machine** (a store tab): 250 a pull; common 40, uncommon 35, rare 20, very rare 5.
- **Daily and weekly quests** (Sim/Quests.cs; the quest board, a store tab and a new building): three a day and two bigger ones a
  week, the same for everyone on that day: walk, search places, eat, pick things up, knock out drones, plant flags, land on new
  worlds, sell, drive, ride out hazards. Each pays funds and something for the backpack; claim on the board.
- **Rooms**: the host runs the drones and sends them round; players' attacks, buying and selling go to the host.
- 4 new sim tests (drones, guards, quests, trading and the machine); `-autotestDrones` (780-785).

### Milestone 41: fixes, water and landings (2026-11-15)
captaincarl24's bug reports and asks, all in one update:
- **Window clicks** (Ui.ShieldAround): My Ardling, the supply store, the expedition report, the tutorials list and the definitions
  each drew a whole-screen invisible button before their own, and in IMGUI the first control under the pointer takes the click,
  so only ESC worked. Now only the space round each window is shielded. The expedition report draws over everything from GameRoot.
- **My Ardling**: the preview has its own key, rim and fill lights (no sun, a soft fill) and turns the world's SSAO off while
  it shows (it grained the model). Rocket rooms seat the players as the crew (as expeditions did), so their looks show.
- **Roles**: only the builder builds (and only the builder's design and launch are taken by the host), in expeditions too;
  watchers see the expedition plan and the report. With nobody holding the job, anyone may until it's handed on.
- **Inside (TAB)**: the cabin's part is found again by its number after each of the host's half-second ship copies (everyone
  but the host used to be thrown out); TAB says why it can't go in.
- **Crashes and the death screen** (Game/FlightDeath.cs): lost while inside or in first person: out of the cabin, the pointer
  freed, the camera pulled back; a death screen fades in (YOU DIED / CREW LOST / VESSEL DESTROYED, the cause, who was aboard,
  flight time, highest, fastest, a tip that fits) over the usual ways on, now with Main menu.
- **Rooms**: the host's roll-out to the pad goes out in each frame (FlightFrame.roll) and players roll their own crawler alongside.
- **F10 twice**: the main menu from anywhere (leaves a room).
- **Water** (Sim/VesselSimWater.cs): floating ships ride on buoyancy from the parts under water (each part's shape volume times
  an air factor: cabins 2.4, tanks 2.0, engines 0.5), worked out at points round each part so a tilt dips one side; buoyancy
  off the centre of mass turns them (capsules upright, tall rockets onto their side); water drag (hard on bobbing, light on
  gliding); engines push across the sea and lift off; wheels paddle; yaw steers; heavier than the water it can push aside: it
  sinks (a warning, then lost below 25 m); a drift onto land washes up. Splashdowns plunge in and throw spray (Smoke.Splash);
  a shallow skim counts mostly the downward speed. The HUD says Floating or Sinking.
- **Landings settle** (Sim/VesselSimSettle.cs): a touchdown no longer parks at the angle it touched: it settles on the rovers'
  contact physics (every foot and part on the ground pushes back and grips; SAS lets go) and parks once still for 0.35 s.
- **Space centre**: no labels; the building under the pointer glows (a box round it) with its name and what it's for.
- The update log keeps the first version and the latest updates only.
- 7 new sim tests; `-autotestArdling` (790-799).

## Next
- Ideas welcome. (Controller support: not wanted.)

## Deliberate differences
- **No wobbly joints.** Rockets are rigid bodies, so tall rockets don't flex and oscillate. Milestone 4 adds joint strength instead, so overloaded parts break off cleanly.
- Original names, planets, parts and art throughout.
