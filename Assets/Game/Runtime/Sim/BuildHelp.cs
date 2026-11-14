using System.Collections.Generic;
using System.Linq;

namespace Rocket
{
    /// <summary>
    /// Milestone 32 (captaincarl24: "when running easy mode make building simpler with like tips saying what this is or
    /// hints"): what a rocket still needs, as a checklist, and what each kind of part is for, in plain words.
    /// </summary>
    public static class BuildHelp
    {
        public class Item
        {
            public string text, search;
            public bool done;
        }

        /// <summary>
        /// The basics of a rocket that flies and comes home: something to steer it, fuel, an engine, a way to land (with
        /// crew aboard), fins to keep it straight, and enough push to leave the pad. "search" finds the parts in the list.
        /// </summary>
        public static List<Item> Checklist(VesselDesign d, List<StageReport> reports)
        {
            var items = new List<Item>();
            var defs = d.Defs().Where(x => x != null).ToList();
            bool cmd = defs.Any(x => x.kind == PartKind.Command);
            bool tank = defs.Any(x => x.kind == PartKind.Tank);
            bool solid = defs.Any(x => x.IsSolid);
            bool engine = defs.Any(x => x.IsEngine && !x.IsJet);
            bool crewed = defs.Any(x => x.crew > 0);
            bool chute = defs.Any(x => x.kind == PartKind.Parachute);
            bool fins = defs.Any(x => x.kind == PartKind.Fins);
            var first = reports?.FirstOrDefault(r => r.hasEngines);
            bool lifts = first != null && first.twrAsl >= 1.15;
            items.Add(new Item { text = "A capsule or probe on top (it steers)", done = cmd, search = "command" });
            items.Add(new Item { text = solid && !tank ? "Fuel (this booster has its own)" : "A fuel tank under it", done = tank || solid, search = "tank" });
            items.Add(new Item { text = "An engine at the bottom", done = engine, search = "engine" });
            if (crewed || !cmd) items.Add(new Item { text = "A parachute on top (to land)", done = chute, search = "parachute" });
            items.Add(new Item { text = "Fins at the bottom (fly straight)", done = fins, search = "fin" });
            if (engine)
                items.Add(new Item { text = lifts ? "Strong enough to take off" : "Too heavy: bigger engine, less fuel", done = lifts, search = lifts ? null : "engine" });
            return items;
        }

        /// <summary>Does a part match what's typed in the parts search? (name, kind, category or description)</summary>
        public static bool Matches(PartDef d, string search)
        {
            if (d == null) return false;
            if (string.IsNullOrWhiteSpace(search)) return true;
            string s = search.Trim().ToLowerInvariant();
            foreach (var word in s.Split(' '))
            {
                if (word.Length == 0) continue;
                bool hit = (d.name ?? "").ToLowerInvariant().Contains(word) || d.kind.ToString().ToLowerInvariant().Contains(word)
                    || (d.Category ?? "").ToLowerInvariant().Contains(word) || (d.description ?? "").ToLowerInvariant().Contains(word)
                    || (d.id ?? "").ToLowerInvariant().Contains(word) || Tip(d).ToLowerInvariant().Contains(word);
                if (!hit) return false;
            }
            return true;
        }

        /// <summary>What a part is for, in a sentence a beginner can follow.</summary>
        public static string Tip(PartDef d)
        {
            if (d == null) return "";
            switch (d.kind)
            {
                case PartKind.Command: return d.crew > 0 ? "The brain of the rocket, with room for crew. It steers the rocket. Put it near the top." : "A computer brain: it steers the rocket with nobody aboard. It needs electricity.";
                case PartKind.Parachute: return "Opens to slow you down so you land gently. Put it on the very top.";
                case PartKind.HeatShield: return "Goes under the capsule. Stops it burning up when it comes home fast.";
                case PartKind.EscapeTower: return "Pulls the capsule away if something goes wrong (press Backspace to abort).";
                case PartKind.NoseCone: return "A pointy top that lets the air slide past, so the rocket goes faster.";
                case PartKind.Tank: return "Holds fuel for the engine below it. More tanks burn longer, but weigh more.";
                case PartKind.Engine: return d.IsJet ? "A jet engine for planes: it breathes air, so it only works low down." : "Pushes the rocket up. Put it at the bottom of a fuel tank.";
                case PartKind.Solid: return "A booster with its own fuel inside: a big push that can't be stopped once it's lit.";
                case PartKind.Decoupler: return "Splits the rocket in two, so empty parts can fall away and the rest gets lighter.";
                case PartKind.RadialDecoupler: return "Holds a booster on the side of the rocket, and lets go when it's empty.";
                case PartKind.Adapter: return "Joins a wide part to a narrow one.";
                case PartKind.Fins: return "Keep the rocket pointing straight, like the feathers on an arrow. Put them at the bottom.";
                case PartKind.Rcs: return "Little puffs for turning and moving gently in space, for docking. They need monopropellant.";
                case PartKind.MonoTank: return "Fuel for the little RCS puffers.";
                case PartKind.Leg: return "Legs for landing on the moons and other worlds.";
                case PartKind.Light: return "A light, for seeing in the dark.";
                case PartKind.Fairing: return "A cover that keeps what's on top of it safe on the way up, then falls away.";
                case PartKind.Solar: return "Turns sunlight into electricity.";
                case PartKind.Antenna: return "Sends messages and science home.";
                case PartKind.Wing: return "Lifts a plane into the air.";
                case PartKind.ControlSurface: return "Moving flaps that steer a plane.";
                case PartKind.Gear: return "Wheels for taking off and landing on the runway.";
                case PartKind.Frame: return "The body of a rover: wheels go on its sides.";
                case PartKind.Wheel: return "Drives a rover along the ground.";
                case PartKind.DockingPort: return "Joins two ships together in space.";
                case PartKind.Grabber: return "A claw for grabbing a space rock.";
                case PartKind.Habitat: return "Somewhere for the crew to live, or supplies for a long trip.";
                case PartKind.Battery: return "Stores electricity.";
                case PartKind.Science: return "An experiment. Run it on your flight to learn things and earn science.";
                case PartKind.Scanner: return "From orbit, it maps where the useful rocks (ore) are.";
                case PartKind.Drill: return "Digs ore out of the ground.";
                case PartKind.OreTank: return "Holds the ore you dig up.";
                case PartKind.Converter: return "Turns ore into fuel.";
                case PartKind.Radiator: return "Gets rid of heat from busy machines.";
                default: return d.description ?? "";
            }
        }
    }
}
