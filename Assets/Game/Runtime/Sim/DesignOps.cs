using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>Editing operations on a part-tree design (used by the builder, presets and tests).</summary>
    public static class DesignOps
    {
        static PartDef Def(PartNode n) => PartCatalog.Get(n.id);

        public static Vector3 Axis(PartNode n) => n.rot * Vector3.up;
        public static Vector3 BottomNode(PartNode n, PartDef d) => n.pos + n.rot * new Vector3(0, -d.length, 0);
        public static Vector3 Center(PartNode n, PartDef d) => n.pos + n.rot * d.CenterLocal;

        public static bool TopFree(VesselDesign d, int i)
        {
            var def = Def(d.nodes[i]);
            if (def == null || def.radial || !def.stackTop || d.nodes[i].attach == Attach.Below) return false;
            for (int j = 0; j < d.nodes.Count; j++) if (d.nodes[j].parent == i && d.nodes[j].attach == Attach.Above) return false;
            return true;
        }

        public static bool BottomFree(VesselDesign d, int i)
        {
            var def = Def(d.nodes[i]);
            if (def == null || def.radial || !def.stackBottom || d.nodes[i].attach == Attach.Above) return false;
            for (int j = 0; j < d.nodes.Count; j++) if (d.nodes[j].parent == i && d.nodes[j].attach == Attach.Below) return false;
            return true;
        }

        public static int NextSym(VesselDesign d) => d.nodes.Count == 0 ? 1 : d.nodes.Max(n => n.sym) + 1;

        public static void ApplyDefaultGroups(VesselDesign d)
        {
            var defs = d.Defs();
            bool tower = defs.Exists(x => x != null && x.kind == PartKind.EscapeTower);
            for (int i = 0; i < d.nodes.Count; i++)
            {
                var def = defs[i];
                if (def == null) continue;
                if (def.kind == PartKind.Leg || def.kind == PartKind.Gear) d.nodes[i].groups |= Groups.Bit(Groups.Gear);
                if (def.kind == PartKind.Light) d.nodes[i].groups |= Groups.Bit(Groups.Lights);
                if (def.IsDeployable) d.nodes[i].groups |= Groups.Bit(Groups.Deploy);
                if (def.kind == PartKind.EscapeTower) d.nodes[i].groups |= Groups.Bit(Groups.Abort);
            }
            if (!tower) return;
            // Abort also drops everything below the capsule: the first stack decoupler under a command part.
            var topo = d.Topo();
            for (int i = 0; i < d.nodes.Count; i++)
            {
                if (defs[i] == null || defs[i].kind != PartKind.Command) continue;
                var q = new Queue<int>(topo.children[i]);
                while (q.Count > 0)
                {
                    int c = q.Dequeue();
                    if (defs[c].kind == PartKind.Decoupler) { d.nodes[c].groups |= Groups.Bit(Groups.Abort); break; }
                    foreach (int cc in topo.children[c]) q.Enqueue(cc);
                }
            }
        }

        public static int AttachStack(VesselDesign d, int parent, bool onTop, string id, int sym = 0)
        {
            var p = d.nodes[parent];
            var pd = Def(p);
            var cd = PartCatalog.Get(id);
            Vector3 pos = onTop ? p.pos + Axis(p) * cd.length : BottomNode(p, pd);
            d.nodes.Add(new PartNode { id = id, parent = parent, attach = onTop ? Attach.Above : Attach.Below, pos = pos, rot = p.rot, sym = sym });
            ApplyDefaultGroupsFor(d, d.nodes.Count - 1);
            return d.nodes.Count - 1;
        }

        static void ApplyDefaultGroupsFor(VesselDesign d, int i)
        {
            var def = Def(d.nodes[i]);
            if (def.kind == PartKind.Leg || def.kind == PartKind.Gear) d.nodes[i].groups |= Groups.Bit(Groups.Gear);
            if (def.kind == PartKind.Light) d.nodes[i].groups |= Groups.Bit(Groups.Lights);
            if (def.IsDeployable) d.nodes[i].groups |= Groups.Bit(Groups.Deploy);
            if (def.kind == PartKind.EscapeTower) d.nodes[i].groups |= Groups.Bit(Groups.Abort);
        }

        /// <summary>Orientation for a part mounted on a surface: local +X along the outward normal, local +Y along the parent's axis.</summary>
        public static Quaternion OrientOnSurface(Vector3 normal, Vector3 axis)
        {
            Vector3 x = normal.normalized;
            Vector3 up = axis - x * Vector3.Dot(axis, x);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.Cross(x, Vector3.forward);
            up.Normalize();
            return Quaternion.LookRotation(Vector3.Cross(x, up), up);
        }

        /// <summary>The symmetric copies of a pose around a parent: radial (n-way) about its axis, or a mirror pair.</summary>
        public static List<(Vector3 pos, Quaternion rot)> SymmetricPoses(PartNode parent, Vector3 pos, Quaternion rot, int count, bool mirror)
        {
            var res = new List<(Vector3, Quaternion)>();
            Vector3 axis = Axis(parent), c = parent.pos;
            if (mirror)
            {
                res.Add((pos, rot));
                // Mirror across the vessel's centre plane (x = 0: left/right); if the parent's axis points sideways,
                // fall back to a plane through the parent's axis.
                Vector3 m = Vector3.right - axis * Vector3.Dot(Vector3.right, axis);
                if (m.sqrMagnitude > 0.5f) c = Vector3.zero;
                else m = (pos - c) - axis * Vector3.Dot(pos - c, axis);
                if (m.sqrMagnitude < 1e-8f) m = parent.rot * Vector3.right;
                m.Normalize();
                Vector3 off = pos - c;
                Vector3 R(Vector3 v) => v - 2f * Vector3.Dot(v, m) * m;
                // Reflect the outward (X) and up (Y) axes; Z is rebuilt so the result is a proper rotation.
                Vector3 rx = R(rot * Vector3.right), ry = R(rot * Vector3.up);
                res.Add((c + R(off), Quaternion.LookRotation(Vector3.Cross(rx, ry), ry)));
                return res;
            }
            count = Math.Max(1, count);
            for (int k = 0; k < count; k++)
            {
                Quaternion q = Quaternion.AngleAxis(360f * k / count, axis);
                res.Add((c + q * (pos - c), q * rot));
            }
            return res;
        }

        /// <summary>
        /// Where a part sits when stuck onto a surface at 'hit': side-mounted parts lie against the hull, but a docking port
        /// or a grabber stands on it with its open face outward (so it can be flown into something from the side).
        /// </summary>
        public static void SurfacePose(PartDef def, Vector3 hit, Vector3 normal, Vector3 parentAxis, Quaternion extra, out Vector3 pos, out Quaternion rot)
        {
            if (def.kind == PartKind.DockingPort || def.kind == PartKind.Grabber)
            {
                Vector3 nrm = normal.normalized, fwd = parentAxis - nrm * Vector3.Dot(parentAxis, nrm);
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(nrm, Vector3.right);
                rot = Quaternion.LookRotation(fwd.normalized, nrm) * extra;
                pos = hit + rot * new Vector3(0, def.length, 0);   // the bottom face sits on the hull, the open top faces out
                return;
            }
            rot = OrientOnSurface(normal, parentAxis) * extra;
            pos = hit - rot * def.SurfaceAttachLocal;
        }

        public static List<int> AttachSurface(VesselDesign d, int parent, Vector3 hit, Vector3 normal, string id, int symmetry, bool mirror, Quaternion extra)
        {
            var sub = new List<PartNode> { new PartNode { id = id, attach = Attach.Surface } };
            var cd = PartCatalog.Get(id);
            SurfacePose(cd, hit, normal, Axis(d.nodes[parent]), extra, out var spos, out var rot);
            sub[0].rot = rot;
            sub[0].pos = spos;
            return InsertSymmetric(d, sub, parent, Attach.Surface, sub[0].pos, sub[0].rot, Def(d.nodes[parent]).radial ? 1 : symmetry, mirror);
        }

        /// <summary>Side-mount on a stack part at a height fraction (0 = top, 1 = bottom) and angle around its axis.</summary>
        public static List<int> AttachSurfaceAt(VesselDesign d, int parent, float frac, float angleDeg, string id, int symmetry, bool mirror)
        {
            var p = d.nodes[parent];
            var pd = Def(p);
            float a = angleDeg * Mathf.Deg2Rad;
            float r = Mathf.Lerp(pd.topDiameter, pd.bottomDiameter, frac) * 0.5f;
            Vector3 local = new Vector3(Mathf.Cos(a) * r, -pd.length * frac, Mathf.Sin(a) * r);
            return AttachSurface(d, parent, p.pos + p.rot * local, p.rot * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), id, symmetry, mirror, Quaternion.identity);
        }

        /// <summary>Hangs a part on the outer face of a radial part (a booster on a radial decoupler).</summary>
        public static int AttachToOuterFace(VesselDesign d, int radialPart, string id)
        {
            var p = d.nodes[radialPart];
            var pd = Def(p);
            Vector3 hit = p.pos + p.rot * new Vector3(pd.radialSize.x, 0, 0);
            return AttachSurface(d, radialPart, hit, p.rot * Vector3.right, id, 1, false, Quaternion.identity)[0];
        }

        // ------------------------------------------------------------------ subtrees

        public static List<int> Subtree(VesselDesign d, int i)
        {
            var res = new List<int> { i };
            for (int k = 0; k < res.Count; k++)
                for (int j = 0; j < d.nodes.Count; j++)
                    if (d.nodes[j].parent == res[k]) res.Add(j);
            return res;
        }

        /// <summary>Copies a subtree as a standalone list (root first, parents re-indexed, root parent = -1).</summary>
        public static List<PartNode> Extract(VesselDesign d, int i)
        {
            var idx = Subtree(d, i);
            var map = new Dictionary<int, int>();
            for (int k = 0; k < idx.Count; k++) map[idx[k]] = k;
            var res = new List<PartNode>();
            foreach (int j in idx)
            {
                var n = d.nodes[j].Copy();
                n.parent = map.TryGetValue(n.parent, out int np) && j != i ? np : -1;
                res.Add(n);
            }
            res[0].attach = Attach.Root;
            return res;
        }

        /// <summary>Removes parts (whole subtrees) and re-indexes the rest.</summary>
        public static void Remove(VesselDesign d, IEnumerable<int> indices)
        {
            var kill = new HashSet<int>(indices);
            var map = new Dictionary<int, int>();
            var keep = new List<PartNode>();
            for (int i = 0; i < d.nodes.Count; i++)
            {
                if (kill.Contains(i)) continue;
                map[i] = keep.Count;
                keep.Add(d.nodes[i]);
            }
            foreach (var n in keep) n.parent = n.parent >= 0 && map.TryGetValue(n.parent, out int np) ? np : -1;
            d.nodes = keep;
        }

        /// <summary>Inserts a subtree with its root at a pose, in symmetry. Returns the indices of the copies' roots.</summary>
        public static List<int> InsertSymmetric(VesselDesign d, List<PartNode> sub, int parent, Attach attach, Vector3 rootPos, Quaternion rootRot, int symmetry, bool mirror)
        {
            var roots = new List<int>();
            var poses = parent >= 0 ? SymmetricPoses(d.nodes[parent], rootPos, rootRot, symmetry, mirror) : new List<(Vector3, Quaternion)> { (rootPos, rootRot) };
            var symIds = new int[sub.Count];
            int nextSym = NextSym(d);
            for (int j = 0; j < sub.Count; j++) symIds[j] = poses.Count > 1 ? nextSym++ : sub[j].sym;
            foreach (var (pos, rot) in poses)
            {
                Quaternion q = rot * Quaternion.Inverse(sub[0].rot);
                int baseIdx = d.nodes.Count;
                for (int j = 0; j < sub.Count; j++)
                {
                    var n = sub[j].Copy();
                    n.pos = pos + q * (sub[j].pos - sub[0].pos);
                    n.rot = q * sub[j].rot;
                    n.parent = j == 0 ? parent : baseIdx + sub[j].parent;
                    if (j == 0) n.attach = parent < 0 ? Attach.Root : attach;
                    n.sym = symIds[j];
                    d.nodes.Add(n);
                }
                roots.Add(baseIdx);
                if (parent < 0) break;
            }
            for (int i = roots[0]; i < d.nodes.Count; i++) ApplyDefaultGroupsFor(d, i);
            return roots;
        }

        /// <summary>
        /// Milestone 32: everything that goes when part i is deleted or picked up: it and its symmetry copies, with all
        /// that hangs from them, but never the root (index 0). A root that shared a symmetry id with other parts used to
        /// be deleted along with them, which broke the rocket.
        /// </summary>
        public static HashSet<int> RemovalSet(VesselDesign d, int i)
        {
            var kill = new HashSet<int>();
            if (i <= 0 || i >= d.nodes.Count) return kill;
            foreach (int g in SymmetryGroup(d, i))
            {
                if (g == 0) continue;
                foreach (int k in Subtree(d, g)) kill.Add(k);
            }
            kill.Remove(0);
            return kill;
        }

        /// <summary>
        /// Checks a design's tree (for tests): one root at index 0, every other part's parent an earlier-or-later valid
        /// index that leads back to the root without loops. Returns null when it's sound, or what's wrong.
        /// </summary>
        public static string TreeProblem(VesselDesign d)
        {
            int n = d.nodes.Count;
            if (n == 0) return null;
            if (d.nodes[0].parent != -1) return "the first part isn't the root";
            for (int i = 1; i < n; i++)
            {
                int p = d.nodes[i].parent;
                if (p < 0 || p >= n || p == i) return $"part {i} ({d.nodes[i].id}) has a bad parent {p}";
                int steps = 0;
                for (int k = i; k != 0; k = d.nodes[k].parent)
                    if (++steps > n || d.nodes[k].parent < 0) return $"part {i} ({d.nodes[i].id}) doesn't lead back to the root";
            }
            return null;
        }

        public static List<int> SymmetryGroup(VesselDesign d, int i)
        {
            int s = d.nodes[i].sym;
            if (s == 0) return new List<int> { i };
            return Enumerable.Range(0, d.nodes.Count).Where(j => d.nodes[j].sym == s).ToList();
        }

        /// <summary>Moves/rotates a part and its subtree in the part's own frame (so symmetric copies stay symmetric).</summary>
        public static void TransformLocal(VesselDesign d, int i, Vector3 offsetLocal, Quaternion rotLocal)
        {
            var n = d.nodes[i];
            var def = Def(n);
            Vector3 pivot = n.pos + n.rot * def.SurfaceAttachLocal;
            Quaternion world = n.rot * rotLocal * Quaternion.Inverse(n.rot);
            Vector3 move = n.rot * offsetLocal;
            foreach (int j in Subtree(d, i))
            {
                var m = d.nodes[j];
                m.pos = pivot + world * (m.pos - pivot) + move;
                m.rot = world * m.rot;
            }
        }

        /// <summary>Makes another part the root; geometry is unchanged, parent links along the path are reversed.</summary>
        public static void Reroot(VesselDesign d, int newRoot)
        {
            if (newRoot <= 0 || newRoot >= d.nodes.Count) return;
            var path = new List<int>();
            for (int k = newRoot; k >= 0; k = d.nodes[k].parent) path.Add(k);
            var oldParent = path.Select(k => d.nodes[k].parent).ToList();
            var oldAttach = path.Select(k => d.nodes[k].attach).ToList();
            for (int p = 1; p < path.Count; p++)
            {
                var node = d.nodes[path[p]];
                node.parent = path[p - 1];
                var a = oldAttach[p - 1];
                node.attach = a == Attach.Below ? Attach.Above : a == Attach.Above ? Attach.Below : Attach.Surface;
            }
            d.nodes[newRoot].parent = -1;
            d.nodes[newRoot].attach = Attach.Root;
            // Re-order breadth-first from the new root so nodes[0] is the root.
            var order = new List<int> { newRoot };
            for (int k = 0; k < order.Count; k++)
                for (int j = 0; j < d.nodes.Count; j++)
                    if (d.nodes[j].parent == order[k]) order.Add(j);
            var map = new Dictionary<int, int>();
            for (int k = 0; k < order.Count; k++) map[order[k]] = k;
            var nodes = order.Select(k => d.nodes[k]).ToList();
            foreach (var n in nodes) n.parent = n.parent >= 0 ? map[n.parent] : -1;
            d.nodes = nodes;
        }

        // ------------------------------------------------------------------ analysis helpers

        public static Vector3 CenterOfMass(VesselDesign d)
        {
            Vector3 sum = Vector3.zero;
            float m = 0;
            foreach (var n in d.nodes)
            {
                var def = Def(n);
                if (def == null) continue;
                sum += Center(n, def) * def.WetMass;
                m += def.WetMass;
            }
            return m > 0 ? sum / m : Vector3.zero;
        }

        /// <summary>How far the liftoff thrust line misses the centre of mass (m).</summary>
        public static float ThrustOffset(VesselDesign d)
        {
            var stages = d.Stages();
            var first = stages.Find(s => s.ignite.Count > 0);
            if (first == null) return 0;
            Vector3 com = CenterOfMass(d), torque = Vector3.zero, force = Vector3.zero;
            foreach (int i in first.ignite)
            {
                var n = d.nodes[i];
                var def = Def(n);
                Vector3 f = Axis(n) * def.ThrustAsl;
                force += f;
                torque += Vector3.Cross(BottomNode(n, def) - com, f);
            }
            return force.magnitude > 0 ? torque.magnitude / force.magnitude : 0;
        }
    }
}
