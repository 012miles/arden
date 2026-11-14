using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Crew names (captaincarl24's call, 2026-11-11): short, friendly first names, and everyone shares the family name of their
    /// species, Ardling: "Bob Ardling", "Billy Ardling", "Ted Ardling". Handed out as crewed parts are filled at launch.
    /// </summary>
    public static class CrewNames
    {
        public const string Surname = "Ardling";
        static readonly string[] First =
        {
            "Bob", "Billy", "Ted", "Gus", "Milo", "Dot", "Pip", "Sal", "Ned", "Lou", "Hank", "Vic", "Ollie", "Jo", "Kit", "Sam",
            "Max", "Ray", "Tom", "Dan", "Pat", "Zed", "Fern", "Ivy", "Mae", "Nell", "Gwen", "Ben", "Al", "Bert", "Cal", "Dale",
            "Earl", "Fred", "Gil", "Hal", "Ike", "Jim", "Ken", "Len", "Mo", "Nate", "Otis", "Phil", "Roy", "Stan", "Tad", "Walt",
            "Abe", "Bea", "Cora", "Dee", "Elsa", "Flo", "Greta", "Hattie", "Iris", "June", "Kay", "Lola", "Meg", "Nora", "Opal",
            "Peg", "Rita", "Sue", "Tess", "Una", "Viv", "Wanda", "Zoe", "Barney", "Chip", "Dusty", "Rusty", "Skip", "Sunny",
            "Biff", "Lenny", "Pete", "Rex", "Toby", "Wally", "Ziggy", "Bonnie", "Dolly", "Penny", "Molly", "Gertie", "Mabel",
        };
        static readonly string[] Numbers = { "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        static readonly System.Random rng = new System.Random();
        public static string Next() => $"{First[rng.Next(First.Length)]} {Surname}";
        /// <summary>When every plain name is taken: "Bob Ardling III".</summary>
        public static string Numbered(int k) => $"{Next()} {Numbers[Math.Min(Math.Max(k, 0), Numbers.Length - 1)]}";
        public static int Count => First.Length;
        public static IReadOnlyList<string> FirstNames => First;
    }

    /// <summary>Docking, undocking, crew transfer and structural failure.</summary>
    public partial class VesselSim
    {
        public const float DockRange = 0.6f, DockMaxSpeed = 1.5f, DockMaxAngle = 15f;

        /// <summary>For SAS "dock align": the direction to point the nose (opposite the target port's face).</summary>
        public Vector3d? dockAlignDir;

        /// <summary>Pieces that broke off during the last step (the flight adds them to the universe).</summary>
        public readonly List<VesselSim> pendingSpawns = new List<VesselSim>();

        public PartState FindPart(int designIndex) => Find(designIndex);

        // ---------------------------------------------------------------- docking ports

        /// <summary>A port's free docking face (vessel frame): its position and outward normal. False if it's not a free port.</summary>
        public bool PortFace(PartState p, out Vector3 pos, out Vector3 nrm)
        {
            pos = nrm = Vector3.zero;
            if (p.def.kind != PartKind.DockingPort || p.dockedTo >= 0) return false;
            if (!p.topCovered) { pos = p.localPos; nrm = p.Axis; return true; }
            if (!p.bottomCovered) { pos = p.Bottom; nrm = -p.Axis; return true; }
            return false;
        }

        /// <summary>World (body-relative) position and normal of a free port face.</summary>
        public bool PortFaceWorld(PartState p, out Vector3d pos, out Vector3d nrm)
        {
            pos = nrm = Vector3d.zero;
            if (!PortFace(p, out var l, out var n)) return false;
            pos = this.pos + Vector3d.Rotate(rot, Vector3d.FromFloat(l - com));
            nrm = Vector3d.FromFloat(rot * n);
            return true;
        }

        public IEnumerable<PartState> FreePorts()
        {
            foreach (var p in parts) if (PortFace(p, out _, out _)) yield return p;
        }

        public IEnumerable<PartState> DockedPorts()
        {
            // Ports, and a grabber with the wanderer it holds (either end of that link can be released)
            foreach (var p in parts)
                if ((p.def.kind == PartKind.DockingPort || p.def.kind == PartKind.Grabber || p.def.kind == PartKind.Rock) && p.dockedTo >= 0) yield return p;
        }

        /// <summary>The closest pair of matching free ports between this vessel and another (for the docking display).</summary>
        public bool BestPortPair(VesselSim other, out PartState mine, out PartState theirs)
        {
            mine = theirs = null;
            double best = double.PositiveInfinity;
            foreach (var a in FreePorts())
            {
                PortFaceWorld(a, out var wa, out _);
                foreach (var b in other.FreePorts())
                {
                    if (a.def.dockSize != b.def.dockSize) continue;
                    other.PortFaceWorld(b, out var wb, out _);
                    double d = (wa - wb).magnitude;
                    if (d < best) { best = d; mine = a; theirs = b; }
                }
            }
            return mine != null;
        }

        Vector3d PointVelocity(Vector3 local) => vel + Vector3d.FromFloat(Vector3.Cross(angVel, rot * (local - com)));

        /// <summary>
        /// Latches a free port of this vessel onto a matching free port of 'other' when they meet face to face,
        /// lined up within 15 degrees, slower than 1.5 m/s. On success 'other' becomes part of this vessel.
        /// 'note' explains a near miss (too fast, not lined up) so the player can correct.
        /// </summary>
        public bool TryDock(VesselSim other, out string note)
        {
            note = null;
            if (other == this || other.body != body || destroyed || other.destroyed || landed || other.landed) return false;
            // Ports that just let go of each other re-arm once they're 2 m apart.
            Rearm(other);
            other.Rearm(this);
            if ((other.pos - pos).magnitude > 300) return false;
            foreach (var pa in parts)
            {
                if (!PortFace(pa, out var la, out var na) || pa.disarmedWith == other) continue;
                Vector3d wa = pos + Vector3d.Rotate(rot, Vector3d.FromFloat(la - com));
                foreach (var pb in other.parts)
                {
                    if (pb.def.dockSize != pa.def.dockSize || pb.disarmedWith == this || !other.PortFace(pb, out var lb, out var nb)) continue;
                    Vector3d wb = other.pos + Vector3d.Rotate(other.rot, Vector3d.FromFloat(lb - other.com));
                    if ((wa - wb).magnitude > DockRange) continue;
                    float ang = Vector3.Angle(rot * na, -(other.rot * nb));
                    double rel = (PointVelocity(la) - other.PointVelocity(lb)).magnitude;
                    if (ang > DockMaxAngle) { note = $"Ports touched but they're {ang:0} deg out of line (needs under {DockMaxAngle:0})"; continue; }
                    if (rel > DockMaxSpeed) { note = $"Ports touched at {rel:0.0} m/s: too fast to latch (under {DockMaxSpeed:0.0} m/s)"; continue; }
                    Merge(other, pa, pb, la, na, lb, nb);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Re-arms this vessel's ports that were disarmed against 'other' once every port face of 'other' is 2 m away.</summary>
        void Rearm(VesselSim other)
        {
            foreach (var p in parts)
            {
                if (p.disarmedWith != other || !PortFaceWorld(p, out var w, out _)) continue;
                double nearest = double.PositiveInfinity;
                foreach (var q in other.FreePorts()) { other.PortFaceWorld(q, out var wq, out _); nearest = Math.Min(nearest, (w - wq).magnitude); }
                if (nearest > 2.0) p.disarmedWith = null;
            }
        }

        /// <summary>Forgets disarm marks against vessels that aren't around any more.</summary>
        public void RearmAllExcept(ICollection<VesselSim> nearby)
        {
            foreach (var p in parts) if (p.disarmedWith != null && !nearby.Contains(p.disarmedWith)) p.disarmedWith = null;
        }

        static Attach Flip(Attach a) => a == Attach.Above ? Attach.Below : a == Attach.Below ? Attach.Above : a;

        /// <summary>Makes 'p' the root of this vessel's part tree by reversing the links from it up to the old root.</summary>
        void RerootAt(PartState p)
        {
            int cur = p.index, par = p.parent;
            Attach curAttach = p.attach;
            p.parent = -1;
            p.attach = Attach.Root;
            int guard = 0;
            while (par >= 0 && guard++ < 1000)
            {
                var P = Find(par);
                if (P == null) break;
                int nextPar = P.parent;
                Attach nextAttach = P.attach;
                P.parent = cur;
                P.attach = Flip(curAttach);
                cur = par;
                curAttach = nextAttach;
                par = nextPar;
            }
        }

        /// <summary>Joins 'o' onto this vessel at the two ports, snapping them face to face. Mass and momentum are conserved.</summary>
        void Merge(VesselSim o, PartState pa, PartState pb, Vector3 la, Vector3 na, Vector3 lb, Vector3 nb)
        {
            double mA = mass, mB = o.mass, m = mA + mB;
            Vector3d newVel = (vel * mA + o.vel * mB) / m;
            Vector3 newAng = (angVel * (float)mA + o.angVel * (float)mB) / (float)m;
            Quaternion inv = Quaternion.Inverse(rot);
            Quaternion rel = inv * o.rot;
            Vector3 offset = inv * (o.pos - pos).ToFloat() + com;          // o's CoM in our frame
            Quaternion snap = Quaternion.FromToRotation(rel * nb, -na);     // turn o so the faces meet squarely
            Vector3 faceB = rel * (lb - o.com) + offset;

            int baseIndex = 0;
            foreach (var p in parts) baseIndex = Math.Max(baseIndex, p.index + 1);
            o.RerootAt(pb);
            foreach (var q in o.parts)
            {
                Vector3 lp = rel * (q.localPos - o.com) + offset;
                q.localPos = snap * (lp - faceB) + la;
                q.localRot = snap * rel * q.localRot;
                q.index += baseIndex;
                if (q.parent >= 0) q.parent += baseIndex;
            }
            pb.parent = pa.index;
            pb.attach = !pa.topCovered && pa.localPos == la ? Attach.Above : Attach.Below;
            pa.dockedTo = pb.index;
            pb.dockedTo = pa.index;
            pb.dockedName = o.name;
            pb.dockedDebris = o.isDebris;

            // The other vessel's remaining stages come after ours.
            if (stages == null) stages = new List<StageAction>();
            if (o.stages != null)
                for (int s = Math.Max(0, o.nextStage); s < o.stages.Count; s++)
                {
                    var st = o.stages[s];
                    var c = new StageAction();
                    foreach (int i in st.decouple) c.decouple.Add(i + baseIndex);
                    foreach (int i in st.ignite) c.ignite.Add(i + baseIndex);
                    foreach (int i in st.chutes) c.chutes.Add(i + baseIndex);
                    foreach (int i in st.jettison) c.jettison.Add(i + baseIndex);
                    stages.Add(c);
                }
            parts.AddRange(o.parts);
            o.parts.Clear();

            Vector3 oldCom = com;
            RebuildTopology();
            RecomputeMass();
            pos = pos + Vector3d.Rotate(rot, Vector3d.FromFloat(com - oldCom));
            vel = newVel;
            angVel = newAng;
            rails = null;
            sasHoldValid = false;
            prevPos = pos;
            prevRot = rot;
            isDebris = false;
            lastEvent = $"Docked with {pb.dockedName}!";
        }

        /// <summary>Releases a docked port: everything on the far side becomes its own vessel again, pushed apart gently.</summary>
        public VesselSim Undock(PartState port)
        {
            if (port == null || port.dockedTo < 0) return null;
            var other = Find(port.dockedTo);
            if (other == null) { port.dockedTo = -1; return null; }
            PartState child = port.parent == other.index ? port : other;
            PartState par = child == port ? other : port;
            string name0 = child.dockedName;
            bool debris0 = child.dockedDebris;
            var oldStages = stages;
            int oldNext = nextStage;
            var piece = Decouple(child.index, 0);
            child.dockedTo = par.dockedTo = -1;
            child.dockedName = null;
            if (piece == null) return null;
            child.disarmedWith = this;
            par.disarmedWith = piece;
            piece.name = string.IsNullOrEmpty(name0) ? name + " (undocked)" : name0;
            piece.isDebris = debris0 && !piece.IsControllable;
            piece.sas = SasMode.Stability;
            piece.throttle = 0;
            // The piece keeps the stage actions that still apply to it.
            piece.stages = new List<StageAction>();
            if (oldStages != null)
                for (int s = Math.Max(0, oldNext); s < oldStages.Count; s++)
                {
                    var st = oldStages[s];
                    var c = new StageAction();
                    foreach (int i in st.decouple) if (piece.Find(i) != null) c.decouple.Add(i);
                    foreach (int i in st.ignite) if (piece.Find(i) != null) c.ignite.Add(i);
                    foreach (int i in st.chutes) if (piece.Find(i) != null) c.chutes.Add(i);
                    foreach (int i in st.jettison) if (piece.Find(i) != null) c.jettison.Add(i);
                    if (!c.Empty) piece.stages.Add(c);
                }
            piece.nextStage = 0;
            piece.RecomputeMass();
            // And this side forgets the stage actions that left with the piece.
            if (stages != null)
                for (int s = stages.Count - 1; s >= Math.Max(0, nextStage); s--)
                {
                    var st = stages[s];
                    st.decouple.RemoveAll(i => Find(i) == null);
                    st.ignite.RemoveAll(i => Find(i) == null);
                    st.chutes.RemoveAll(i => Find(i) == null);
                    st.jettison.RemoveAll(i => Find(i) == null);
                    if (st.Empty) stages.RemoveAt(s);
                }
            // Spring the two apart along the port axis, 0.3 m/s total, momentum conserved.
            PortFaceWorld(par, out var face, out _);
            Vector3d dir = (piece.pos - face).normalized;
            if (dir.magnitude < 0.5) dir = Vector3d.FromFloat(rot * par.Axis);
            double total = mass + piece.mass;
            piece.vel = piece.vel + dir * (0.3 * mass / total);
            vel = vel - dir * (0.3 * piece.mass / total);
            lastEvent = $"Undocked from {piece.name}";
            return piece;
        }

        // ---------------------------------------------------------------- crew

        public IEnumerable<PartState> CrewParts()
        {
            foreach (var p in parts) if (p.def.crew > 0) yield return p;
        }

        public int CrewCount { get { int n = 0; foreach (var p in parts) n += p.crew.Count; return n; } }

        /// <summary>Moves one person from one part to another in this vessel (docked vessels count as one). Returns a message.</summary>
        public string TransferCrew(PartState from, PartState to)
        {
            if (from == null || to == null || from == to) return "Pick two different crew parts.";
            if (from.crew.Count == 0) return $"Nobody aboard the {from.def.name}.";
            if (to.crew.Count >= to.def.crew) return $"The {to.def.name} is full ({to.def.crew} seats).";
            string who = from.crew[from.crew.Count - 1];
            from.crew.RemoveAt(from.crew.Count - 1);
            to.crew.Add(who);
            RecomputeMass();
            return $"{who} moved to the {to.def.name}.";
        }

        // ---------------------------------------------------------------- structure

        /// <summary>
        /// Loads on every joint from thrust, air and the vessel's acceleration and spin. A joint carrying more than its
        /// rating tears, and a deployed panel or antenna tears off in thick air. Broken pieces go to pendingSpawns.
        /// </summary>
        void CheckStructure(double dt, Vector3 wOld)
        {
            if (topo == null || parts.Count < 2 || isDebris && parts.Count < 2) return;
            double qk = dynPressure / 1000.0;
            Vector3 fTot = Vector3.zero;
            foreach (var p in parts) fTot += p.thrustLocal + p.aeroArea * (float)qk;
            Vector3 a0 = fTot / (float)mass;
            Vector3 w = Quaternion.Inverse(rot) * angVel;
            Vector3 alpha = (w - wOld) / (float)dt;

            int n = topo.n;
            if (loadScratch.Length < n) loadScratch = new Vector3[n];
            for (int i = 0; i < n; i++) loadScratch[i] = Vector3.zero;
            foreach (var p in parts)
            {
                Vector3 r = p.Center - com;
                Vector3 acc = a0 + Vector3.Cross(alpha, r) + Vector3.Cross(w, Vector3.Cross(w, r));
                loadScratch[p.index] = acc * (float)p.Mass - (p.thrustLocal + p.aeroArea * (float)qk);
            }
            // Sum each subtree into its root (children are deeper in the tree, so accumulate from the leaves up).
            order.Clear();
            var st = new Stack<int>();
            foreach (var p in parts) if (p.parent < 0 || Find(p.parent) == null) st.Push(p.index);
            while (st.Count > 0) { int i = st.Pop(); order.Add(i); foreach (int c in topo.children[i]) st.Push(c); }
            for (int k = order.Count - 1; k >= 0; k--)
            {
                int i = order[k];
                var p = Find(i);
                if (p != null && p.parent >= 0 && p.parent < n) loadScratch[p.parent] += loadScratch[i];
            }

            PartState worst = null;
            float worstRatio = 1f;
            string why = null;
            foreach (var p in parts)
            {
                if (p.parent < 0 || Find(p.parent) == null) continue;
                if (p.def.IsDeployable && p.legDeploy > 0.5f && p.def.maxQ > 0 && qk > p.def.maxQ)
                {
                    float rq = (float)(qk / p.def.maxQ) + 1f;
                    if (rq > worstRatio) { worstRatio = rq; worst = p; why = $"{p.def.name} tore off in the airflow ({qk:0.0} kPa, it takes {p.def.maxQ:0.#} kPa deployed)"; }
                    continue;
                }
                float load = loadScratch[p.index].magnitude, rating = p.def.JointStrength;
                if (load > rating && load / rating > worstRatio)
                {
                    worstRatio = load / rating;
                    worst = p;
                    why = $"{p.def.name} tore off: {load:0} kN on a joint rated {rating:0} kN";
                }
            }
            if (worst != null) BreakOff(worst, why);
        }

        Vector3[] loadScratch = new Vector3[0];
        readonly List<int> order = new List<int>();

        /// <summary>Snaps a part (and everything hanging off it) away as a separate piece.</summary>
        public void BreakOff(PartState p, string why)
        {
            var piece = Decouple(p.index, 0.5);
            if (piece == null) return;
            piece.name = $"{p.def.name} (broken off)";
            if (p.def.kind == PartKind.DockingPort || p.dockedTo >= 0) p.dockedTo = -1;
            pendingSpawns.Add(piece);
            lastEvent = why;
        }
    }
}
