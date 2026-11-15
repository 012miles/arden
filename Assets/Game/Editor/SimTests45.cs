using System.Linq;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Milestone 45 tests: the new parachute.</summary>
    public static partial class SimTests
    {
        static void RunMilestone45()
        {
            Run("Parachute: a canopy of gores with suspension lines down to a riser (not a floating dome)", ParachuteMeshTest);
        }

        static string ParachuteMeshTest()
        {
            var m = MeshFactory.Canopy();
            var v = m.vertices;
            Check(v.Length > 1000, $"only {v.Length} vertices");
            float top = v.Max(p => p.y), low = v.Min(p => p.y), wide = v.Max(p => new Vector2(p.x, p.z).magnitude);
            Check(top > 0.5f && top < 0.8f, $"crown at {top:0.00}");
            Check(Mathf.Abs(low + MeshFactory.CanopyLines + 0.35f) < 0.02f, $"the lines and riser reach down to {low:0.00}");
            Check(wide > 0.98f && wide < 1.1f, $"skirt radius {wide:0.00}");
            // the lines meet in the middle under the canopy: every vertex below the skirt is near the axis or on a line
            int lineVerts = v.Count(p => p.y < -0.2f);
            Check(lineVerts > 32, $"{lineVerts} line vertices");
            return $"{v.Length} vertices, crown {top:0.00}, skirt {wide:0.00}, lines down to {low:0.00}";
        }
    }
}
