using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    public partial class AutoTest
    {
        /// <summary>A group photo: six Ardlings in a row on Tal, each with their own suit, sticky-note face and hat.</summary>
        IEnumerator CrewPhotoTour()
        {
            var root = GameRoot.I;
            var u = root.universe;
            var tal = root.moon;
            // Names whose looks differ: distinct suits and faces, most with a hat
            var picked = new List<string>();
            var suits = new HashSet<Color>(); var faces = new HashSet<int>(); var hats = new HashSet<int>();
            foreach (var fn in CrewNames.FirstNames)
                {
                    if (picked.Count >= 6) break;
                    string n = fn + " " + CrewNames.Surname;
                    var l = Ardlings.LookFor(n);
                    if (suits.Contains(l.suit) || faces.Contains(l.face) || (l.hat != 0 && hats.Contains(l.hat)) || (l.hat == 0 && picked.Count < 5)) continue;
                    picked.Add(n); suits.Add(l.suit); faces.Add(l.face); hats.Add(l.hat);
                }

            Vector3d sun = Sun.DirectionAt(u.ut);
            Vector3d centre = (tal.InertialToFixed(sun, u.ut) + new Vector3d(0.3, 0.35, 0.15)).normalized;
            VesselSim first0 = null;
            Vector3d right = Vector3d.zero;
            var crew = new List<VesselSim>();
            for (int i = 0; i < picked.Count; i++)
            {
                Vector3d dir = i == 0 ? centre : (centre * tal.radius + right * (i * 1.05)).normalized;
                var v = VesselSim.CreateLanded(VesselDesign.Stack(picked[i], "eva.suit"), tal, dir, u.ut);
                v.parts[0].crew.Clear();
                v.parts[0].crew.Add(picked[i]);
                v.throttle = 0; v.hasLaunched = true;
                u.Add(v);
                crew.Add(v);
                if (i == 0)
                {
                    first0 = v;
                    // the row runs along the first one's right hand, everyone facing the same way
                    Vector3d up = tal.FixedToInertial(centre, u.ut);
                    Vector3d facing = Vector3d.FromFloat(v.rot * Vector3.forward);
                    right = tal.InertialToFixed(Vector3d.Cross(up, facing).normalized, u.ut);
                }
            }
            root.Fly(crew[2]);
            var f = root.flight;
            yield return new WaitForSecondsRealtime(2f);
            Vector3 face = crew[2].rot * Vector3.forward;
            f.LookAlong(Vector3d.FromFloat(-face), 6.5f, 12f, 6f);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("320_ardling_lineup");
            f.LookAlong(Vector3d.FromFloat(-face), 4.2f, -18f, 2f);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("321_ardling_closeup");
            Log("crew photo: " + string.Join(", ", picked.Select(n => { var l = Ardlings.LookFor(n); return $"{n} ({Ardlings.FaceNames[l.face]}, {Ardlings.HatNames[l.hat]})"; })));
        }
    }
}
