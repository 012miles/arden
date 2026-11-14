using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 22 in flight: astronauts carry ground cables between landed modules, linked modules share what they
    /// hold every frame, cables are drawn on the ground, and a Base panel shows the whole base.
    /// </summary>
    public partial class FlightMode
    {
        int cableFrom;                       // the module the astronaut on EVA is carrying a cable from (0 = none)
        readonly Dictionary<long, LineRenderer> cableLines = new Dictionary<long, LineRenderer>();
        LineRenderer carryLine;
        static Material cableMat;

        /// <summary>The landed module (not an astronaut) nearest the astronaut on EVA within working reach, or null.</summary>
        VesselSim ModuleInReach()
        {
            if (active == null || !active.IsEva) return null;
            VesselSim best = null;
            double bd = double.MaxValue;
            foreach (var u in U.vessels)
            {
                if (u == active || u.destroyed || !u.landed || u.IsEva || u.body != active.body) continue;
                if (u.parts.All(p => p.def.kind == PartKind.Flag)) continue;
                double d = u.parts.Min(p => active.ReachTo(u, p));
                if (d < VesselSim.WorkRange && d < bd) { bd = d; best = u; }
            }
            return best;
        }

        /// <summary>Per frame: loose cables, sharing between linked modules, and the astronaut's cable stretching too far.</summary>
        void UpdateBases()
        {
            foreach (var n in Bases.Validate(U)) Show(n, 4f);
            Bases.Share(U);
            if (cableFrom != 0)
            {
                var src = U.Find(cableFrom);
                if (src == null || src.destroyed || active == null || !active.IsEva || active.body != src.body) cableFrom = 0;
                else if (Bases.Distance(active, src) > Bases.MaxCable + Bases.Slack) { cableFrom = 0; Show($"The cable from {src.name} won't reach any further: you let it go.", 4f); }
            }
        }

        void TakeCable(VesselSim from)
        {
            cableFrom = from.id;
            audio?.Clunk(0.25f);
            Show($"Cable from {from.name}: walk to another landed module within {Bases.MaxCable:0} m and plug it in.", 4f);
        }

        void PlugCable(VesselSim into)
        {
            var src = U.Find(cableFrom);
            string why = src == null ? "The cable's gone." : Bases.Connect(U, src, into);
            if (why != null) { Show(why, 3f); return; }
            cableFrom = 0;
            audio?.Clank(0.5f);
            var g = Bases.GroupOf(U, into);
            Show($"{src.name} and {into.name} are connected: a base of {g.Count} modules sharing power, supplies, ore and fuel.", 5f);
        }

        void UnplugModule(VesselSim v)
        {
            int n = Bases.Unplug(U, v);
            audio?.Clunk(0.25f);
            Show(n > 0 ? $"Unplugged {n} cable{(n == 1 ? "" : "s")} from {v.name}." : $"{v.name} has no cables.", 3f);
        }

        /// <summary>For the autotests: the same as the EVA panel's buttons.</summary>
        public void TakeCableFrom(VesselSim from) => TakeCable(from);
        public void PlugCableInto(VesselSim into) => PlugCable(into);
        public bool CarryingCable => cableFrom != 0;

        /// <summary>EVA panel rows for cables. Returns the height they took (call with draw = false to measure).</summary>
        float CableRows(Rect r, float cy, bool draw)
        {
            if (!active.landed) return 0;
            var near = ModuleInReach();
            float h = 0;
            void Row(string label, bool enabled, Action act)
            {
                if (draw)
                {
                    GUI.enabled = enabled;
                    if (Ui.Press(new Rect(r.x + 12, cy + h, 276, 26), label, Ui.ButtonSmall)) act();
                    GUI.enabled = true;
                }
                h += 30;
            }
            if (cableFrom == 0)
            {
                if (near != null) Row($"Take a cable from {near.name}", true, () => TakeCable(near));
                if (near != null && Bases.Linked(U, near)) Row($"Unplug {near.name}'s cables", true, () => UnplugModule(near));
            }
            else
            {
                var src = U.Find(cableFrom);
                if (src != null)
                {
                    if (draw) GUI.Label(new Rect(r.x + 12, cy + h, 276, 20), $"Cable from <b>{src.name}</b>: {Bases.Distance(active, src):0} of {Bases.MaxCable:0} m", Ui.Small);
                    h += 22;
                    if (near != null && near != src)
                    {
                        string why = Bases.CanConnect(U, src, near);
                        Row(why == null ? $"Plug the cable into {near.name}" : why, why == null, () => PlugCable(near));
                    }
                    Row("Drop the cable", true, () => { cableFrom = 0; Show("You put the cable down.", 2f); });
                }
            }
            return h;
        }

        /// <summary>The base the active vessel belongs to (two or more modules), or null.</summary>
        List<VesselSim> ActiveBase()
        {
            if (active == null || active.IsEva || !active.landed || !Bases.Linked(U, active)) return null;
            var g = Bases.GroupOf(U, active);
            return g.Count > 1 ? g : null;
        }

        const float BasePanelH = 196;

        /// <summary>The whole base at a glance: modules, crew, power, supplies, ore, fuel, research; bring crew over.</summary>
        void DrawBasePanel(Rect r, List<VesselSim> g)
        {
            Ui.Box(r);
            var s = Bases.Summarize(g);
            GUI.Label(new Rect(r.x + 12, r.y + 6, 200, 22), "Base", Ui.Header);
            GUI.Label(new Rect(r.x + 64, r.y + 9, r.width - 76, 20), $"<b>{Bases.NameOf(g)}</b>  <size=11>{s.modules} modules · crew {s.crew}/{s.seats}</size>", Ui.Label);
            float cy = r.y + 32;
            string days = double.IsInfinity(s.supplyDays) ? "no one eating" : Supplies.DaysText(s.supplyDays);
            GUI.Label(new Rect(r.x + 12, cy, 280, 20), $"Power {s.ec:0}/{s.ecCap:0} EC   Supplies {s.supplies:0} crew-days ({days}){(s.greenhouse ? ", greenhouse" : "")}", Ui.Small);
            cy += 18;
            GUI.Label(new Rect(r.x + 12, cy, 280, 20), $"Ore {s.ore:0.0}/{s.oreCap:0.0} t   Fuel {s.fuel:0.0}/{s.fuelCap:0.0} t" + (s.lab ? $"   Research <color=#9fe0ff>{s.labPerDay:0.0}/day</color>" : ""), Ui.Small);
            cy += 22;
            foreach (var m in g.Take(4))
            {
                int crew = m.parts.Sum(p => p.crew.Count), seats = m.parts.Sum(p => p.def.crew);
                string here = m == active ? "  <color=#ffd060>(here)</color>" : $"  <size=10>{Bases.Distance(active, m):0} m</size>";
                GUI.Label(new Rect(r.x + 12, cy + 3, 190, 20), $"{m.name}  <size=11>{(seats > 0 ? $"crew {crew}/{seats}" : "")}</size>{here}", Ui.Small);
                if (m != active && crew > 0 && active.parts.Exists(p => p.def.crew > p.crew.Count))
                    if (Ui.Press(new Rect(r.xMax - 96, cy, 84, 22), "Bring 1 here", Ui.ButtonSmall)) { Show(Bases.MoveCrew(U, m, active), 3f); return; }
                cy += 26;
            }
            if (g.Count > 4) GUI.Label(new Rect(r.x + 12, cy, 280, 20), $"and {g.Count - 4} more", Ui.Small);
        }

        // ---------------------------------------------------------------- cables on the ground

        static long Key(int a, int b) => ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);

        /// <summary>Draws every cable near the camera along the ground (and the one being carried), each frame.</summary>
        void UpdateCableViews()
        {
            if (cableMat == null)
            {
                cableMat = new Material(Mats.Unlit) { name = "Cable" };
                cableMat.SetColor("_Color", new Color(0.08f, 0.08f, 0.09f, 1f));
            }
            var used = new HashSet<long>();
            foreach (var l in U.links)
            {
                VesselSim a = U.Find(l.a), b = U.Find(l.b);
                if (a == null || b == null || a.body != active.body || Vector3d.Distance(a.pos, active.pos) > 3000) continue;
                long k = Key(l.a, l.b);
                used.Add(k);
                if (!cableLines.TryGetValue(k, out var lr) || lr == null) cableLines[k] = lr = NewCable();
                LayCable(lr, a.pos, b.pos, a.body);
            }
            foreach (var k in cableLines.Keys.ToList())
                if (!used.Contains(k)) { if (cableLines[k] != null) UnityEngine.Object.Destroy(cableLines[k].gameObject); cableLines.Remove(k); }
            var src = cableFrom != 0 ? U.Find(cableFrom) : null;
            if (src != null && active.IsEva)
            {
                if (carryLine == null) carryLine = NewCable();
                if (!carryLine.gameObject.activeSelf) carryLine.gameObject.SetActive(true);
                LayCable(carryLine, src.pos, active.pos, src.body, true);
            }
            else if (carryLine != null && carryLine.gameObject.activeSelf) carryLine.gameObject.SetActive(false);
        }

        static LineRenderer NewCable()
        {
            var go = new GameObject("Cable");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = cableMat;
            lr.widthMultiplier = 0.07f;
            lr.positionCount = 24;
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        /// <summary>A cable lying on the ground between two points (body-centred), lifted to hand height at a carried end.</summary>
        void LayCable(LineRenderer lr, Vector3d pa, Vector3d pb, CelestialBody b, bool carriedEnd = false)
        {
            Vector3d bodyAbs = b.AbsolutePosition(renderUt);
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / (n - 1);
                Vector3d d = (pa * (1 - t) + pb * t).normalized;
                double ground = b.radius + LaunchSite.SurfaceHeight(b, b.InertialToFixed(d, renderUt).normalized);
                double lift = 0.06 + (carriedEnd ? Math.Pow(t, 6) * 0.8 : 0);
                lr.SetPosition(i, (bodyAbs + d * (ground + lift) - originAbs).ToFloat());
            }
        }
    }
}
