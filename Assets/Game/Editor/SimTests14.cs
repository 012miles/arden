using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 14 tests: wanderers, the grabber, capture contracts and the Wrangler tug.</summary>
    public static partial class SimTests
    {
        static void RunMilestone14()
        {
            Run("Wanderers arrive inside Arden's space on open paths: closest pass above the air, near the equator, days away", WandererPaths);
            Run("A wanderer left alone swings past and leaves; the universe forgets it and sends a new one (two at most)", WandererLeaves);
            Run("Grabber: latches onto a wanderer at walking pace (mass and momentum kept), not at 3 m/s or side on; releases it again", GrabAndRelease);
            Run("Pushing a wanderer: thrust through the combined mass, and SAS still holds the heading", PushWanderer);
            Run("Capture: grabbed at its closest pass, a retrograde burn of a few tens of m/s puts it in orbit and pays the contract", CaptureWanderer);
            Run("Rock samples: only while the grabber holds a wanderer, one subject per wanderer", RockSamples);
            Run("Wanderers and held wanderers save and load", WandererSave);
            Run("Contracts: grab and capture offers once the grabber is researched; capture fails if the wanderer leaves", WandererContracts);
            Run("Wrangler: reaches orbit with the delta-v to catch a wanderer and push a class B one", WranglerPreset);
        }

        static CelestialBody Home14 => System4[0];

        static string WandererPaths()
        {
            var home = Home14;
            double minPe = double.MaxValue, maxPe = 0, minT = double.MaxValue, maxT = 0, maxInc = 0;
            var counts = new int[3];
            for (int seed = 1; seed <= 40; seed++)
            {
                var w = Wanderers.Spawn(home, 1000, new System.Random(seed), seed);
                Check(w.IsRock && w.body == home, $"seed {seed}: not a rock around Arden");
                int cls = Wanderers.ClassOf(w.name);
                Check(cls >= 0 && w.parts[0].def == PartCatalog.Rocks[cls], $"seed {seed}: name {w.name} doesn't match its part {w.parts[0].def.id}");
                counts[cls]++;
                double r = w.pos.magnitude;
                Check(r < home.SoiRadius * 0.86 && r > home.SoiRadius * 0.84, $"seed {seed}: starts at {r / 1e6:0.0} Mm (SOI {home.SoiRadius / 1e6:0.0} Mm)");
                Check(Vector3d.Dot(w.pos, w.vel) < 0, $"seed {seed}: heading out, not in");
                var o = Orbit.FromState(w.pos, w.vel, home.mu, 1000);
                Check(!o.IsElliptic, $"seed {seed}: on a closed orbit (e {o.e:0.000})");
                double pe = o.Periapsis - home.radius;
                Check(pe > home.SafeAltitude + 140_000 && pe < home.SafeAltitude + 6_200_000, $"seed {seed}: closest pass {pe / 1000:0} km");
                Vector3d h = Vector3d.Cross(w.pos, w.vel).normalized;
                double inc = Math.Acos(Math.Abs(h.y)) * 180 / Math.PI;
                Check(inc < 25.5, $"seed {seed}: tilted {inc:0} deg");
                double t = o.TimeToPeriapsis(1000);
                minPe = Math.Min(minPe, pe); maxPe = Math.Max(maxPe, pe); minT = Math.Min(minT, t); maxT = Math.Max(maxT, t); maxInc = Math.Max(maxInc, inc);
            }
            Check(minT > 0.4 * 86400 && maxT < 15 * 86400, $"closest pass comes {minT / 86400:0.0}-{maxT / 86400:0.0} days after arrival");
            Check(counts.All(n => n > 0), $"classes A/B/C: {string.Join("/", counts)}");
            return $"40 arrivals: classes {string.Join("/", counts)}, closest pass {minPe / 1000:0}-{maxPe / 1000:0} km up, {minT / 86400:0.0}-{maxT / 86400:0.0} days later, tilt up to {maxInc:0} deg";
        }

        static string WandererLeaves()
        {
            // A wanderer can really crash into Tal or Vesk on its way past: try a few starts until one sails clear
            foreach (int seed in new[] { 1234, 4321, 777, 2026, 99 })
            {
                var probe = new Universe();
                probe.career.Start(GameMode.Sandbox);
                probe.career.seed = seed;
                probe.TendWanderers(System4);
                var r = probe.vessels.First(v => v.IsRock);
                for (int k = 0; k < 40 * 24 && !r.body.isStar && !r.destroyed; k++) r.BackgroundAdvance(3600, k * 3600.0, true);
                if (!r.destroyed) return WandererLeavesWith(seed);
            }
            Check(false, "every wanderer tried crashed into a moon");
            return "";
        }

        static string WandererLeavesWith(int seed)
        {
            var u = new Universe();
            u.career.Start(GameMode.Sandbox);
            u.career.seed = seed;
            var news = u.TendWanderers(System4);
            var rocks = u.vessels.Where(v => v.IsRock).ToList();
            Check(rocks.Count == 1, $"{rocks.Count} wanderers after the first look");
            var w = rocks[0];
            string name = w.name;
            Check(u.career.liveWanderers.Contains(name), "the career doesn't know about the new wanderer");
            u.TendWanderers(System4);
            Check(u.vessels.Count(v => v.IsRock) == 1, "a second one came straight away");
            // Let it fly past: a day at a time on rails.
            double closest = double.MaxValue;
            int days = 0;
            for (; days < 40 && !w.body.isStar && !w.destroyed; days++)
            {
                for (int k = 0; k < 24; k++) { w.BackgroundAdvance(3600, u.ut, true); u.ut += 3600; closest = Math.Min(closest, w.pos.magnitude - w.body.radius); }
            }
            Check(!w.destroyed, "the wanderer was destroyed: " + w.destroyReason);
            Check(w.body.isStar, $"after {days} days it's still around {w.body.name}");
            news = u.TendWanderers(System4);
            Check(!u.vessels.Contains(w), "the universe kept a wanderer that left");
            Check(news.Any(n => n.Contains(name) && n.Contains("left")), "no news that it left: " + string.Join(" | ", news));
            Check(u.vessels.Count(v => v.IsRock) == 1 && u.vessels.First(v => v.IsRock).name != name, "no new wanderer after the first left");
            // Two at most
            u.nextWandererUt = 0; u.TendWanderers(System4);
            u.nextWandererUt = 0; u.TendWanderers(System4);
            Check(u.vessels.Count(v => v.IsRock) == 2, $"{u.vessels.Count(v => v.IsRock)} wanderers at once");
            var off = new Universe { wanderersOn = false };
            off.TendWanderers(System4);
            Check(off.vessels.Count == 0, "wanderers came while switched off");
            return $"{name} passed {closest / 1000:0} km up and left after {days} days; a new one followed; two at most";
        }

        /// <summary>A tug pointing its grabber at a wanderer in a 300 km orbit, the claws 'gap' m from the surface, closing at 'speed'.</summary>
        static (VesselSim tug, VesselSim rock) GrabSetup(int cls, double gap, double speed, float offAxisDeg = 0)
        {
            var home = Home14;
            double r = home.radius + 300_000, sp = Math.Sqrt(home.mu / r);
            var rpos = new Vector3d(r, 0, 0);
            var rvel = new Vector3d(0, 0, -sp);
            var rock = VesselSim.CreateFree(VesselDesign.Stack("Wanderer " + "ABC"[cls] + "-TT1", PartCatalog.Rocks[cls].id), home, rpos, rvel, Quaternion.identity);
            rock.name = "Wanderer " + "ABC"[cls] + "-TT1";
            rock.sas = SasMode.Off; rock.throttle = 0;
            float R = PartCatalog.Rocks[cls].length * 0.5f;
            var dir = new Vector3d(0, 1, 0);   // the tug sits "above" the rock (north) and flies down onto it
            var tug = VesselSim.CreateFree(VesselDesign.WranglerTug(), home, rpos, rvel, Quaternion.identity);
            var grab = tug.parts.Find(p => p.def.kind == PartKind.Grabber);
            // Point the tug's nose (its grabber) at the rock, or a little off.
            Vector3 aim = Quaternion.AngleAxis(offAxisDeg, Vector3.forward) * (-dir).ToFloat();
            tug.rot = Quaternion.FromToRotation(Vector3.up, aim);
            Vector3d face = Vector3d.Rotate(tug.rot, Vector3d.FromFloat(grab.localPos - tug.com));
            Vector3d rockCenter = rock.pos + Vector3d.Rotate(rock.rot, Vector3d.FromFloat(rock.parts[0].Center - rock.com));
            tug.pos = rockCenter + dir * (R + gap) - face;
            tug.vel = rvel - dir * speed;
            tug.throttle = 0;
            tug.sas = SasMode.Stability;
            return (tug, rock);
        }

        static string GrabAndRelease()
        {
            var (tug, rock) = GrabSetup(0, 1.5, 0.6);
            double mT = tug.mass, mR = rock.mass;
            Vector3d p0 = tug.vel * mT + rock.vel * mR;
            double ut = 0;
            bool got = false;
            string note = null;
            for (int i = 0; i < 400 && !got; i++)
            {
                tug.Step(0.02, ut); rock.Step(0.02, ut); ut += 0.02;
                p0 = tug.vel * mT + rock.vel * mR;   // just before the latch
                got = tug.TryGrab(rock, out note);
            }
            Check(got, "never latched: " + note);
            Check(Math.Abs(tug.mass - (mT + mR)) < 0.01, $"mass {tug.mass:0.00} t, expected {mT + mR:0.00}");
            Vector3d p1 = tug.vel * tug.mass;
            Check((p1 - p0).magnitude / tug.mass < 0.01, $"velocity jumped by {(p1 - p0).magnitude / tug.mass:0.000} m/s");
            var held = tug.parts.Find(p => p.def.kind == PartKind.Rock);
            var grab = tug.parts.Find(p => p.def.kind == PartKind.Grabber);
            Check(held != null && held.dockedName == rock.name && tug.HeldRock(grab) == held, "the rock isn't recorded as held by the grabber");
            Check(tug.HeldRockNames().Contains(rock.name), "HeldRockNames misses it");
            Check(!tug.IsRock && tug.IsControllable, "the tug lost its controls");

            // Too fast, and side on
            var (t2, r2) = GrabSetup(0, 0.4, 3.0);
            bool fast = false; string n2 = null;
            for (int i = 0; i < 100 && !fast; i++) { t2.Step(0.02, i * 0.02); r2.Step(0.02, i * 0.02); fast = t2.TryGrab(r2, out n2) ; if (n2 != null) break; }
            Check(!fast && n2 != null && n2.Contains("too fast"), $"3 m/s: latched {fast}, note '{n2}'");
            var (t3, r3) = GrabSetup(0, 0.3, 0.3, 50f);
            bool side = t3.TryGrab(r3, out string n3);
            Check(!side && n3 != null && n3.Contains("off square"), $"50 deg off: latched {side}, note '{n3}'");

            // Release
            var piece = tug.Undock(held);
            Check(piece != null && piece.IsRock && piece.name == rock.name, $"release gave {piece?.name ?? "nothing"} ({piece?.parts.Count ?? 0} parts)");
            Check(Math.Abs(tug.mass - mT) < 0.01 && Math.Abs(piece.mass - mR) < 0.01, $"after release: tug {tug.mass:0.00} t, rock {piece.mass:0.00} t");
            Check(tug.HeldRock(grab) == null, "the grabber still thinks it holds the rock");
            bool again = tug.TryGrab(piece, out _);
            Check(!again, "re-grabbed instantly after letting go");
            return $"latched at 0.6 m/s after {ut:0.0} s: {mT:0.00} t tug + {mR:0} t rock; refused at 3 m/s ('{n2}') and 50 deg off; released cleanly";
        }

        static string PushWanderer()
        {
            var (tug, rock) = GrabSetup(1, 0.3, 0.4);
            bool got = false;
            for (int i = 0; i < 200 && !got; i++) { tug.Step(0.02, i * 0.02); rock.Step(0.02, i * 0.02); got = tug.TryGrab(rock, out _); }
            Check(got, "never latched onto the class B rock");
            // The engine pushes the tug's tail end, so the push goes from the engine through the grabber into the rock.
            tug.ActivateNextStage();
            tug.throttle = 1;
            tug.sas = SasMode.Stability;
            double ut = 10;
            for (int i = 0; i < 20; i++) { tug.Step(0.02, ut); ut += 0.02; }   // let thrust build
            Vector3d v0 = tug.vel, axis0 = tug.Axis, g0 = -tug.pos.normalized * Home14.mu / tug.pos.sqrMagnitude;
            double t0 = ut, thrust = 0;
            for (int i = 0; i < 500; i++) { tug.Step(0.02, ut); ut += 0.02; }
            foreach (var p in tug.parts) if (p.ignited && p.def.IsEngine) thrust += p.def.thrustVac;
            // Subtract gravity: compare with a free fall from the same state over the same time.
            double dt = ut - t0;
            Vector3d g = (g0 - tug.pos.normalized * Home14.mu / tug.pos.sqrMagnitude) * 0.5;
            double a = ((tug.vel - v0) / dt - g).magnitude, expect = thrust / tug.mass;
            Check(Math.Abs(a - expect) / expect < 0.06, $"acceleration {a:0.000} m/s2, expected {expect:0.000} ({thrust:0} kN / {tug.mass:0.0} t)");
            double turn = Vector3.Angle(axis0.ToFloat(), tug.Axis.ToFloat());
            Check(turn < 5, $"the heading drifted {turn:0.0} deg while pushing");
            Check(!tug.destroyed && tug.parts.Exists(p => p.def.kind == PartKind.Rock), "the rock broke off: " + tug.destroyReason);
            return $"{tug.mass:0.0} t together: {a:0.000} m/s2 from {thrust:0} kN (expected {expect:0.000}); heading held within {turn:0.0} deg";
        }

        static string CaptureWanderer()
        {
            var home = Home14;
            // A wanderer at its closest pass, 600 km up at 400 m/s arrival speed, and a tug that has matched it.
            Wanderers.Path(home, home.radius + 600_000, 400, 10, 30, 0, out var p0, out var v0, out double toPeri);
            var o = Orbit.FromState(p0, v0, home.mu, 0);
            o.StateAt(toPeri, out var rp, out var vp);
            var rock = VesselSim.CreateFree(VesselDesign.Stack("Wanderer A-CP2", "rock.a"), home, rp, vp, Quaternion.identity);
            rock.name = "Wanderer A-CP2";
            var c = new Career();
            c.Start(GameMode.Career);
            c.unlocked.Add("wanderers");
            c.liveWanderers.Add(rock.name);
            var k = new Contract { id = 99, type = "capture", title = "Capture", body = "Arden", target = rock.name, reward = 100_000, state = ContractState.Active, deadline = 1e9 };
            c.contracts.Add(k);
            c.CheckWanderers(new[] { rock }, toPeri);
            Check(k.state == ContractState.Active, "the contract finished before the rock was caught");
            var tug = VesselSim.CreateFree(VesselDesign.WranglerTug(), home, rp + vp.normalized * 40, vp, Quaternion.identity);
            // Line the grabber up on the rock along the path, closing at 0.4 m/s.
            var grab = tug.parts.Find(p => p.def.kind == PartKind.Grabber);
            Vector3d back = (-vp).normalized;
            tug.rot = Quaternion.FromToRotation(Vector3.up, back.ToFloat());
            Vector3d face = Vector3d.Rotate(tug.rot, Vector3d.FromFloat(grab.localPos - tug.com));
            tug.pos = rp + vp.normalized * (1.6 + 0.4) - face;   // just ahead of the rock, claws facing back at it
            tug.vel = vp - vp.normalized * 0.4;
            double ut = toPeri;
            bool got = false;
            for (int i = 0; i < 300 && !got; i++) { tug.Step(0.02, ut); rock.Step(0.02, ut); ut += 0.02; got = tug.TryGrab(rock, out _); }
            Check(got, "the tug never caught the rock");
            // The tug's nose (the grabber) already faces back along the path, so its engine pushes retrograde.
            tug.sas = SasMode.Retrograde;
            tug.ActivateNextStage();
            tug.throttle = 0;
            for (int i = 0; i < 400; i++) { tug.Step(0.02, ut); ut += 0.02; }   // turn round
            double speed0 = tug.vel.magnitude, dvUsed = 0;
            tug.throttle = 1;
            int guard = 0;
            while (!Career.Captured(tug, ut) && guard++ < 60_000)
            {
                double before = tug.vel.magnitude;
                tug.Step(0.02, ut); ut += 0.02;
                foreach (var p in tug.parts) if (p.ignited && p.def.IsEngine) dvUsed += p.def.thrustVac / tug.mass * 0.02 * tug.throttle;
            }
            tug.throttle = 0;
            Check(Career.Captured(tug, ut), $"never captured: {tug.Situation(ut)}");
            Check(dvUsed < 250, $"took {dvUsed:0} m/s");
            c.CheckWanderers(new[] { tug }, ut);
            Check(k.state == ContractState.Completed, $"contract {k.state}");
            Check((c.records & Records.FirstCapture) != 0, "no capture record");
            var oc = Orbit.FromState(tug.pos, tug.vel, home.mu, ut);
            return $"caught at {(rp.magnitude - home.radius) / 1000:0} km doing {vp.magnitude:0} m/s; {dvUsed:0} m/s of burn left it in a {(oc.Apoapsis - home.radius) / 1e6:0.0} Mm x {(oc.Periapsis - home.radius) / 1000:0} km orbit; contract paid";
        }

        static string RockSamples()
        {
            var c = new Career();
            c.Start(GameMode.Career);
            var (tug, rock) = GrabSetup(0, 2, 0.5);
            var grab = tug.parts.Find(p => p.def.kind == PartKind.Grabber);
            var e = Science.Get("rock");
            Check(e != null && grab.def.experiment == "rock", "the grabber has no rock experiment");
            Check(Lab.Preview(c, tug, e, 0, grab) < 0, "a sample could be taken without a wanderer");
            string refuse = Lab.Run(c, tug, grab, e, 0);
            Check(grab.science.Count == 0 && refuse.Contains("holding"), "ran without a rock: " + refuse);
            bool got = false;
            for (int i = 0; i < 400 && !got; i++) { tug.Step(0.02, i * 0.02); rock.Step(0.02, i * 0.02); got = tug.TryGrab(rock, out _); }
            Check(got, "never latched");
            float worth = Lab.Preview(c, tug, e, 10, grab);
            Check(worth > 10, $"a rock sample is worth {worth:0.0}");
            string ran = Lab.Run(c, tug, grab, e, 10);
            var d = grab.science.FirstOrDefault();
            Check(d != null && d.subject.Contains(rock.name) && d.title.Contains(rock.name), "result: " + ran);
            float gain = c.Credit(d, 1f, System4);
            Check(Math.Abs(gain - worth) < 0.5f, $"credited {gain:0.0}, previewed {worth:0.0}");
            // Another wanderer is a new subject
            float other = c.Remaining(d.subject.Replace(rock.name, "Wanderer B-ZZ9"), worth);
            Check(other > worth * 0.9f, "a second wanderer's sample is worth nothing");
            return $"{ran}; credited {gain:0.0} science";
        }

        static string WandererSave()
        {
            var u = new Universe();
            u.career.Start(GameMode.Sandbox);
            u.TendWanderers(System4);
            var free = u.vessels.First(v => v.IsRock);
            var (tug, rock) = GrabSetup(2, 0.5, 0.5);
            bool got = false;
            for (int i = 0; i < 200 && !got; i++) { tug.Step(0.02, i * 0.02); rock.Step(0.02, i * 0.02); got = tug.TryGrab(rock, out _); }
            Check(got, "never latched");
            u.Add(tug);
            double next = u.nextWandererUt;
            int seen = u.wanderersSeen;
            var json = u.ToJson();
            var back = new Universe();
            back.LoadJson(json, System4);
            var f2 = back.vessels.Find(v => v.IsRock);
            Check(f2 != null && f2.name == free.name && f2.parts[0].def == free.parts[0].def, "the free wanderer didn't come back");
            Check((f2.pos - free.pos).magnitude < 1 && (f2.vel - free.vel).magnitude < 0.01, "the free wanderer moved");
            var t2 = back.vessels.Find(v => v.name == tug.name);
            Check(t2 != null, "the tug didn't come back");
            var g2 = t2.parts.Find(p => p.def.kind == PartKind.Grabber);
            Check(t2.HeldRock(g2) != null && t2.HeldRockNames().Contains(rock.name), "the tug lost hold of its rock");
            Check(Math.Abs(t2.mass - tug.mass) < 0.01, $"mass {t2.mass:0.00} vs {tug.mass:0.00}");
            // (Unity's JSON doesn't always round-trip a double to the last bit: a second either way is fine.)
            Check(Math.Abs(back.nextWandererUt - next) < 1 && back.wanderersSeen == seen, $"the arrival clock was lost: {back.nextWandererUt:0.000} vs {next:0.000}, seen {back.wanderersSeen} vs {seen}");
            return $"free {free.name} and a tug holding {rock.name} ({tug.mass:0.0} t) load back as they were";
        }

        static string WandererContracts()
        {
            var c = new Career();
            c.Start(GameMode.Career);
            c.liveWanderers.Add("Wanderer B-KR3");
            c.RefreshOffers(0);
            Check(!c.Offered.Any(k => k.type == "grab" || k.type == "capture"), "wanderer contracts before the grabber is researched");
            c.unlocked.Add("wanderers");
            // Offers are a random few of the candidates: reshuffle until both have shown up.
            Contract grab = null, cap = null;
            var all = new List<Contract>();
            for (int i = 0; i < 40 && (grab == null || cap == null); i++)
            {
                c.contracts.RemoveAll(k => k.state == ContractState.Offered);
                c.seed = 100 + i;
                c.RefreshOffers(0);
                foreach (var k in c.contracts.Where(k => k.state == ContractState.Offered)) all.Add(k);
                grab = grab ?? c.contracts.FirstOrDefault(k => k.type == "grab" && k.state == ContractState.Offered);
                cap = cap ?? c.contracts.FirstOrDefault(k => k.type == "capture" && k.state == ContractState.Offered);
            }
            Check(grab != null && cap != null, "wanderer contracts offered: " + string.Join(" | ", all.Where(k => k.type == "grab" || k.type == "capture").Select(k => k.title).Distinct()));
            Check(cap.target == "Wanderer B-KR3", $"capture contract names {cap.target}");
            cap.state = ContractState.Active;
            if (grab != null)
            {
                grab.state = ContractState.Active;
                c.OnGrabbed(null, "Wanderer B-KR3");
                Check(grab.state == ContractState.Completed, $"grab contract {grab.state}");
            }
            c.CheckWanderers(new VesselSim[0], 100);
            Check(cap.state == ContractState.Failed, $"capture contract {cap.state} after its wanderer left");
            return $"offered: {grab.title} ({grab.reward:N0}), {cap.title} ({cap.reward:N0}); capture fails when the rock is gone";
        }

        static string WranglerPreset()
        {
            var b = Body;
            var v = VesselSim.CreateOnPad(VesselDesign.Wrangler(), b, 0);
            var ap = new FlightAutopilot();
            double ut = 0, top = 0, maxAoa = 0;
            var log = new System.Text.StringBuilder();
            double m0 = v.mass, twr0 = 0;
            foreach (var r in v.StageReports()) { twr0 = r.twrAsl; break; }
            for (int i = 0; i < 200_000 && !v.destroyed && ap.phase != FlightAutopilot.Phase.Done; i++)
            {
                if (ap.Update(v, ut)) { v.ActivateNextStage(); log.Append($" stage{v.nextStage}@{ut:0}s/{v.Altitude / 1000:0.0}km"); }
                v.Step(0.02, ut); ut += 0.02;
                top = Math.Max(top, v.Altitude);
                var sv = v.SurfaceRelVelocity;
                if (sv.magnitude > 50 && v.Altitude < 40_000) maxAoa = Math.Max(maxAoa, Vector3.Angle(v.Axis.ToFloat(), sv.normalized.ToFloat()));
            }
            Check(!v.destroyed, $"destroyed on the way up: {v.destroyReason}; {m0:0.0} t, liftoff TWR {twr0:0.00}, peak {top / 1000:0.0} km, worst AoA {maxAoa:0} deg, phase {ap.phase};{log}");
            var o = Orbit.FromState(v.pos, v.vel, b.mu, ut);
            Check(o.Periapsis - b.radius > b.atmosphereHeight, $"not in orbit: {(o.Apoapsis - b.radius) / 1000:0} x {(o.Periapsis - b.radius) / 1000:0} km");
            double dv = 0; foreach (var r in v.StageReports()) dv += r.dvVac;
            Check(dv > 2200, $"only {dv:0} m/s left in orbit");
            // The tug alone, pushing a class B rock
            var tug = VesselSim.CreateFree(VesselDesign.WranglerTug(), Home14, new Vector3d(Home14.radius + 300_000, 0, 0), Vector3d.zero, Quaternion.identity);
            double mTug = tug.mass, dvTug = 0; foreach (var r in tug.StageReports()) dvTug += r.dvVac;
            double dry = mTug - tug.parts.Sum(p => p.propellant);
            double ve = dvTug / Math.Log(mTug / dry);   // exhaust speed, from the tug's own delta-v
            double withB = ve * Math.Log((mTug + 60) / (dry + 60)), withC = ve * Math.Log((mTug + 170) / (dry + 170));
            Check(withB > 450, $"with a class B rock the tug has only {withB:0} m/s");
            Check(withC > 150, $"with a class C rock the tug has only {withC:0} m/s");
            return $"orbit {(o.Apoapsis - b.radius) / 1000:0} x {(o.Periapsis - b.radius) / 1000:0} km with {dv:0} m/s left; the tug ({mTug:0.0} t) alone {dvTug:0} m/s, {withB:0} m/s pushing class B, {withC:0} m/s class C";
        }
    }
}
