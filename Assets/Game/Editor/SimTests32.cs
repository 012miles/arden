using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>
    /// Milestone 32 tests: a sturdier, easier builder (the root can't be lost, random edits never break a rocket), the new
    /// presets and parts, the Easy checklist and search, the interface size, the transonic drag, and fairer jobs in rooms.
    /// </summary>
    public static partial class SimTests
    {
        static void RunMilestone32()
        {
            Run("Builder: deleting or picking up a part never takes the root, even when the root shares a symmetry group", RootSurvives);
            Run("Builder: 3,000 random edits (add, symmetry, delete, move, re-root, save and load) never break the rocket's tree", BuilderFuzz);
            Run("Presets: ten new ones, every preset is a valid design, and the new rockets lift off with enough speed budget for their job", NewPresets);
            Run("Parts: ten new ones, each with a price, a research node, a category, a description and an Easy tip", NewParts);
            Run("Easy builder: the checklist ticks off as a rocket is built; the parts search finds by name, kind and words", ChecklistAndSearch);
            Run("Interface size: fits every screen shape without running short of room; the size setting only makes it smaller", InterfaceScale);
            Run("Physics: drag rises through the speed of sound and eases off when fast; the speed of sound falls with height", TransonicDrag);
            Run("Rooms: jobs are kept when someone joins or leaves, shared out fairly, and taking a job never leaves anyone with nothing", RoomJobs);
        }

        static string RootSurvives()
        {
            // the side-booster rocket, re-rooted onto one of its boosters: the root now shares a symmetry id with the other
            var d = VesselDesign.Broadside();
            int srb = d.nodes.FindIndex(n => n.id == "srb.cinder");
            DesignOps.Reroot(d, srb);
            Check(d.nodes[0].id == "srb.cinder" && d.nodes[0].sym != 0, "the test rocket's root should be a booster in a symmetry group");
            int other = Enumerable.Range(1, d.nodes.Count - 1).First(i => d.nodes[i].sym == d.nodes[0].sym);
            var kill = DesignOps.RemovalSet(d, other);
            Check(!kill.Contains(0), "deleting the root's symmetry partner would delete the root too");
            int before = d.nodes.Count;
            DesignOps.Remove(d, kill);
            Check(DesignOps.TreeProblem(d) == null, "after the delete: " + DesignOps.TreeProblem(d));
            Check(d.nodes.Count < before && d.nodes.Count > 5, $"parts {before} -> {d.nodes.Count}");
            Check(DesignOps.RemovalSet(d, 0).Count == 0, "the root itself can't be removed this way");
            return $"{before} parts, deleted {kill.Count}, the root kept";
        }

        static string BuilderFuzz()
        {
            var rng = new System.Random(32032);
            string[] stack = { "tank.s1", "tank.s2", "tank.s3", "tank.s4", "eng.kestrel", "eng.ember", "eng.swift", "dec.s", "fins.s", "nose.s", "chute.s", "adapt.ls", "tank.l4", "eng.heron", "srb.flint" };
            string[] side = { "fin.r", "fin.l", "solar.s", "antenna.s", "leg.s", "rcs.b", "dec.r", "light.s", "sci.therm" };
            int ops = 0, rockets = 0;
            for (int run = 0; run < 15; run++)
            {
                var d = run % 3 == 0 ? VesselDesign.Default() : run % 3 == 1 ? VesselDesign.Broadside() : Presets.TwinBooster();
                rockets++;
                for (int k = 0; k < 200; k++, ops++)
                {
                    int op = rng.Next(6), n = d.nodes.Count;
                    string what = "";
                    try
                    {
                        if (n == 0) { d = VesselDesign.Stack("Restart", "pod.wren", "tank.s2", "eng.kestrel"); continue; }
                        int i = rng.Next(n);
                        switch (op)
                        {
                            case 0:   // stack a part on top or under a part that has room
                            {
                                string id = stack[rng.Next(stack.Length)];
                                var def = PartCatalog.Get(id);
                                what = $"stack {id} on {d.nodes[i].id}";
                                if (DesignOps.BottomFree(d, i) && def.stackTop) DesignOps.AttachStack(d, i, false, id);
                                else if (DesignOps.TopFree(d, i) && def.stackBottom) DesignOps.AttachStack(d, i, true, id);
                                break;
                            }
                            case 1:   // something on the side, in symmetry
                            {
                                var pd = PartCatalog.Get(d.nodes[i].id);
                                if (pd == null || pd.radial) break;
                                string id = side[rng.Next(side.Length)];
                                what = $"side {id} on {d.nodes[i].id}";
                                DesignOps.AttachSurfaceAt(d, i, (float)rng.NextDouble(), rng.Next(360), id, 1 + rng.Next(4), rng.Next(4) == 0);
                                break;
                            }
                            case 2:   // delete
                                what = $"delete {d.nodes[i].id}";
                                DesignOps.Remove(d, DesignOps.RemovalSet(d, i));
                                break;
                            case 3:   // pick a part up and put it on another
                            {
                                if (i == 0) break;
                                what = $"move {d.nodes[i].id}";
                                var sub = DesignOps.Extract(d, i);
                                DesignOps.Remove(d, DesignOps.RemovalSet(d, i));
                                int to = rng.Next(d.nodes.Count);
                                var tp = PartCatalog.Get(d.nodes[to].id);
                                if (tp == null || tp.radial) break;
                                var pose = d.nodes[to].pos + d.nodes[to].rot * new Vector3(tp.RMid, -tp.length * 0.5f, 0);
                                DesignOps.InsertSymmetric(d, sub, to, Attach.Surface, pose, d.nodes[to].rot, 1 + rng.Next(3), false);
                                break;
                            }
                            case 4:   // a new root
                                what = $"re-root on {d.nodes[i].id}";
                                DesignOps.Reroot(d, i);
                                break;
                            case 5:   // save and load
                                what = "save and load";
                                d = d.Clone();
                                break;
                        }
                        DesignOps.ApplyDefaultGroups(d);
                        string bad = DesignOps.TreeProblem(d);
                        Check(bad == null, $"after {what}: {bad}");
                        d.Validate();
                        d.Stages();
                        Staging.AnalyzeDesign(d, 9.81);
                        var com = DesignOps.CenterOfMass(d);
                        Check(!float.IsNaN(com.x + com.y + com.z), $"after {what}: centre of mass is NaN");
                        if (d.nodes.Count > 120) d = VesselDesign.Default();   // keep it quick
                    }
                    catch (Exception e) when (!(e.Message.StartsWith("after ")))
                    {
                        throw new Exception($"run {run} step {k} ({what}): {e.GetType().Name}: {e.Message}");
                    }
                }
            }
            return $"{ops} edits on {rockets} rockets, no broken trees";
        }

        static string NewPresets()
        {
            var all = Presets.All();
            Check(all.Count >= 22, $"{all.Count} presets");
            foreach (var name in Presets.NewOnes) Check(all.Any(p => p.label == name), $"missing preset {name}");
            var lines = new List<string>();
            var need = new Dictionary<string, double>
            {
                { "Sky Hopper (tiny booster)", 400 }, { "Science Hopper", 1000 }, { "Abort test (escape tower)", 800 }, { "Twin Booster", 3400 },
                { "Orbiter (heat shield)", 3400 }, { "Tal Lander (land and return)", 5600 }, { "Satellite launcher", 3400 }, { "Big Lift (heavy probe)", 4500 },
                { "Quad Booster", 3400 }, { "Station Core (big)", 3400 },
            };
            foreach (var (label, d) in all)
            {
                Check(d.Validate() == null, $"{label}: {d.Validate()}");
                Check(DesignOps.TreeProblem(d) == null, $"{label}: {DesignOps.TreeProblem(d)}");
                if (!need.ContainsKey(label)) continue;
                var reps = Staging.AnalyzeDesign(d, 9.81);
                var first = reps.FirstOrDefault(r => r.hasEngines);
                double dv = reps.Sum(r => r.dvVac);
                Check(first != null && first.twrAsl >= 1.15, $"{label}: liftoff push/weight {first?.twrAsl:0.00}");
                Check(dv >= need[label], $"{label}: speed budget {dv:0} m/s, needs {need[label]:0}");
                lines.Add($"{label.Split(' ')[0]} {dv:0}/{first.twrAsl:0.0}");
            }
            return string.Join(", ", lines);
        }

        static string NewParts()
        {
            string[] ids = { "tank.s3", "tank.l2", "tank.l16", "eng.swift", "eng.condor", "srb.flint", "srb.blaze", "nose.l", "adapt.sl", "fin.l" };
            foreach (var id in ids)
            {
                var d = PartCatalog.Get(id);
                Check(d != null, $"{id} isn't in the catalogue");
                Check(TechTree.NodeOf(id) != null, $"{id} can't be researched");
                Check(Prices.Of(d) > 200, $"{id} has no price");
                Check(!string.IsNullOrEmpty(d.description) && !string.IsNullOrEmpty(d.Category), $"{id} needs a description and category");
            }
            // every buildable part explains itself in Easy words
            foreach (var d in PartCatalog.All.Where(p => p.kind != PartKind.Eva && p.kind != PartKind.Flag && p.kind != PartKind.Rock && p.kind != PartKind.FairingHalf))
                Check(!string.IsNullOrEmpty(BuildHelp.Tip(d)), $"{d.id} has no Easy tip");
            var condor = PartCatalog.Get("eng.condor");
            Check(condor.ispVac > PartCatalog.Get("eng.heron").ispVac && condor.ThrustAsl < condor.thrustVac * 0.5f, "the Condor should be a vacuum engine");
            return $"{ids.Length} new parts; {PartCatalog.All.Count} in all";
        }

        static string ChecklistAndSearch()
        {
            var empty = new VesselDesign();
            var items = BuildHelp.Checklist(empty, new List<StageReport>());
            Check(items.Count >= 4 && items.All(i => !i.done), "an empty floor should need everything");
            var ff = VesselDesign.FirstFlight();
            var done = BuildHelp.Checklist(ff, Staging.AnalyzeDesign(ff, 9.81));
            Check(done.All(i => i.done), "the first-flight rocket should tick every box: " + string.Join("; ", done.Where(i => !i.done).Select(i => i.text)));
            var heavy = VesselDesign.Stack("Brick", "pod.wren", "tank.l16", "tank.l16", "adapt.sl", "eng.kestrel");
            var hc = BuildHelp.Checklist(heavy, Staging.AnalyzeDesign(heavy, 9.81));
            Check(hc.Any(i => !i.done && i.text.StartsWith("Too heavy")), "a rocket too heavy to lift should say so");
            int Count(string s) => PartCatalog.All.Count(p => BuildHelp.Matches(p, s));
            Check(Count("kestrel") == 1, $"'kestrel' found {Count("kestrel")}");
            Check(Count("engine") >= 6, $"'engine' found {Count("engine")}");
            Check(Count("") == PartCatalog.All.Count, "an empty search shows everything");
            Check(Count("zzzz") == 0, "nonsense finds nothing");
            Check(BuildHelp.Matches(PartCatalog.Get("chute.s"), "parachute"), "parachute");
            Check(BuildHelp.Matches(PartCatalog.Get("tank.l16"), "fuel 2.6"), "two words: fuel and 2.6");
            return $"{items.Count} items; 'engine' finds {Count("engine")}";
        }

        static string InterfaceScale()
        {
            var screens = new (int w, int h, string name)[]
            {
                (2560, 1664, "13\" MacBook Air"), (2880, 1864, "15\" MacBook Air"), (1470, 956, "13\" (points)"), (1710, 1107, "15\" (points)"),
                (1920, 1080, "16:9"), (1280, 720, "small 16:9"), (1024, 768, "4:3"), (3440, 1440, "ultrawide"), (1440, 900, "16:10"),
            };
            foreach (var (w, h, name) in screens)
            {
                float s = Ui.ComputeScale(w, h, 1f);
                Check(w / s >= Ui.MinWidth - 0.5f && h / s >= Ui.MinHeight - 0.5f, $"{name}: layout {w / s:0}x{h / s:0} is smaller than {Ui.MinWidth}x{Ui.MinHeight}");
                Check(Mathf.Abs(w / s - Ui.MinWidth) < 1f || Mathf.Abs(h / s - Ui.MinHeight) < 1f, $"{name}: should be as big as fits");
                Check(Ui.ComputeScale(w, h, 0.8f) < s, $"{name}: 80% should be smaller");
                Check(Mathf.Approximately(Ui.ComputeScale(w, h, 1.3f), s), $"{name}: an old 130% setting can't overlap");
            }
            return $"{screens.Length} screens";
        }

        static string TransonicDrag()
        {
            Check(Mathf.Approximately(VesselSim.MachDrag(0.5f), 1f) && Mathf.Approximately(VesselSim.MachDrag(0.79f), 1f), "below Mach 0.8 nothing changes");
            float peak = VesselSim.MachDrag(1.05f);
            Check(peak > 1.4f && peak < 1.5f, $"just past Mach 1: x{peak:0.00}");
            Check(VesselSim.MachDrag(2f) < peak && VesselSim.MachDrag(2f) > 1.1f, $"Mach 2: x{VesselSim.MachDrag(2f):0.00}");
            Check(Mathf.Approximately(VesselSim.MachDrag(3.5f), 1f) && Mathf.Approximately(VesselSim.MachDrag(20f), 1f), "reentry speeds are as before");
            for (float m = 0.5f; m < 6f; m += 0.01f)
                Check(Mathf.Abs(VesselSim.MachDrag(m + 0.01f) - VesselSim.MachDrag(m)) < 0.05f, $"a jump near Mach {m:0.00}");
            var bodies = CelestialBody.CreateSystem();
            var home = bodies.Find(b => b.IsHomeWorld);
            var v = VesselSim.CreateOnPad(VesselDesign.Default(), home, 0);
            double sea = v.SoundSpeed();
            Check(Math.Abs(sea - 340) < 3, $"speed of sound on the pad {sea:0}");
            return $"x{peak:0.00} at Mach 1.05, {sea:0} m/s on the pad";
        }

        static string RoomJobs()
        {
            var room = new NetRoom { IsHost = true, Connected = true };
            room.State.activity = 1;
            var host = new NetPlayer { id = 1, name = "Host", roles = 15 };
            room.State.players.Add(host);
            int R(NetPlayer p) => p.roles;
            var mia = new NetPlayer { id = 2, name = "Mia" };
            room.State.players.Add(mia); room.GiveJobsTo(mia);
            Check(R(host) == 0b0011 && R(mia) == 0b1100, $"two players: host {R(host)}, Mia {R(mia)}");
            // Mia takes the builder's job: the host keeps the pilot's
            room.Apply(2, new NetMsg { t = "take", n = 0 });
            Check(mia.Has(MRole.Lead) && !host.Has(MRole.Lead) && host.roles != 0, $"after Mia takes building: host {R(host)}, Mia {R(mia)}");
            var leo = new NetPlayer { id = 3, name = "Leo" };
            room.State.players.Add(leo); room.GiveJobsTo(leo);
            Check(leo.roles != 0 && mia.Has(MRole.Lead), $"Leo joins: Leo {R(leo)}, Mia still builds {mia.Has(MRole.Lead)}");
            var zoe = new NetPlayer { id = 4, name = "Zoe" };
            room.State.players.Add(zoe); room.GiveJobsTo(zoe);
            Check(room.State.players.All(p => p.roles != 0), "four players: everyone has a job");
            int all = room.State.players.Aggregate(0, (a, p) => a | p.roles);
            Check(all == 15 && room.State.players.Sum(p => CountBits(p.roles)) == 4, "each job held exactly once");
            // someone with one job has it taken: they get one back from the taker... the taker had only one, so a straight swap
            int zoeJob = zoe.roles, leoJob = leo.roles;
            room.Apply(3, new NetMsg { t = "take", n = Log2(zoeJob) });
            Check(leo.roles == zoeJob && zoe.roles == leoJob, $"swap: Leo {R(leo)}, Zoe {R(zoe)}");
            // Leo leaves: his job goes to whoever has the fewest
            room.State.players.Remove(leo);
            room.HandOnJobs(leo.roles);
            Check(room.State.players.Aggregate(0, (a, p) => a | p.roles) == 15, "every job still covered after Leo left");
            Check(mia.Has(MRole.Lead), "Mia kept building through it all");
            return $"host {R(host)}, Mia {R(mia)}, Zoe {R(zoe)}";
        }

        static int CountBits(int v) { int n = 0; for (; v != 0; v &= v - 1) n++; return n; }
        static int Log2(int v) { for (int k = 0; k < 4; k++) if ((v & (1 << k)) != 0) return k; return 0; }
    }
}
