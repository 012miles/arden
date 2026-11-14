using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 20 tests: the pieces of the new look that can be checked without a screen.</summary>
    public static partial class SimTests
    {
        static void RunMilestone20()
        {
            Run("Planet maps: cube-face directions follow Unity's layout (face centres on the axes, row 0 at the top)", CubeFaceDirections);
            Run("Ground kinds: weights stay 0..1 and add to at most 1 on every world; Arden's seas are sand, its poles snow", GroundKinds);
            Run("Ardlings: the same name always gets the same look; all ten faces and every hat turn up across names", ArdlingLooks);
            Run("Ardlings: the spacewalk suit is Ardling-sized and the sticky-note faces are drawn", ArdlingSuit);
        }

        static string CubeFaceDirections()
        {
            Vector3[] centres = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int f = 0; f < 6; f++)
                Check(Vector3.Angle(PlanetBaker.Dir(f, 0, 0), centres[f]) < 0.01f, $"face {f} centre points {PlanetBaker.Dir(f, 0, 0)}");
            // On the four side faces the top row (v = -1) leans up; every texel direction is a unit vector
            foreach (int f in new[] { 0, 1, 4, 5 })
                Check(PlanetBaker.Dir(f, 0, -0.9f).y > 0.5f, $"face {f} top row points {PlanetBaker.Dir(f, 0, -0.9f)}");
            Check(PlanetBaker.Dir(4, 0.9f, 0).x > 0.5f && PlanetBaker.Dir(0, 0.9f, 0).z < -0.5f, "u runs the wrong way on +Z or +X");
            return "six faces, D3D layout";
        }

        static string GroundKinds()
        {
            var rng = new System.Random(20);
            int sea = 0, seaSand = 0, pole = 0, poleSnow = 0;
            foreach (var b in System4)
            {
                for (int k = 0; k < 400; k++)
                {
                    var d = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1).normalized;
                    Vector3 w = MeshFactory.GroundWeights(b, d);
                    Check(w.x >= -1e-4f && w.y >= -1e-4f && w.z >= -1e-4f && w.x + w.y + w.z <= 1.0001f, $"{b.name} weights {w} at {d}");
                    if (b.style != BodyStyle.Earthlike) continue;
                    if (MeshFactory.WaterMask(b, d) > 0.5f) { sea++; if (w.x + w.y > 0.99f) seaSand++; }   // sand, or snow near the poles
                    if (Mathf.Abs(d.y) > 0.95f) { pole++; if (w.y > 0.8f) poleSnow++; }
                }
            }
            Check(sea > 20 && seaSand == sea, $"{seaSand}/{sea} sea points have a sandy (or snowy) floor");
            var home = System4[0];
            var north = new Vector3(0.05f, 1f, 0.02f).normalized;
            Check(MeshFactory.GroundWeights(home, north).y > 0.8f, $"Arden's north pole snow weight {MeshFactory.GroundWeights(home, north).y:0.00}");
            return $"{System4.Count} worlds; {sea} sea samples all sandy";
        }

        static string ArdlingLooks()
        {
            var faces = new HashSet<int>();
            var hats = new HashSet<int>();
            for (int i = 0; i < 400; i++)
            {
                string name = $"{(char)('A' + i % 26)}ardling {i}";
                var a = Ardlings.LookFor(name);
                var b = Ardlings.LookFor(name);
                Check(a.face == b.face && a.hat == b.hat && a.suit == b.suit && a.paper == b.paper && a.noteTilt == b.noteTilt, $"{name} looks different twice");
                Check(a.face >= 0 && a.face < Ardlings.Faces && a.hat >= 0 && a.hat < Ardlings.Hats, $"{name} face {a.face} hat {a.hat}");
                faces.Add(a.face);
                hats.Add(a.hat);
            }
            Check(faces.Count == Ardlings.Faces, $"only {faces.Count} faces used");
            Check(hats.Count == Ardlings.Hats, $"only {hats.Count} hats used");
            Check(Ardlings.FaceNames.Length == Ardlings.Faces && Ardlings.HatNames.Length == Ardlings.Hats, "face/hat name lists");
            return $"{faces.Count} faces, {hats.Count} hats (incl. none)";
        }

        static string ArdlingSuit()
        {
            Check(Mathf.Abs(PartCatalog.EvaSuit.length - Ardlings.Height) < 1e-4f, $"suit length {PartCatalog.EvaSuit.length}");
            var look = Ardlings.LookFor("Kaia Wren");
            var body = Ardlings.Body(look);
            var b = body.bounds;
            Check(b.max.y > -0.05f && b.min.y > -Ardlings.Height - 0.08f && b.min.y < -Ardlings.Height + 0.1f, $"body spans {b.min.y:0.00}..{b.max.y:0.00}");
            var core = Ardlings.Body(look, false);
            Check(core.vertexCount < body.vertexCount, "the core without limbs isn't smaller");
            var arm = Ardlings.Arm(look, 1).bounds;
            Check(arm.center.magnitude < 0.2f, $"arm isn't built round its shoulder (centre {arm.center})");
            var tex = Ardlings.FaceTexture(look.face, look.paper);
            Check(tex != null && tex.width == 128, "no face texture");
            // The marker face has dark ink on it somewhere in the middle
            var px = tex.GetPixels();
            int ink = px.Count(c => c.grayscale < 0.2f);
            Check(ink > 200, $"only {ink} inked pixels");
            return $"{Ardlings.Height} m tall, {ink} inked pixels";
        }
    }
}
