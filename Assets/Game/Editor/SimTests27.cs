using System;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 27 tests: places to discover, weather and wind, prints and tracks, space stations, the docking guide.</summary>
    public static partial class SimTests
    {
        static void RunMilestone27()
        {
            Run("Places to discover: nine on seven worlds, each on the ground (Arden's on dry land away from the space centre), the probe in a low spot", LandmarkPlaces);
            Run("Discovering: flying low over one spots it, landing beside it finds it once (science, funds, a badge); kept in the save", LandmarkDiscovery);
            Run("Weather: the same at the same place and time; Arden has rain sometimes, Ochre dust storms, airless worlds none; wind stays sensible and fades out high up", WeatherBasics);
            Run("Wind: a capsule under its parachute drifts downwind; with the weather off it doesn't", WindDrift);
            Run("Prints and tracks: kept up to a limit, the oldest go first, a new world starts afresh", GroundMarks);
            Run("Stations: ships docked together count as pieces; a station contract needs the pieces, seats, a free port, antenna and solar; the docking guide's advice", StationsAndDocking);
        }

        static string LandmarkPlaces()
        {
            var sys = CelestialBody.CreateSystem();
            var all = Landmarks.All(sys).ToList();
            Check(all.Count == Landmarks.Count && all.Count == 9, $"{all.Count} landmarks placed, expected {Landmarks.Count}");
            Check(all.Select(l => l.id).Distinct().Count() == all.Count, "two landmarks share an id");
            Check(all.Select(l => l.body).Distinct().Count() == 7, "landmarks aren't on seven worlds");
            var sb = new System.Text.StringBuilder();
            foreach (var l in all)
            {
                var b = sys.First(x => x.name == l.body);
                double h = Terrain.Height(b, l.dir);
                Check(Math.Abs(l.dir.magnitude - 1) < 1e-6, $"{l.name}: direction isn't a unit vector");
                Check(h < Terrain.MaxHeight(b), $"{l.name}: {h:0} m is above {b.name}'s highest ground");
                if (b.IsHomeWorld)
                {
                    Check(!Terrain.IsWater(b, l.dir) && h > 50, $"{l.name} is in the sea or on the beach ({h:0} m)");
                    double km = Vector3d.Angle(l.dir, LaunchSite.Direction) * b.radius / 1000;
                    Check(km > 20 && km < 200, $"{l.name} is {km:0} km from the space centre");
                    sb.Append($"{l.name} {km:0} km from the pad at {h:0} m; ");
                }
                if (l.kind == LandmarkKind.Probe)
                {
                    // the probe lies lower than the ground round it (a crater floor)
                    Vector3d e = CelestialBody.EastAt(l.dir), n = CelestialBody.NorthAt(l.dir);
                    double ring = 0;
                    for (int k = 0; k < 8; k++) { double a = k * Math.PI / 4; ring += Terrain.Height(b, (l.dir + (e * Math.Cos(a) + n * Math.Sin(a)) * (8000 / b.radius)).normalized); }
                    ring /= 8;
                    Check(h < ring, $"the Fallen Probe ({h:0} m) isn't lower than the ground round it ({ring:0} m)");
                    sb.Append($"probe {ring - h:0} m below its surroundings; ");
                }
                if (l.kind == LandmarkKind.Caldera)
                    Check(Vector3d.Angle(l.dir, Vector3d.FromFloat(Terrain.GreatMound).normalized) < 1e-6, "the caldera isn't on the Great Mound");
            }
            return sb.ToString();
        }

        static string LandmarkDiscovery()
        {
            var u = new Universe();
            u.career.Start(GameMode.Career);
            var sys = CelestialBody.CreateSystem();
            var tal = sys.First(b => b.name == "Tal");
            var probe = Landmarks.On(tal).First(l => l.kind == LandmarkKind.Probe);
            // flying past high up: not spotted; low and near: spotted
            var d = VesselDesign.Stack("Scout", "probe.spark", "tank.s1", "eng.ember");
            Vector3d far = (probe.dir + CelestialBody.EastAt(probe.dir) * (200_000 / tal.radius)).normalized;
            var flyer = VesselSim.CreateFree(d, tal, far * (tal.radius + 20_000), Vector3d.zero, Quaternion.identity);
            flyer.hasLaunched = true;
            Landmarks.Update(u, flyer, 0);
            Check(!u.career.spotted.Contains(probe.id), "spotted from 200 km away");
            var low = VesselSim.CreateFree(d, tal, probe.dir * (tal.radius + 30_000), Vector3d.zero, Quaternion.identity);
            low.hasLaunched = true;
            Landmarks.Update(u, low, 0);
            Check(u.career.spotted.Contains(probe.id) && !u.career.visited.Contains(probe.id), "not spotted flying 30 km over it");
            // landing 2 km off isn't a visit; landing 100 m off is, once
            Vector3d off2 = (probe.dir + CelestialBody.EastAt(probe.dir) * (2000 / tal.radius)).normalized;
            var lander = VesselSim.CreateLanded(d, tal, off2, 0); lander.hasLaunched = true;
            Landmarks.Update(u, lander, 0);
            Check(!u.career.visited.Contains(probe.id), "found from 2 km away");
            Vector3d near = (probe.dir + CelestialBody.EastAt(probe.dir) * (100 / tal.radius)).normalized;
            lander = VesselSim.CreateLanded(d, tal, near, 0); lander.hasLaunched = true;
            double sci = u.career.science, funds = u.career.funds;
            Landmarks.Update(u, lander, 0);
            Check(u.career.visited.Contains(probe.id), "not found landing 100 m away");
            double gotSci = u.career.science - sci, gotFunds = u.career.funds - funds;
            var explorer = Achievements.Get("explorer");
            Check(Math.Abs(gotSci - probe.science - explorer.science) < 1e-6 && Math.Abs(gotFunds - probe.funds - explorer.funds) < 1e-6,
                $"paid {gotSci:0} science and {gotFunds:N0} funds, expected {probe.science + explorer.science:0} and {probe.funds + explorer.funds:N0}");
            Check(u.career.badges.Contains("explorer"), "no explorer badge");
            sci = u.career.science;
            Landmarks.Update(u, lander, 0);
            Check(u.career.science == sci, "paid twice");
            // kept in the save
            var u2 = new Universe();
            u2.career.Load(u.career.ToSave());
            Check(u2.career.spotted.Contains(probe.id) && u2.career.visited.Contains(probe.id), "landmarks not saved");
            return $"{probe.name}: +{gotSci:0} science, +{gotFunds:N0} funds (with the badge)";
        }

        static string WeatherBasics()
        {
            bool was = Weather.On;
            Weather.On = true;
            try
            {
                var sys = CelestialBody.CreateSystem();
                var arden = sys.First(b => b.IsHomeWorld);
                var ochre = sys.First(b => b.name == "Ochre");
                var vesk = sys.First(b => b.name == "Vesk");
                Check(!Weather.Has(vesk) && !Weather.Has(sys.First(b => b.name == "Tal")) && Weather.Has(arden) && Weather.Has(ochre), "the wrong worlds have weather");
                var rng = new System.Random(5);
                int n = 3000, rain = 0, storm = 0;
                double maxWind = 0, maxDust = 0;
                for (int i = 0; i < n; i++)
                {
                    var d = new Vector3d(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1).normalized;
                    double t = rng.NextDouble() * 400 * 86400;
                    var a = Weather.At(arden, d, t);
                    var a2 = Weather.At(arden, d, t);
                    Check(a.windSpeed == a2.windSpeed && a.rain == a2.rain, "weather changed between two looks at the same moment");
                    Check(Math.Abs(a.windFixed.magnitude - a.windSpeed) < 1e-6 && Math.Abs(Vector3d.Dot(a.windFixed, d)) < 1e-6 * Math.Max(1, a.windSpeed), "wind isn't along the ground");
                    if (a.rain > 0.4f) rain++;
                    maxWind = Math.Max(maxWind, a.windSpeed);
                    var o = Weather.At(ochre, d, t);
                    if (o.dust > 0.4f) storm++;
                    maxWind = Math.Max(maxWind, o.windSpeed);
                    maxDust = Math.Max(maxDust, o.dust);
                    Check(o.rain == 0 && a.dust == 0, "rain on Ochre or dust storms on Arden");
                }
                double rainShare = rain / (double)n, stormShare = storm / (double)n;
                Check(rainShare > 0.02 && rainShare < 0.4, $"it rains {rainShare:P0} of the time on Arden");
                Check(stormShare > 0.02 && stormShare < 0.4, $"dust storms {stormShare:P0} of the time on Ochre");
                Check(maxWind < 50, $"wind reaches {maxWind:0} m/s");
                Check(Weather.WindScale(30_000) == 0 && Weather.WindScale(1000) > 0.8, "wind doesn't fade out high up");
                Check(Weather.WindInertial(vesk, new Vector3d(vesk.radius + 100, 0, 0), 0).magnitude == 0, "wind on Vesk");
                // over time a place's weather changes
                var spot = CelestialBody.DirectionFromLatLon(10, 20);
                int changes = 0; float last = Weather.At(arden, spot, 0).rain;
                for (int h = 1; h < 24 * 20; h++) { float r = Weather.At(arden, spot, h * 3600.0).rain; if ((r > 0.4f) != (last > 0.4f)) changes++; last = r; }
                string desc = Weather.At(arden, spot, 0).Describe(spot);
                Check(desc.Contains("wind") || desc.Contains("still"), "weather line doesn't mention the wind: " + desc);
                return $"Arden rain {rainShare:P0}, Ochre storms {stormShare:P0}, strongest wind {maxWind:0} m/s, {changes} rain changes in 20 days at one spot; \"{desc}\"";
            }
            finally { Weather.On = was; Weather.Override = null; }
        }

        static string WindDrift()
        {
            double Drift(bool weather)
            {
                bool was = Weather.On;
                Weather.On = weather;
                var b = Body;
                Vector3d up = CelestialBody.DirectionFromLatLon(20, 40);
                var east = CelestialBody.EastAt(up);
                Weather.Override = new WeatherNow { windSpeed = 15, windFixed = east * 15 };
                try
                {
                    var d = VesselDesign.Stack("Drop", "chute.s", "pod.wren");
                    Vector3d pos = up * (b.radius + 3000);
                    var v = VesselSim.CreateFree(d, b, pos, b.SurfaceVelocity(pos), Quaternion.LookRotation(east.ToFloat(), up.ToFloat()));
                    v.hasLaunched = true;
                    foreach (var p in v.parts) if (p.def.kind == PartKind.Parachute) p.chuteDeployed = true;
                    double t = 0;
                    for (int i = 0; i < 3000; i++) { v.Step(0.02, t); t += 0.02; }
                    Vector3d f = b.InertialToFixed(v.pos, t).normalized;
                    Check(!v.destroyed && !v.landed, $"the capsule {(v.destroyed ? "broke up" : "landed")} during the test");
                    return Vector3d.Dot(f - up, east) * b.radius;
                }
                finally { Weather.On = was; Weather.Override = null; }
            }
            double calm = Drift(false), windy = Drift(true);
            Check(Math.Abs(calm) < 20, $"drifted {calm:0} m east with no wind");
            Check(windy > 300, $"only drifted {windy:0} m downwind in a minute in a 15 m/s wind");
            return $"one minute under the chute: {calm:0} m with the weather off, {windy:0} m downwind in 15 m/s";
        }

        static string GroundMarks()
        {
            var sys = CelestialBody.CreateSystem();
            var tal = sys.First(b => b.name == "Tal");
            var vesk = sys.First(b => b.name == "Vesk");
            GroundFx.Clear();
            Vector3d d0 = CelestialBody.DirectionFromLatLon(3, 4), e = CelestialBody.EastAt(d0);
            for (int i = 0; i < GroundFx.MaxMarks + 500; i++)
            {
                Vector3d d = (d0 + e * (i * 0.5 / tal.radius)).normalized;
                GroundFx.Add(tal, d, 0, 0, e, 0.3f, 0.13f, Color.gray, true);
            }
            Check(GroundFx.Count == GroundFx.MaxMarks, $"{GroundFx.Count} marks kept, limit {GroundFx.MaxMarks}");
            GroundFx.Add(vesk, d0, 0, 0, e, 0.3f, 0.13f, Color.gray, true);
            Check(GroundFx.Count == 1, "marks from Tal kept after moving to Vesk");
            GroundFx.Clear();
            return $"limit {GroundFx.MaxMarks}";
        }

        static System.Collections.Generic.List<Contract> CandidatesOf(Career c) =>
            (System.Collections.Generic.List<Contract>)typeof(Career).GetMethod("Candidates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(c, new object[] { 0.0 });

        static string StationsAndDocking()
        {
            var b = Body;
            var v = VesselSim.CreateFree(VesselDesign.Stack("Core", "port.std", "pod.wren", "tank.s1", "port.std"), b, new Vector3d(b.radius + 200_000, 0, 0), new Vector3d(0, 0, Math.Sqrt(b.mu / (b.radius + 200_000))), Quaternion.identity);
            Check(Stations.DockedPieces(v) == 1, "a single ship counts as more than one piece");
            // pretend two ships docked into the starter rocket: two latched pairs of ports
            var ports = v.parts.Where(p => p.def.kind == PartKind.DockingPort).ToList();
            string pieces = "starter has no ports";
            if (ports.Count >= 2)
            {
                ports[0].dockedTo = ports[1].index; ports[1].dockedTo = ports[0].index;
                Check(Stations.DockedPieces(v) == 2, "one docking didn't make two pieces");
                pieces = "2 pieces";
            }
            // the advice
            Check(Stations.DockHint(50, 0.1, 40, 0.5).StartsWith("Turn"), "no advice to turn when facing away");
            Check(Stations.DockHint(10, 4, 3, 0.2).Contains("sideways"), "no advice to slide sideways when off the line");
            Check(Stations.DockHint(10, 0.1, 3, 4).StartsWith("Too fast"), "no warning closing at 4 m/s from 10 m");
            Check(Stations.DockHint(100, 0.1, 3, -0.5).StartsWith("Drifting away"), "no warning drifting away");
            Check(Stations.DockHint(20, 0.1, 3, 2).StartsWith("Good"), "a good approach isn't called good");
            Check(Stations.GoodSpeed(1000) == 5 && Stations.GoodSpeed(1) == 0.3, "comfortable speeds off");
            // station contracts get offered once docking is unlocked and the orbit record is set
            var u = new Universe();
            u.career.Start(GameMode.Career);
            u.career.records |= Records.Orbit;
            Check(!CandidatesOf(u.career).Any(c => c.type == "station"), "station contract offered before the first docking");
            u.career.records |= Records.Docking;
            if (!u.career.unlocked.Contains("docking")) u.career.unlocked.Add("docking");
            var offers = CandidatesOf(u.career);
            bool offered = offers.Any(c => c.type == "station" && c.body == "Arden");
            Check(offered, "no station contract offered after the first docking");
            return $"{pieces}; station offered: {offered}";
        }
    }
}
