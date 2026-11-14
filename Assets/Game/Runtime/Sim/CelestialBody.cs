using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    public enum BodyStyle { Earthlike, Barren, Icy, Star, Desert, Gas, Volcanic }

    /// <summary>
    /// A planet or moon. Each body's frame is centred on it with its spin axis along +Y. A moon follows a
    /// circular equatorial orbit around its parent. Vessels feel only the gravity of the body whose
    /// sphere of influence (SOI) they are in — the patched-conic approximation.
    /// </summary>
    public class CelestialBody
    {
        public string name;
        public double radius;            // m
        public double mu;                // m^3/s^2
        public double rotationPeriod;    // s
        public double atmosphereHeight;  // m (0 = airless)
        public double seaLevelDensity;   // kg/m^3
        public double scaleHeight;       // m
        public int seed;
        public BodyStyle style;
        public float scienceMult = 1f;   // how much more a moon's science is worth (farther = more)
        public Color sky = new Color(0.36f, 0.58f, 0.9f); // daytime sky colour under its air (Arden's blue)

        public CelestialBody parent;
        public readonly List<CelestialBody> children = new List<CelestialBody>();
        public double orbitRadius, orbitPhase0; // circular orbit around parent
        // The orbit's plane: where it is at phase 0 (orbitX) and the axis it heads away from a quarter turn later (orbitZ).
        // Moons circle their planet's equator (the defaults); a planet's orbit round the sun is tilted to its equator.
        public Vector3d orbitX = new Vector3d(1, 0, 0), orbitZ = new Vector3d(0, 0, 1);
        public bool isHome, isStar;
        /// <summary>A gas giant's rings, from ringInner to ringOuter radii (0 = none).</summary>
        public double ringInner, ringOuter;
        /// <summary>No solid surface: a gas giant's clouds just get thicker the deeper you go.</summary>
        public bool IsGasGiant => style == BodyStyle.Gas;
        /// <summary>The home planet: "absolute" positions are measured from it, so everything near home stays precise.</summary>
        public CelestialBody home;

        public double AngularVelocity => rotationPeriod > 0 ? 2 * Math.PI / rotationPeriod : 0;
        public Vector3d AngularVelocityVector => new Vector3d(0, AngularVelocity, 0);
        public double SurfaceGravity => mu / (radius * radius);
        public bool HasAtmosphere => atmosphereHeight > 0;
        public double OrbitalAngularVelocity => parent == null ? 0 : Math.Sqrt(parent.mu / (orbitRadius * orbitRadius * orbitRadius));
        public double OrbitPeriod => parent == null ? double.PositiveInfinity : 2 * Math.PI / OrbitalAngularVelocity;
        /// <summary>Laplace sphere-of-influence radius.</summary>
        public double SoiRadius => parent == null ? double.PositiveInfinity : orbitRadius * Math.Pow(mu / parent.mu, 0.4);
        /// <summary>Below this the vessel is in air (or close to the ground on airless worlds).</summary>
        public double SafeAltitude => HasAtmosphere ? atmosphereHeight : 0;

        public double Altitude(Vector3d pos) => pos.magnitude - radius;

        public double Density(double altitude)
        {
            if (!HasAtmosphere || altitude >= atmosphereHeight) return 0;
            if (altitude < 0) altitude = 0;
            double rho = seaLevelDensity * Math.Exp(-altitude / scaleHeight);
            double fade = (atmosphereHeight - altitude) / (atmosphereHeight * 0.1);
            return fade < 1 ? rho * fade : rho;
        }

        /// <summary>Air pressure in atmospheres (1 = Arden at sea level): engines, the HUD and the air gauge all use it.</summary>
        public double PressureAtm(double altitude) => HasAtmosphere ? Density(altitude) / StandardDensity : 0;
        public const double StandardDensity = 1.2; // kg/m3 at one atmosphere (Arden's sea level)

        public double RotationAngle(double ut) => (AngularVelocity * ut) % (2 * Math.PI);

        public Quaternion RotationAt(double ut) =>
            Quaternion.Euler(0f, (float)(RotationAngle(ut) * (180.0 / Math.PI)), 0f);

        public Vector3d FixedToInertial(Vector3d p, double ut) => Vector3d.RotateY(p, RotationAngle(ut));
        public Vector3d InertialToFixed(Vector3d r, double ut) => Vector3d.RotateY(r, -RotationAngle(ut));
        public Vector3d SurfaceVelocity(Vector3d r) => new Vector3d(r.z, 0, -r.x) * AngularVelocity;

        public Vector3d Gravity(Vector3d r)
        {
            double m = r.magnitude;
            return r * (-mu / (m * m * m));
        }

        /// <summary>Position relative to the parent body (zero for the root body).</summary>
        public Vector3d PositionAt(double ut)
        {
            if (parent == null) return Vector3d.zero;
            double th = orbitPhase0 + OrbitalAngularVelocity * ut;
            return (orbitX * Math.Cos(th) - orbitZ * Math.Sin(th)) * orbitRadius;
        }

        public Vector3d VelocityAt(double ut)
        {
            if (parent == null) return Vector3d.zero;
            double w = OrbitalAngularVelocity, th = orbitPhase0 + w * ut;
            return (orbitX * -Math.Sin(th) - orbitZ * Math.Cos(th)) * (orbitRadius * w);
        }

        /// <summary>Position relative to the root of the system (the sun).</summary>
        public Vector3d RootPosition(double ut) => parent == null ? Vector3d.zero : parent.RootPosition(ut) + PositionAt(ut);
        public Vector3d RootVelocity(double ut) => parent == null ? Vector3d.zero : parent.RootVelocity(ut) + VelocityAt(ut);

        /// <summary>Position in the home-planet frame (home at the origin; the sun far off, going round once a year).</summary>
        public Vector3d AbsolutePosition(double ut) => home == null ? RootPosition(ut) : RootPosition(ut) - home.RootPosition(ut);
        public Vector3d AbsoluteVelocity(double ut) => home == null ? RootVelocity(ut) : RootVelocity(ut) - home.RootVelocity(ut);

        /// <summary>A planet in its own right (circles the sun, or is the root): not a moon.</summary>
        public bool IsPlanet => !isStar && (parent == null || parent.isStar);

        /// <summary>The home planet (launch site, its own science values); a lone planet made without a sun counts too.</summary>
        public bool IsHomeWorld => isHome || (parent == null && !isStar);

        public static Vector3d EastAt(Vector3d up)
        {
            var e = new Vector3d(up.z, 0, -up.x);
            return e.sqrMagnitude > 1e-12 ? e.normalized : new Vector3d(0, 0, -1);
        }

        /// <summary>North = up x east (so that east x up = south).</summary>
        public static Vector3d NorthAt(Vector3d up) => Vector3d.Cross(up, EastAt(up)).normalized;

        public static Vector3d DirectionFromLatLon(double latDeg, double lonDeg)
        {
            double lat = latDeg * Math.PI / 180, lon = lonDeg * Math.PI / 180;
            return new Vector3d(Math.Cos(lat) * Math.Cos(lon), Math.Sin(lat), -Math.Cos(lat) * Math.Sin(lon));
        }

        /// <summary>The home planet: 700 km radius, 1 g, 6 h day, 75 km atmosphere.</summary>
        public static CelestialBody CreateHome() => new CelestialBody
        {
            name = "Arden", radius = 700_000, mu = 9.81 * 700_000.0 * 700_000.0, rotationPeriod = 6 * 3600,
            atmosphereHeight = 75_000, seaLevelDensity = 1.2, scaleHeight = 5_800, seed = 1337, style = BodyStyle.Earthlike,
        };

        /// <summary>The moon: 200 km radius, 0.16 g, airless, 12,000 km out, tidally locked.</summary>
        public static CelestialBody CreateMoon(CelestialBody parent)
        {
            var m = new CelestialBody
            {
                name = "Tal", radius = 200_000, mu = 1.6 * 200_000.0 * 200_000.0,
                atmosphereHeight = 0, seaLevelDensity = 0, scaleHeight = 1, seed = 4242, style = BodyStyle.Barren,
                parent = parent, orbitRadius = 12_000_000, orbitPhase0 = 2.2,
            };
            m.rotationPeriod = m.OrbitPeriod;
            parent.children.Add(m);
            return m;
        }

        /// <summary>The outer moon: a small ball of ice, 110 km radius, 0.07 g, 26,000 km out, tidally locked.</summary>
        public static CelestialBody CreateVesk(CelestialBody parent)
        {
            var m = new CelestialBody
            {
                name = "Vesk", radius = 110_000, mu = 0.7 * 110_000.0 * 110_000.0,
                atmosphereHeight = 0, seaLevelDensity = 0, scaleHeight = 1, seed = 9191, style = BodyStyle.Icy,
                parent = parent, orbitRadius = 26_000_000, orbitPhase0 = 4.4, scienceMult = 1.6f,
            };
            m.rotationPeriod = m.OrbitPeriod;
            parent.children.Add(m);
            return m;
        }

        /// <summary>The sun: 200,000 km across the middle, far too hot to visit; Arden circles it 13.6 Gm out.</summary>
        public static CelestialBody CreateSun() => new CelestialBody
        {
            name = "Helion", radius = 200_000_000, mu = 1.81e18, rotationPeriod = 30 * 86400.0,
            atmosphereHeight = 0, seaLevelDensity = 0, scaleHeight = 1, seed = 77, style = BodyStyle.Star, isStar = true, scienceMult = 1.5f,
        };

        /// <summary>
        /// Puts a planet on its orbit round the sun so that, at time 0, the sun lies in the direction 'sunDir' as seen from
        /// the planet, at its furthest north or south of the equator (so the planet's tilt is the sun's height there).
        /// </summary>
        static void OrbitSun(CelestialBody planet, CelestialBody sun, double radius, Vector3d sunDir)
        {
            planet.parent = sun;
            planet.orbitRadius = radius;
            planet.orbitPhase0 = 0;
            Vector3d p0 = -sunDir.normalized, up = new Vector3d(0, 1, 0);
            Vector3d n = (up - p0 * Vector3d.Dot(up, p0)).normalized; // orbit axis, tilted from the planet's spin axis
            planet.orbitX = p0;
            planet.orbitZ = Vector3d.Cross(p0, n);                     // so it goes round prograde about n
            sun.children.Add(planet);
        }

        /// <summary>
        /// Ochre: a dusty orange desert planet 21.5 Gm from the sun, in Arden's orbital plane. 320 km radius, 0.31 g, thin
        /// air up to 50 km, an 18 h day. It starts 130 deg ahead of Arden, so the first trip window opens about 40 days in.
        /// </summary>
        public static CelestialBody CreateOchre(CelestialBody sun, CelestialBody home)
        {
            var p = new CelestialBody
            {
                name = "Ochre", radius = 320_000, mu = 3.0 * 320_000.0 * 320_000.0, rotationPeriod = 18 * 3600,
                atmosphereHeight = 50_000, seaLevelDensity = 0.18, scaleHeight = 5_500, seed = 5151, style = BodyStyle.Desert,
                scienceMult = 2.5f, sky = new Color(0.78f, 0.58f, 0.4f),
                parent = sun, orbitRadius = 21_500_000_000, orbitPhase0 = 130 * Math.PI / 180,
                orbitX = home.orbitX, orbitZ = home.orbitZ,
            };
            sun.children.Add(p);
            return p;
        }

        /// <summary>Mote: Ochre's little moon, a 45 km lump of grey rock 3,000 km out, tidally locked.</summary>
        public static CelestialBody CreateMote(CelestialBody ochre)
        {
            var m = new CelestialBody
            {
                name = "Mote", radius = 45_000, mu = 0.3 * 45_000.0 * 45_000.0,
                atmosphereHeight = 0, seaLevelDensity = 0, scaleHeight = 1, seed = 6262, style = BodyStyle.Barren,
                parent = ochre, orbitRadius = 3_000_000, orbitPhase0 = 1.1, scienceMult = 2.2f,
            };
            m.rotationPeriod = m.OrbitPeriod;
            ochre.children.Add(m);
            return m;
        }

        /// <summary>
        /// Brume: a banded, ringed gas giant 48 Gm from the sun (a 568-day year), in Arden's orbital plane. 3,000 km to its
        /// cloud tops, 0.82 g there, a 9 h day. Its thick air (220 km deep) makes aerobraking easy; below the cloud tops
        /// there's nothing to land on. It starts 300 deg ahead of Arden, so the first trip window opens about two months in.
        /// </summary>
        public static CelestialBody CreateBrume(CelestialBody sun, CelestialBody home)
        {
            var p = new CelestialBody
            {
                name = "Brume", radius = 3_000_000, mu = 8.0 * 3_000_000.0 * 3_000_000.0, rotationPeriod = 9 * 3600,
                atmosphereHeight = 220_000, seaLevelDensity = 2.4, scaleHeight = 22_000, seed = 8484, style = BodyStyle.Gas,
                scienceMult = 3.0f, sky = new Color(0.86f, 0.74f, 0.52f),
                parent = sun, orbitRadius = 48_000_000_000, orbitPhase0 = 300 * Math.PI / 180,
                orbitX = home.orbitX, orbitZ = home.orbitZ,
                ringInner = 4_600_000, ringOuter = 7_600_000,
            };
            sun.children.Add(p);
            return p;
        }

        /// <summary>Pyre: Brume's inner moon, 190 km of black rock and lava, 1.4 g, 9,500 km out (just outside the rings).</summary>
        public static CelestialBody CreatePyre(CelestialBody brume)
        {
            var m = new CelestialBody
            {
                name = "Pyre", radius = 190_000, mu = 1.4 * 190_000.0 * 190_000.0,
                atmosphereHeight = 0, seaLevelDensity = 0, scaleHeight = 1, seed = 6464, style = BodyStyle.Volcanic,
                parent = brume, orbitRadius = 9_500_000, orbitPhase0 = 3.9, scienceMult = 4.0f,
            };
            m.rotationPeriod = m.OrbitPeriod;
            brume.children.Add(m);
            return m;
        }

        /// <summary>Rime: Brume's outer moon, a 260 km ball of cracked ice, 1.0 g, 16,000 km out.</summary>
        public static CelestialBody CreateRime(CelestialBody brume)
        {
            var m = new CelestialBody
            {
                name = "Rime", radius = 260_000, mu = 1.0 * 260_000.0 * 260_000.0,
                atmosphereHeight = 0, seaLevelDensity = 0, scaleHeight = 1, seed = 7373, style = BodyStyle.Icy,
                parent = brume, orbitRadius = 16_000_000, orbitPhase0 = 0.7, scienceMult = 3.5f,
            };
            m.rotationPeriod = m.OrbitPeriod;
            brume.children.Add(m);
            return m;
        }

        /// <summary>
        /// The Helion system: Arden (with its moons Tal and Vesk), Ochre (with Mote) and Brume (with Rime and Pyre) circling
        /// the sun. Returns [home, Tal, Vesk, sun, Ochre, Mote, Brume, Rime, Pyre]: the home planet stays first.
        /// "Absolute" positions are measured from Arden.
        /// </summary>
        public static List<CelestialBody> CreateSystem()
        {
            var sun = CreateSun();
            var home = CreateHome();
            home.isHome = true;
            OrbitSun(home, sun, 13_600_000_000, Sun.Fixed);
            var moon = CreateMoon(home);
            var vesk = CreateVesk(home);
            var ochre = CreateOchre(sun, home);
            var mote = CreateMote(ochre);
            var brume = CreateBrume(sun, home);
            var rime = CreateRime(brume);
            var pyre = CreatePyre(brume);
            var all = new List<CelestialBody> { home, moon, vesk, sun, ochre, mote, brume, rime, pyre };
            foreach (var b in all) b.home = home;
            Sun.Body = sun;
            return all;
        }
    }
}
