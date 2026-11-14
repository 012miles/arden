using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>Electricity (EC) and the comms link: storage, solar, alternators, probe and reaction-wheel draw.</summary>
    public partial class VesselSim
    {
        public const double WheelEcPerKnms = 0.03; // EC per kN*m*s of reaction-wheel torque
        public const double LightEc = 0.04, TransmitEc = 10;

        public double ecRate;           // net EC/s last step
        public bool commandable;        // a powered probe core or a crewed command part is aboard (set in RecomputeMass)
        public bool connected = true;   // has a link home (the flight updates this twice a second)
        public double signal = 1;
        float wheelUse;                 // kN*m of wheel torque used in the last rotation step

        public double EcStored { get { double s = 0; foreach (var p in parts) s += p.charge; return s; } }
        public double EcCapacity { get { double s = 0; foreach (var p in parts) s += p.def.ecCapacity; return s; } }

        /// <summary>An uncrewed vessel with no link home: it can't take new commands (only cut throttle and steer).</summary>
        public bool PartialControl => IsControllable && CrewCount == 0 && !connected;

        IEnumerable<CelestialBody> NearBodies()
        {
            yield return body;
            if (body.parent != null) yield return body.parent;
            foreach (var c in body.children) yield return c;
        }

        public bool InSunlight(double ut)
        {
            Vector3d abs = AbsolutePosition(ut);
            sunDir = Sun.DirectionFrom(abs, ut);
            return Sun.Lights(abs, NearBodies(), ut);
        }

        Vector3d sunDir = Sun.Fixed; // toward the sun, as of the last InSunlight check

        /// <summary>
        /// How well a deployed panel faces the sun. Panels pivot on their arm (the part's outward axis) to follow it, so
        /// only the sun lying along the arm defeats them. Mount panels at right angles to each other for all-round power.
        /// </summary>
        float SolarFacing(PartState p)
        {
            Vector3 arm = rot * (p.localRot * Vector3.right);
            float c = Vector3.Dot(arm, sunDir.ToFloat());
            return Mathf.Sqrt(Mathf.Max(0f, 1f - c * c));
        }

        /// <summary>Solar output right now (EC/s), for the HUD.</summary>
        public double SolarOutput(double ut)
        {
            bool panels = false;
            foreach (var p in parts) if (p.def.solarRate > 0 && p.legDeploy > 0.9f) { panels = true; break; }
            if (!panels || !InSunlight(ut)) return 0;
            double s = 0;
            foreach (var p in parts) if (p.def.solarRate > 0 && p.legDeploy > 0.9f) s += p.def.solarRate * SolarFacing(p);
            return s;
        }

        // Production and use each step are worked out in UpdateSystems (OreSystem.cs), together with the mining machines.

        /// <summary>Adds (or, if negative, drains) electricity across the batteries; returns what actually moved.</summary>
        public double AddCharge(double amount)
        {
            double moved = 0;
            if (amount > 0)
            {
                double room = 0;
                foreach (var p in parts) room += Math.Max(0, p.def.ecCapacity - p.charge);
                if (room <= 0) return 0;
                double k = Math.Min(1, amount / room);
                foreach (var p in parts) { double r = Math.Max(0, p.def.ecCapacity - p.charge) * k; p.charge += r; moved += r; }
            }
            else if (amount < 0)
            {
                double have = EcStored;
                if (have <= 0) return 0;
                double k = Math.Min(1, -amount / have);
                foreach (var p in parts) { double r = p.charge * k; p.charge -= r; moved -= r; }
            }
            return moved;
        }
    }
}
