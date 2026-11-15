using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rocket
{
    /// <summary>
    /// The GameObjects for one vessel (or design): one child per part placed at its vessel-frame pose, plus engine
    /// and tower flames, parachute canopies, animated leg struts, spotlights, heat glow and reentry plasma.
    /// </summary>
    public class VesselView
    {
        public GameObject root;
        readonly Dictionary<int, GameObject> partGo = new Dictionary<int, GameObject>();
        readonly Dictionary<int, Transform> flames = new Dictionary<int, Transform>();
        readonly Dictionary<int, MeshRenderer> plumeRenderers = new Dictionary<int, MeshRenderer>();
        readonly Dictionary<int, ParticleSystem> smokes = new Dictionary<int, ParticleSystem>();
        readonly Dictionary<int, Transform[]> ardlingLimbs = new Dictionary<int, Transform[]>();
        float walkPhase, walkAmp, floatAmp, runAmp, airAmp, swimAmp, swimMove, strokePhase;
        // milestone 42: more body language (crouching, carrying, aiming, knocked out, a look round now and then, landing)
        float crouchAmp, carryAmp, aimAmp, downAmp, lookYaw, lookTarget, lookTimer, landSquash;
        bool wasAir;
        string heldShown;
        /// <summary>Milestone 42: what the game knows about an Ardling (by name): knocked out, carrying something, the weapon in hand, seconds since they last struck.</summary>
        public static System.Func<string, bool> ArdlingDown, ArdlingCarrying;
        public static System.Func<string, string> ArdlingHolds;
        public static System.Func<string, float> ArdlingStruck;

        /// <summary>
        /// The Ardling's body language (milestone 35: richer). Standing, they breathe and sway a little; walking, arms and
        /// legs swing with a bob in each step; running, bigger strides leaning forward; in the air near the ground (a jump),
        /// arms up and knees bent; in space, limbs drifting in a float; in the sea, treading water upright with arms sculling,
        /// or swimming front crawl, body flat, arms turning over and legs kicking, head just above the water.
        /// </summary>
        void AnimateArdling(VesselSim sim, PartState p, Transform[] limbs)
        {
            float dt = Mathf.Min(UnityEngine.Time.deltaTime, 0.05f), t = UnityEngine.Time.time;
            bool ground = sim.landed, water = ground && sim.InWater;
            string who = p.crew.Count > 0 ? p.crew[0] : null;
            bool down = who != null && ArdlingDown != null && ArdlingDown(who);
            bool carrying = who != null && ArdlingCarrying != null && ArdlingCarrying(who);
            string holds = who != null && ArdlingHolds != null ? ArdlingHolds(who) : null;
            float struck = who != null && ArdlingStruck != null ? ArdlingStruck(who) : 99f;
            ShowHeld(limbs[1], holds);
            float fwd = ground ? Mathf.Clamp(sim.inputPitch, -1f, 1f) : 0f, strafe = ground ? Mathf.Clamp(sim.evaStrafe, -1f, 1f) : 0f;
            float move = Mathf.Max(Mathf.Max(Mathf.Abs(fwd), Mathf.Abs(strafe)), ground ? Mathf.Abs(sim.inputYaw) * 0.5f : 0f);
            bool nearGround = !ground && sim.Altitude < 3000 && sim.body.SurfaceGravity > 0.2;
            walkAmp = Mathf.MoveTowards(walkAmp, ground && !water && move > 0.02f ? 1f : 0f, dt * 5f);
            runAmp = Mathf.MoveTowards(runAmp, ground && !water && sim.evaRun && move > 0.1f ? 1f : 0f, dt * 4f);
            airAmp = Mathf.MoveTowards(airAmp, nearGround ? 1f : 0f, dt * 6f);
            floatAmp = Mathf.MoveTowards(floatAmp, !ground && !nearGround ? 1f : 0f, dt * 2f);
            swimAmp = Mathf.MoveTowards(swimAmp, water ? 1f : 0f, dt * 3f);
            swimMove = Mathf.MoveTowards(swimMove, water && move > 0.05f ? 1f : 0f, dt * 2.5f);
            crouchAmp = Mathf.MoveTowards(crouchAmp, ground && !water && sim.evaCrouch && !down ? 1f : 0f, dt * 5f);
            carryAmp = Mathf.MoveTowards(carryAmp, carrying && !down ? 1f : 0f, dt * 5f);
            bool rangedHeld = holds == "zapper" || holds == "blaster" || holds == "boltrifle";
            aimAmp = Mathf.MoveTowards(aimAmp, rangedHeld && ground && !water && !carrying && !down ? 1f : 0f, dt * 4f);
            downAmp = Mathf.MoveTowards(downAmp, down && ground ? 1f : 0f, dt * 2.5f);
            // landing from a jump: a quick squash
            bool air = !ground && airAmp > 0.5f;
            if (wasAir && ground) landSquash = 1f;
            wasAir = air;
            landSquash = Mathf.MoveTowards(landSquash, 0f, dt * 4f);
            // standing still a while: a look round now and then
            lookTimer -= dt;
            if (lookTimer <= 0) { lookTimer = 3f + Mathf.Abs(Mathf.Sin(t * 7.3f + p.index)) * 5f; lookTarget = walkAmp < 0.1f && !down ? Mathf.Sin(t * 3.1f) * 28f : 0f; }
            if (walkAmp > 0.2f) lookTarget = 0f;
            lookYaw = Mathf.MoveTowards(lookYaw, lookTarget, dt * 60f);
            walkPhase += dt * (8f + 5f * runAmp) * Mathf.Max(move, 0.35f) * (fwd < 0 ? -1f : 1f);
            strokePhase += dt * (2.6f + 1.4f * (sim.evaRun ? 1f : 0f)) * Mathf.Max(move, 0.3f);
            float swing = Mathf.Sin(walkPhase) * (34f + 16f * runAmp) * walkAmp;
            float breathe = Mathf.Sin(t * 1.7f) * 2.5f;
            float drift = Mathf.Sin(t * 1.2f), drift2 = Mathf.Sin(t * 0.9f + 1.3f);
            for (int k = 0; k < 4; k++)
            {
                int side = k % 2 == 0 ? -1 : 1;
                bool arm = k < 2;
                Quaternion standing = arm
                    ? Quaternion.Euler(swing * side * 0.8f + breathe * 0.3f - 12f * runAmp, 0, side * (5f + breathe * 0.4f))
                    : Quaternion.Euler(-swing * side, 0, 0);
                Quaternion jumping = arm ? Quaternion.Euler(-35f, 0, side * 55f) : Quaternion.Euler(28f - 10f * side * Mathf.Sin(t * 3f), 0, side * 6f);
                Quaternion floating = arm
                    ? Quaternion.Euler(-20f + 10f * drift * side, 0, side * (32f + 6f * drift2))
                    : Quaternion.Euler(18f + 8f * drift2 * side, 0, side * 6f);
                // in the sea: treading (arms sculling out to the sides, slow kicks) or the crawl (arms turning over, quick kicks)
                Quaternion tread = arm ? Quaternion.Euler(-60f, 0, side * (42f + 18f * Mathf.Sin(t * 2.4f + side))) : Quaternion.Euler(16f * Mathf.Sin(t * 2f + side), 0, side * 8f);
                Quaternion crawl = arm ? Quaternion.Euler(-(strokePhase * Mathf.Rad2Deg + (side > 0 ? 180f : 0f)), 0, side * 12f) : Quaternion.Euler(22f * Mathf.Sin(strokePhase * 2.4f + (side > 0 ? Mathf.PI : 0f)), 0, side * 4f);
                Quaternion swimming = Quaternion.Slerp(tread, crawl, swimMove);
                var q = Quaternion.Slerp(standing, jumping, airAmp);
                q = Quaternion.Slerp(q, floating, floatAmp);
                q = Quaternion.Slerp(q, swimming, swimAmp);
                // milestone 42: crouching (knees forward, arms ready), carrying (both arms out in front), aiming (the gun arm up
                // and steady), striking (a swing or a kick of recoil), knocked out (flat out, arms wide)
                Quaternion crouched = arm ? Quaternion.Euler(-30f + swing * side * 0.4f, 0, side * 14f) : Quaternion.Euler(-38f - swing * side * 0.5f, 0, side * 4f);
                q = Quaternion.Slerp(q, crouched, crouchAmp);
                if (arm) q = Quaternion.Slerp(q, Quaternion.Euler(-62f, 0, side * -10f), carryAmp);
                if (arm && side > 0) q = Quaternion.Slerp(q, Quaternion.Euler(-82f, 0, -4f), aimAmp);
                if (arm && side < 0) q = Quaternion.Slerp(q, Quaternion.Euler(-30f, 0, 18f), aimAmp * 0.6f);   // (the other hand steadies it)
                if (arm && side > 0 && struck < 0.35f && !down)
                {
                    float f = struck / 0.35f;
                    q = rangedHeld ? q * Quaternion.Euler(-14f * (1f - f), 0, 0)                                         // recoil
                                   : Quaternion.Euler(Mathf.Lerp(-130f, 25f, Mathf.SmoothStep(0, 1, f)), 0, -12f);       // a swing
                }
                if (!arm && side > 0 && struck < 0.35f && holds == null && !down) q = Quaternion.Euler(-70f * Mathf.Sin(struck / 0.35f * Mathf.PI), 0, 0);   // a kick
                Quaternion flat = arm ? Quaternion.Euler(-10f, 0, side * 75f) : Quaternion.Euler(4f, 0, side * 12f);
                q = Quaternion.Slerp(q, flat, downAmp);
                limbs[k].localRotation = q;
            }
            // the whole body: a bob in each step, leaning into a run, flat and low in the water when swimming
            if (partGo.TryGetValue(p.index, out var go))
            {
                float stepBob = Mathf.Abs(Mathf.Sin(walkPhase)) * (0.03f + 0.03f * runAmp) * walkAmp * (1f - 0.5f * crouchAmp);
                float waves = Mathf.Sin(t * 1.3f) * 0.05f * swimAmp;
                float sink = Mathf.Lerp(0.72f, 0.42f, swimMove) * swimAmp + 0.13f * crouchAmp + 0.07f * landSquash + 0.78f * downAmp;
                float lean = 9f * runAmp * (fwd < 0 ? -1f : 1f) + 72f * swimMove * swimAmp + 16f * crouchAmp + 6f * carryAmp * (1f - crouchAmp) - 4f * aimAmp;
                lean = Mathf.Lerp(lean, -88f, downAmp);   // (on their back)
                float sway = Mathf.Sin(t * 0.8f) * 1.2f * (1f - walkAmp) * (1f - swimAmp) + Mathf.Sin(strokePhase) * 8f * swimMove;
                go.transform.localPosition = p.localPos + p.localRot * new Vector3(0, stepBob + waves - sink, 0);
                go.transform.localRotation = p.localRot * Quaternion.Euler(lean, lookYaw * (1f - downAmp), sway * (1f - downAmp));
            }
        }
        /// <summary>Milestone 42: the weapon in an Ardling's right hand (built along the arm, so raising the arm aims it).</summary>
        void ShowHeld(Transform rightArm, string id)
        {
            if (rightArm == null || id == heldShown) return;
            heldShown = id;
            var old = rightArm.Find("Held");
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            if (id == null) return;
            var mesh = HeldMesh(id);
            if (mesh == null) return;
            var g = new GameObject("Held");
            g.transform.SetParent(rightArm, false);
            g.transform.localPosition = new Vector3(0.074f, -0.235f, 0.04f);   // (in the glove)
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mats.Lit;
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        static readonly Dictionary<string, Mesh> heldMeshes = new Dictionary<string, Mesh>();

        /// <summary>The weapons as models: the grip at the origin, the business end down the arm (-Y).</summary>
        public static Mesh HeldMesh(string id)
        {
            if (heldMeshes.TryGetValue(id, out var m)) return m;
            var b = new MeshBuilder { Mat = MeshBuilder.MatPaint };
            Color metal = new Color(0.62f, 0.64f, 0.68f), dark = new Color(0.16f, 0.17f, 0.19f);
            Quaternion I = Quaternion.identity;
            switch (id)
            {
                case "wrench":
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddBox(new Vector3(0, -0.12f, 0), new Vector3(0.022f, 0.26f, 0.012f), metal, I);
                    b.AddBox(new Vector3(0, -0.26f, 0), new Vector3(0.07f, 0.04f, 0.016f), metal, I);
                    b.AddBox(new Vector3(0.022f, -0.29f, 0), new Vector3(0.02f, 0.04f, 0.016f), metal, I);
                    b.AddBox(new Vector3(-0.022f, -0.29f, 0), new Vector3(0.02f, 0.04f, 0.016f), metal, I);
                    b.Mat = MeshBuilder.MatRubber;
                    b.AddBox(new Vector3(0, -0.02f, 0), new Vector3(0.03f, 0.08f, 0.02f), new Color(0.75f, 0.2f, 0.15f), I);
                    break;
                case "baton":
                    b.Mat = MeshBuilder.MatRubber;
                    b.AddFrustum(0.04f, -0.08f, 0.016f, 0.016f, dark, 10, true, true);
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddFrustum(-0.08f, -0.3f, 0.012f, 0.012f, new Color(0.3f, 0.35f, 0.42f), 10, false, false);
                    b.AddFrustum(-0.3f, -0.34f, 0.018f, 0.018f, new Color(0.35f, 0.75f, 1f), 10, false, true);
                    break;
                case "zapper":
                    b.AddFrustum(0.03f, -0.2f, 0.024f, 0.022f, new Color(0.3f, 0.7f, 0.95f), 12, true, false);
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddFrustum(-0.2f, -0.24f, 0.03f, 0.016f, metal, 12, false, true);
                    b.AddBox(new Vector3(0, -0.03f, 0.03f), new Vector3(0.02f, 0.05f, 0.03f), dark, I);
                    break;
                case "blaster":
                    b.AddBox(new Vector3(0, -0.09f, 0.02f), new Vector3(0.034f, 0.16f, 0.05f), new Color(0.95f, 0.5f, 0.2f), I);
                    b.AddBox(new Vector3(0, 0.0f, -0.02f), new Vector3(0.028f, 0.05f, 0.06f), dark, I);    // the grip
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddFrustum(-0.17f, -0.24f, 0.012f, 0.012f, metal, 10, false, true);                   // the barrel
                    b.AddBox(new Vector3(0, -0.1f, 0.05f), new Vector3(0.012f, 0.06f, 0.012f), new Color(1f, 0.85f, 0.3f), I);
                    break;
                case "boltrifle":
                    b.AddBox(new Vector3(0, -0.15f, 0.02f), new Vector3(0.036f, 0.3f, 0.052f), new Color(0.3f, 0.36f, 0.3f), I);
                    b.AddBox(new Vector3(0, 0.06f, 0.0f), new Vector3(0.03f, 0.12f, 0.07f), dark, I);       // the stock
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddFrustum(-0.3f, -0.5f, 0.014f, 0.012f, metal, 10, false, true);                     // the long barrel
                    b.AddFrustum(-0.08f, -0.2f, 0.016f, 0.016f, dark, 10, true, true);                      // the scope
                    b.AddBox(new Vector3(0, -0.2f, 0.05f), new Vector3(0.012f, 0.1f, 0.012f), new Color(0.6f, 0.95f, 0.35f), I);
                    break;
                default: return null;
            }
            m = b.ToMesh("Held " + id);
            heldMeshes[id] = m;
            return m;
        }

        static readonly Dictionary<string, Material> plumeMats = new Dictionary<string, Material>();

        /// <summary>
        /// Exhaust look by engine type: solid boosters burn bright white-orange, kerosene lifters orange-yellow with shock
        /// diamonds, high-efficiency vacuum engines a faint blue-violet, jets and the escape tower their own.
        /// </summary>
        static Material PlumeMaterial(PartDef def)
        {
            string kind = def.kind == PartKind.EscapeTower ? "tower" : def.kind == PartKind.Solid ? "solid" : def.IsJet ? "jet" : def.ispVac > 600 ? "ion" : def.ispAsl < def.ispVac * 0.6f ? "vac" : "lifter";
            if (plumeMats.TryGetValue(kind, out var m)) return m;
            m = new Material(Mats.Plume) { name = "Plume " + kind };
            switch (kind)
            {
                case "solid": m.SetColor("_CoreColor", new Color(1f, 0.95f, 0.85f)); m.SetColor("_EdgeColor", new Color(1f, 0.55f, 0.2f)); m.SetFloat("_Intensity", 6f); m.SetFloat("_Diamonds", 0.25f); break;
                case "vac": m.SetColor("_CoreColor", new Color(0.85f, 0.85f, 1f)); m.SetColor("_EdgeColor", new Color(0.45f, 0.5f, 1f)); m.SetFloat("_Intensity", 3.2f); m.SetFloat("_Diamonds", 0.5f); break;
                case "ion": m.SetColor("_CoreColor", new Color(0.6f, 0.8f, 1f)); m.SetColor("_EdgeColor", new Color(0.3f, 0.5f, 1f)); m.SetFloat("_Intensity", 2.5f); m.SetFloat("_Diamonds", 0f); break;
                case "jet": m.SetColor("_CoreColor", new Color(1f, 0.7f, 0.5f)); m.SetColor("_EdgeColor", new Color(0.6f, 0.35f, 0.9f)); m.SetFloat("_Intensity", 1.6f); m.SetFloat("_Diamonds", 0.8f); break;
                case "tower": m.SetColor("_CoreColor", new Color(1f, 0.9f, 0.75f)); m.SetColor("_EdgeColor", new Color(1f, 0.5f, 0.2f)); m.SetFloat("_Intensity", 5f); m.SetFloat("_Diamonds", 0f); break;
                default: m.SetColor("_CoreColor", new Color(1f, 0.88f, 0.62f)); m.SetColor("_EdgeColor", new Color(1f, 0.48f, 0.14f)); m.SetFloat("_Intensity", 4.5f); m.SetFloat("_Diamonds", 0.7f); break;
            }
            plumeMats[kind] = m;
            return m;
        }
        readonly Dictionary<int, Transform> canopies = new Dictionary<int, Transform>();
        readonly Dictionary<int, Transform> struts = new Dictionary<int, Transform>();
        readonly Dictionary<int, Light> lights = new Dictionary<int, Light>();
        readonly Dictionary<int, Transform> flaps = new Dictionary<int, Transform>();
        readonly Dictionary<int, Transform> gearStruts = new Dictionary<int, Transform>();
        readonly Dictionary<int, Transform> wheels = new Dictionary<int, Transform>();
        readonly Dictionary<int, Transform> tyres = new Dictionary<int, Transform>(); // rover wheels
        readonly Dictionary<int, Transform> deployables = new Dictionary<int, Transform>();
        readonly Dictionary<int, Light> engineLights = new Dictionary<int, Light>();
        readonly Dictionary<int, GameObject> shells = new Dictionary<int, GameObject>();
        bool flight;
        readonly Dictionary<int, float> glowShown = new Dictionary<int, float>();
        public readonly Dictionary<Collider, int> colliderToIndex = new Dictionary<Collider, int>();
        readonly HashSet<int> present = new HashSet<int>();
        GameObject plasma;
        MaterialPropertyBlock mpb;

        public static VesselView Create(VesselSim sim)
        {
            var vv = new VesselView { root = new GameObject("Vessel " + sim.name), flight = true };
            foreach (var p in sim.parts) { vv.AddPart(p.def, p.index, p.localPos, p.localRot, p.variant, false, p.crew.Count > 0 ? p.crew[0] : null); vv.AddDecal(p.index, p.decal); }
            return vv;
        }

        public static VesselView FromNodes(IList<PartNode> nodes, string name, bool colliders)
        {
            var vv = new VesselView { root = new GameObject(name) };
            for (int i = 0; i < nodes.Count; i++)
            {
                var def = PartCatalog.Get(nodes[i].id);
                if (def != null) { vv.AddPart(def, i, nodes[i].pos, nodes[i].rot, nodes[i].variant, colliders); vv.AddDecal(i, nodes[i].decal); }
            }
            return vv;
        }

        /// <summary>Milestone 34: a part's decal, if it has one (stuck on its GameObject, so it moves with it).</summary>
        public void AddDecal(int index, string decal)
        {
            if (string.IsNullOrEmpty(decal) || !partGo.TryGetValue(index, out var go) || go == null) return;
            Decals.Attach(go.transform, decal, go.layer);
        }

        /// <summary>A part's GameObject (the builder works out where on it a decal goes).</summary>
        public Transform PartTransform(int index) => partGo.TryGetValue(index, out var go) && go != null ? go.transform : null;

        void AddPart(PartDef def, int index, Vector3 pos, Quaternion rot, int variant, bool collider, string crewName = null)
        {
            var go = new GameObject(def.name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            partGo[index] = go;
            var mesh = MeshFactory.PartMesh(def, variant);
            Ardlings.Look look = default;
            if (def.kind == PartKind.Eva)
            {
                look = Ardlings.LookFor(crewName ?? "Ardling");
                mesh = Ardlings.Body(look, false);   // arms and legs are their own pieces, so they can swing
            }
            // Control surfaces carry their mesh on a child that pivots about the span axis.
            var meshGo = go;
            if (def.controlSurface)
            {
                meshGo = new GameObject("Flap");
                meshGo.transform.SetParent(go.transform, false);
                flaps[index] = meshGo.transform;
            }
            meshGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = meshGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            renderers[index] = mr;
            if (def.kind == PartKind.Eva)
            {
                Ardlings.AddNote(go.transform, look);
                Mesh[] lm = { Ardlings.Arm(look, -1), Ardlings.Arm(look, 1), Ardlings.Leg(look, -1), Ardlings.Leg(look, 1) };
                Vector3[] piv = { Ardlings.ShoulderL, Ardlings.ShoulderR, Ardlings.HipL, Ardlings.HipR };
                var limbs = new Transform[4];
                for (int k = 0; k < 4; k++)
                {
                    var lg = new GameObject(k < 2 ? "Arm" : "Leg");
                    lg.transform.SetParent(go.transform, false);
                    lg.transform.localPosition = piv[k];
                    lg.AddComponent<MeshFilter>().sharedMesh = lm[k];
                    var lr = lg.AddComponent<MeshRenderer>();
                    lr.sharedMaterial = Mats.Lit;
                    lr.shadowCastingMode = ShadowCastingMode.On;
                    limbs[k] = lg.transform;
                }
                ardlingLimbs[index] = limbs;
            }
            if (collider)
            {
                var mc = meshGo.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                colliderToIndex[mc] = index;
            }
            if (def.kind == PartKind.Wheel)
            {
                var ty = new GameObject("Tyre");
                ty.transform.SetParent(go.transform, false);
                ty.transform.localPosition = new Vector3(def.strutLength, 0, 0);
                ty.AddComponent<MeshFilter>().sharedMesh = MeshFactory.RoverTyre(def.wheelRadius, def.radialSize.x);
                var tr = ty.AddComponent<MeshRenderer>();
                tr.sharedMaterial = Mats.Lit;
                tr.shadowCastingMode = ShadowCastingMode.Off;
                tyres[index] = ty.transform;
            }
            if (def.kind == PartKind.Gear)
            {
                var s = new GameObject("Strut");
                s.transform.SetParent(go.transform, false);
                s.AddComponent<MeshFilter>().sharedMesh = MeshFactory.GearStrut();
                s.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                gearStruts[index] = s.transform;
                var w = new GameObject("Wheel");
                w.transform.SetParent(go.transform, false);
                w.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Wheel(def.wheelRadius);
                w.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                wheels[index] = w.transform;
                SetGear(index, def, 1f, 0f);
            }
            if (def.IsDeployable)
            {
                var dg = new GameObject("Deploy");
                dg.transform.SetParent(go.transform, false);
                // Drill bits come down out of the bottom of the housing; everything else unfolds outward.
                dg.transform.localPosition = def.kind == PartKind.Drill ? new Vector3(def.radialSize.x * 0.5f, -def.radialSize.y * 0.5f, 0) : new Vector3(def.radialSize.x, 0, 0);
                dg.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Deployable(def);
                var dr = dg.AddComponent<MeshRenderer>();
                dr.sharedMaterial = Mats.Lit;
                dr.shadowCastingMode = ShadowCastingMode.Off;
                deployables[index] = dg.transform;
                renderersExtra[index] = dr;
                SetDeploy(dg.transform, def, flight ? 0f : 1f); // the builder shows it unfolded so you can see its size
            }
            if (def.kind == PartKind.Fairing && flight)
            {
                var sh = new GameObject("Shell");
                sh.transform.SetParent(go.transform, false);
                sh.AddComponent<MeshFilter>().sharedMesh = MeshFactory.FairingShell(def, variant);
                var shr = sh.AddComponent<MeshRenderer>();
                shr.sharedMaterial = Mats.Lit;
                shr.shadowCastingMode = ShadowCastingMode.Off;
                shells[index] = sh;
            }

            if (def.IsEngine || def.kind == PartKind.EscapeTower)
            {
                var f = new GameObject("Flame");
                f.transform.SetParent(go.transform, false);
                if (def.kind == PartKind.EscapeTower)
                {
                    f.transform.localPosition = new Vector3(0, -1.9f, 0);
                    f.transform.localRotation = Quaternion.identity;
                }
                else f.transform.localPosition = new Vector3(0, -def.length + 0.05f, 0);
                f.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Flame();
                var fr = f.AddComponent<MeshRenderer>();
                fr.sharedMaterial = PlumeMaterial(def);
                fr.shadowCastingMode = ShadowCastingMode.Off;
                plumeRenderers[index] = fr;
                f.SetActive(false);
                flames[index] = f.transform;
                if (flight && Smoke.Makes(def)) smokes[index] = Smoke.Create(go.transform, def);
                if (flight && def.IsEngine && !def.IsJet)
                {
                    // The exhaust lights up the ground and the rocket.
                    var lg = new GameObject("EngineLight");
                    lg.transform.SetParent(go.transform, false);
                    lg.transform.localPosition = new Vector3(0, -def.length - 1.2f - def.bottomDiameter, 0);
                    var el = lg.AddComponent<Light>();
                    el.type = LightType.Point;
                    el.color = new Color(1f, 0.68f, 0.38f);
                    el.range = 14f + 22f * def.bottomDiameter;
                    el.shadows = LightShadows.None;
                    el.enabled = false;
                    engineLights[index] = el;
                }
            }
            if (def.kind == PartKind.Parachute)
            {
                var c = new GameObject("Canopy");
                c.transform.SetParent(go.transform, false);
                c.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Canopy();
                var cr = c.AddComponent<MeshRenderer>();
                cr.sharedMaterial = Mats.Lit;
                cr.shadowCastingMode = ShadowCastingMode.Off;
                c.SetActive(false);
                canopies[index] = c.transform;
            }
            if (def.kind == PartKind.Leg)
            {
                var s = new GameObject("Strut");
                s.transform.SetParent(go.transform, false);
                s.AddComponent<MeshFilter>().sharedMesh = MeshFactory.LegStrut();
                var sr = s.AddComponent<MeshRenderer>();
                sr.sharedMaterial = Mats.Lit;
                sr.shadowCastingMode = ShadowCastingMode.Off;
                struts[index] = s.transform;
                SetLeg(index, def, 0f);
            }
            if (def.kind == PartKind.Light)
            {
                var lg = new GameObject("Spot");
                lg.transform.SetParent(go.transform, false);
                lg.transform.localPosition = new Vector3(def.radialSize.x, 0, 0);
                lg.transform.localRotation = Quaternion.LookRotation(new Vector3(1f, -0.45f, 0f), Vector3.up); // out and a little down: lights the ground around a lander
                var l = lg.AddComponent<Light>();
                l.type = LightType.Spot;
                l.range = 45f;
                l.spotAngle = 70f;
                l.intensity = 6f;
                l.color = def.lightColor;
                l.enabled = false;
                lights[index] = l;
            }
        }

        readonly Dictionary<int, MeshRenderer> renderers = new Dictionary<int, MeshRenderer>();

        void SetGear(int index, PartDef def, float k, float steer)
        {
            if (!gearStruts.TryGetValue(index, out var s)) return;
            Vector3 hub = Vector3.Lerp(new Vector3(0.15f, 0, 0), new Vector3(def.strutLength, 0, 0), k);
            hub = Vector3.Lerp(new Vector3(0.05f, -0.4f, 0), hub, k); // swings back toward the tail when retracted
            s.localRotation = Quaternion.FromToRotation(Vector3.down, hub.normalized);
            s.localScale = new Vector3(1f, hub.magnitude, 1f);
            var w = wheels[index];
            w.localPosition = hub;
            w.localRotation = Quaternion.AngleAxis(steer, Vector3.right);
            w.gameObject.SetActive(k > 0.05f);
        }

        static readonly Vector3 LegPivotFrac = new Vector3(0.7f, 0.3f, 0f);

        readonly Dictionary<int, MeshRenderer> renderersExtra = new Dictionary<int, MeshRenderer>();

        static void SetDeploy(Transform t, PartDef def, float k)
        {
            bool on = k > 0.02f;
            if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
            if (on) t.localScale = def.kind == PartKind.Drill ? new Vector3(1f, k, 1f) : new Vector3(k, 1f, 1f);
        }

        void SetLeg(int index, PartDef def, float k)
        {
            if (!struts.TryGetValue(index, out var s)) return;
            Vector3 rs = def.radialSize;
            Vector3 pivot = new Vector3(rs.x * LegPivotFrac.x, rs.y * LegPivotFrac.y, 0);
            Vector3 foot = Vector3.Lerp(new Vector3(rs.x, -rs.y * 0.5f, 0), new Vector3(def.legLength * 0.5f, -def.legLength, 0), k);
            Vector3 d = foot - pivot;
            s.localPosition = pivot;
            s.localRotation = Quaternion.FromToRotation(Vector3.down, d.normalized);
            s.localScale = new Vector3(1f, d.magnitude, 1f);
        }

        /// <param name="worldPos">Vessel centre of mass in render (floating-origin) space.</param>
        public void Sync(VesselSim sim, Vector3 worldPos, Quaternion renderRot, double pressure, bool thermalView)
        {
            root.transform.SetPositionAndRotation(worldPos - renderRot * sim.com, renderRot);
            float vac = 1f - Mathf.Clamp01((float)pressure);

            present.Clear();
            foreach (var p in sim.parts) present.Add(p.index);
            foreach (var kv in partGo)
                if (!present.Contains(kv.Key) && kv.Value.activeSelf) kv.Value.SetActive(false);

            if (mpb == null) mpb = new MaterialPropertyBlock();
            foreach (var p in sim.parts)
            {
                if (flames.TryGetValue(p.index, out var f))
                {
                    bool on = p.thrustFrac > 0.001f && !sim.destroyed;
                    if (f.gameObject.activeSelf != on) f.gameObject.SetActive(on);
                    if (on)
                    {
                        float flicker = 0.93f + 0.07f * Mathf.PerlinNoise(UnityEngine.Time.time * 25f, p.index * 3.1f);
                        if (p.def.kind == PartKind.EscapeTower) f.localScale = new Vector3(0.5f, 3.5f * flicker, 0.5f);
                        else
                        {
                            // The shader widens the plume in thin air; here: nozzle-sized, longer with throttle
                            float r = p.def.bottomDiameter * 0.42f;
                            float len = (p.def.bottomDiameter * 5f + 5f) * (0.4f + 0.6f * p.thrustFrac) * (1f + vac * 0.8f) * flicker;
                            f.localScale = new Vector3(r, len, r);
                        }
                        if (plumeRenderers.TryGetValue(p.index, out var pr))
                        {
                            mpb.Clear();
                            mpb.SetFloat("_Expand", Mathf.SmoothStep(0f, 1f, vac));
                            mpb.SetFloat("_Seed", p.index * 1.37f);
                            mpb.SetFloat("_Intensity", pr.sharedMaterial.GetFloat("_Intensity") * (0.45f + 0.55f * p.thrustFrac));
                            pr.SetPropertyBlock(mpb);
                        }
                    }
                    if (smokes.TryGetValue(p.index, out var sm)) Smoke.Drive(sm, p.def, on ? p.thrustFrac : 0f, (float)pressure);
                    if (engineLights.TryGetValue(p.index, out var el))
                    {
                        if (el.enabled != on) el.enabled = on;
                        if (on) el.intensity = (1.2f + 3f * p.thrustFrac) * (0.88f + 0.12f * Mathf.PerlinNoise(UnityEngine.Time.time * 30f, p.index * 1.7f));
                    }
                }
                if (canopies.TryGetValue(p.index, out var c))
                {
                    bool on = p.chuteDeployed && !p.chuteFailed && p.chuteOpen > 0.01f;
                    if (c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
                    if (on)
                    {
                        float size = Mathf.Sqrt(p.def.chuteCdA / 1.3f / Mathf.PI) * Mathf.Sqrt(p.chuteOpen);
                        c.localScale = new Vector3(size, size * 0.8f, size);
                        c.localPosition = new Vector3(0, 1.5f + size * 1.3f, 0);
                    }
                }
                if (struts.ContainsKey(p.index)) SetLeg(p.index, p.def, p.legDeploy);
                if (gearStruts.ContainsKey(p.index)) SetGear(p.index, p.def, p.legDeploy, p.steer);
                if (tyres.TryGetValue(p.index, out var tyre))
                {
                    // Steer about the vessel's up (+Y), roll about its sideways axis (+X): the same for wheels on both sides.
                    Quaternion inv = Quaternion.Inverse(p.localRot);
                    tyre.localRotation = Quaternion.AngleAxis(p.steer, inv * Vector3.up) * Quaternion.AngleAxis(p.spin, inv * Vector3.right);
                }
                if (deployables.TryGetValue(p.index, out var dep))
                {
                    SetDeploy(dep, p.def, p.legDeploy);
                    if (p.def.kind == PartKind.Drill && p.load > 0.01f) dep.localRotation = Quaternion.Euler(0, UnityEngine.Time.time * 300f * p.load % 360f, 0);
                    if (p.def.kind == PartKind.Solar && p.legDeploy > 0.02f)
                    {
                        // Panels pivot on their arm to face the sun (the sim counts the same).
                        Vector3 sunL = Quaternion.Inverse(renderRot * p.localRot) * GameRoot.SunNow;
                        dep.localRotation = Quaternion.AngleAxis(-Mathf.Atan2(sunL.y, sunL.z) * Mathf.Rad2Deg, Vector3.right);
                    }
                }
                if (ardlingLimbs.TryGetValue(p.index, out var limbsT)) AnimateArdling(sim, p, limbsT);
                if (flaps.TryGetValue(p.index, out var flap)) flap.localRotation = Quaternion.AngleAxis(p.deflect, Vector3.right);
                if (shells.TryGetValue(p.index, out var shell) && shell.activeSelf == p.fairingGone) shell.SetActive(!p.fairingGone);
                if (lights.TryGetValue(p.index, out var l) && l.enabled != p.lightOn) l.enabled = p.lightOn;

                float glow = thermalView ? Mathf.Clamp01(p.HeatFraction) : Mathf.Clamp01(((float)p.temp - 850f) / 1100f);
                if (p.def.kind == PartKind.Light && p.lightOn && !thermalView) glow = Mathf.Max(glow, 0.6f);
                // Radiators and machines glow as the mining core heats up.
                if (!thermalView && (p.def.coolRate > 0 || p.def.IsMachine))
                {
                    float core = Mathf.Clamp01(((float)sim.coreTemp - (p.def.coolRate > 0 ? 480f : 650f)) / 600f) * 0.75f;
                    glow = Mathf.Max(glow, core);
                    if (renderersExtra.TryGetValue(p.index, out var er) && er != null)
                    {
                        float shownE = Mathf.Round(core * 40f) / 40f;
                        if (shownE <= 0) er.SetPropertyBlock(null);
                        else { mpb.Clear(); mpb.SetColor("_Tint", Color.Lerp(Color.white, new Color(1f, 0.45f, 0.2f), shownE)); mpb.SetFloat("_Ambient", 0.24f); mpb.SetFloat("_Emission", shownE * 0.9f); er.SetPropertyBlock(mpb); }
                    }
                }
                glow = Mathf.Round(glow * 40f) / 40f;
                float charShown = Mathf.Round(p.charred * 40f) / 40f;
                float key = glow + charShown * 100f;   // redo the block when either the glow or the charring changes
                if (!glowShown.TryGetValue(p.index, out float shown) || shown != key || thermalView)
                {
                    glowShown[p.index] = key;
                    if (renderers.TryGetValue(p.index, out var mr) && mr != null)
                    {
                        if (glow <= 0 && charShown <= 0 && !thermalView) mr.SetPropertyBlock(null);
                        else
                        {
                            mpb.Clear();
                            Color tint = thermalView ? HeatColor(p.HeatFraction) : Color.Lerp(Color.white, new Color(1f, 0.45f, 0.2f), glow);
                            if (p.def.kind == PartKind.Light && p.lightOn) tint = Color.white;
                            mpb.SetColor("_Tint", tint);
                            mpb.SetFloat("_Ambient", thermalView ? 0.6f : 0.24f);
                            mpb.SetFloat("_Emission", thermalView ? 0.4f : glow * 0.9f);
                            mpb.SetFloat("_Char", thermalView ? 0f : charShown);
                            mr.SetPropertyBlock(mpb);
                        }
                    }
                }
            }
            UpdatePlasma(sim);
        }

        static Color HeatColor(float frac)
        {
            frac = Mathf.Clamp01(frac);
            if (frac < 0.5f) return Color.Lerp(new Color(0.2f, 0.4f, 1f), new Color(1f, 0.9f, 0.2f), frac * 2f);
            return Color.Lerp(new Color(1f, 0.9f, 0.2f), new Color(1f, 0.1f, 0.05f), (frac - 0.5f) * 2f);
        }

        ParticleSystem sparks;
        MaterialPropertyBlock plasmaProps;

        /// <summary>
        /// Reentry (milestone 23): a glowing shock layer cupping whatever faces the airflow, a long plasma sheath with
        /// flowing streaks trailing behind, and sparks streaming off. Its width follows the vessel's profile across the
        /// flow, so a capsule tumbling sideways wraps itself in a much wider fireball than one coming in shield-first.
        /// </summary>
        void UpdatePlasma(VesselSim sim)
        {
            float strength = sim.leadingPart >= 0 ? Mathf.Clamp01((float)(sim.heatFlux / 6e5)) : 0f;
            bool on = strength >= 0.03f && !sim.destroyed && sim.leadingPart < sim.parts.Count;
            if (sparks != null) Smoke.DriveSparks(sparks, on ? strength : 0f);
            if (!on)
            {
                if (plasma != null && plasma.activeSelf) plasma.SetActive(false);
                return;
            }
            if (plasma == null)
            {
                plasma = new GameObject("Plasma");
                plasma.transform.SetParent(root.transform, false);
                plasma.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Plasma();
                var pr = plasma.AddComponent<MeshRenderer>();
                pr.sharedMaterial = Mats.Reentry;
                pr.shadowCastingMode = ShadowCastingMode.Off;
                plasmaProps = new MaterialPropertyBlock();
            }
            if (!plasma.activeSelf) plasma.SetActive(true);
            Vector3 flowWorld = sim.SurfaceRelVelocity.normalized.ToFloat();          // the air streaming past, in the vessel's world frame
            Vector3 flowLocal = Quaternion.Inverse(root.transform.rotation) * flowWorld;
            // The vessel's silhouette across the flow: how far each part reaches upstream and out to the side
            Vector3 com = sim.com;
            float front = -1e9f, wide = 0.3f;
            foreach (var p in sim.parts)
            {
                Vector3 c = p.Center - com;
                float rad = Mathf.Max(p.RTop, p.RBottom), half = p.def.Height * 0.5f;
                float along = Vector3.Dot(c, -flowLocal);
                float axisAlong = Mathf.Abs(Vector3.Dot(p.Axis, flowLocal));         // 1 when the part points into the flow
                float reachUp = along + Mathf.Lerp(rad, half, axisAlong);              // nose-on it reaches half its length, sideways its radius
                front = Mathf.Max(front, reachUp);
                Vector3 side = c - (-flowLocal) * along;
                float sideReach = side.magnitude + Mathf.Lerp(half, rad, axisAlong);
                wide = Mathf.Max(wide, sideReach);
            }
            Vector3 face = com + (-flowLocal) * (front + 0.15f);
            float r = wide * 1.12f;
            plasma.transform.position = root.transform.TransformPoint(face);
            plasma.transform.rotation = Quaternion.FromToRotation(Vector3.up, -flowWorld);
            float len = (sim.Length + 4f + r * 2f) * (0.7f + 1.8f * strength) * (0.92f + 0.08f * Mathf.PerlinNoise(UnityEngine.Time.time * 18f, 0.5f));
            plasma.transform.localScale = new Vector3(r * (1f + strength * 0.3f), len, r * (1f + strength * 0.3f));
            plasmaProps.SetFloat("_Strength", strength);
            plasmaProps.SetFloat("_Seed", sim.id * 0.37f);
            plasma.GetComponent<MeshRenderer>().SetPropertyBlock(plasmaProps);
            if (sparks == null) sparks = Smoke.CreateSparks(root.transform);
            sparks.transform.position = plasma.transform.position;
            sparks.transform.rotation = Quaternion.LookRotation(flowWorld);
            var shape = sparks.shape;
            shape.radius = r * 0.9f;
        }

        public VesselView SplitOff(VesselSim piece)
        {
            var vv = new VesselView { root = new GameObject("Vessel " + piece.name), flight = true };
            vv.root.transform.SetPositionAndRotation(root.transform.position, root.transform.rotation);
            foreach (var p in piece.parts)
            {
                if (p.def.kind == PartKind.FairingHalf)
                {
                    vv.AddPart(p.def, p.index, p.localPos, p.localRot, p.variant, false); // a new piece, not split from this view
                    vv.AddDecal(p.index, p.decal);
                    continue;
                }
                if (partGo.TryGetValue(p.index, out var go))
                {
                    go.transform.SetParent(vv.root.transform, false);
                    vv.partGo[p.index] = go;
                    partGo.Remove(p.index);
                }
                if (renderers.TryGetValue(p.index, out var r)) { vv.renderers[p.index] = r; renderers.Remove(p.index); }
                if (flaps.TryGetValue(p.index, out var fl)) { vv.flaps[p.index] = fl; flaps.Remove(p.index); }
                if (gearStruts.TryGetValue(p.index, out var gs)) { vv.gearStruts[p.index] = gs; gearStruts.Remove(p.index); vv.wheels[p.index] = wheels[p.index]; wheels.Remove(p.index); }
                if (tyres.TryGetValue(p.index, out var tyr)) { vv.tyres[p.index] = tyr; tyres.Remove(p.index); }
                if (shells.TryGetValue(p.index, out var sh)) { vv.shells[p.index] = sh; shells.Remove(p.index); }
                if (flames.TryGetValue(p.index, out var f)) { vv.flames[p.index] = f; flames.Remove(p.index); }
                if (plumeRenderers.TryGetValue(p.index, out var plr)) { vv.plumeRenderers[p.index] = plr; plumeRenderers.Remove(p.index); }
                if (smokes.TryGetValue(p.index, out var smk)) { vv.smokes[p.index] = smk; smokes.Remove(p.index); }
                if (canopies.TryGetValue(p.index, out var c)) { vv.canopies[p.index] = c; canopies.Remove(p.index); }
                if (struts.TryGetValue(p.index, out var s)) { vv.struts[p.index] = s; struts.Remove(p.index); }
                if (lights.TryGetValue(p.index, out var l)) { vv.lights[p.index] = l; lights.Remove(p.index); }
                if (deployables.TryGetValue(p.index, out var dp)) { vv.deployables[p.index] = dp; deployables.Remove(p.index); }
                if (renderersExtra.TryGetValue(p.index, out var rx)) { vv.renderersExtra[p.index] = rx; renderersExtra.Remove(p.index); }
                if (engineLights.TryGetValue(p.index, out var elx)) { vv.engineLights[p.index] = elx; engineLights.Remove(p.index); }
            }
            return vv;
        }

        /// <summary>Builder: tint a part (null = normal).</summary>
        public void Tint(int index, Color? tint, float emission = 0.35f)
        {
            if (!renderers.TryGetValue(index, out var mr) || mr == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            if (!tint.HasValue) { mr.SetPropertyBlock(null); return; }
            mpb.Clear();
            mpb.SetColor("_Tint", tint.Value);
            mpb.SetFloat("_Emission", emission);
            mpb.SetFloat("_Ambient", 0.35f);
            mr.SetPropertyBlock(mpb);
        }

        public void TintAll(Color? tint, float emission = 0.35f) { foreach (var k in renderers.Keys) Tint(k, tint, emission); }

        public void SetVisible(bool v) { if (root != null) root.SetActive(v); }
        public void Destroy() { if (root != null) Object.Destroy(root); root = null; }

        /// <summary>Milestone 35: takes the parts off the model as loose pieces (kept where they are in the world), then removes the rest.</summary>
        public List<GameObject> DetachParts()
        {
            var list = new List<GameObject>();
            foreach (var kv in partGo)
            {
                var go = kv.Value;
                if (go == null || !go.activeInHierarchy) continue;
                go.transform.SetParent(null, true);
                foreach (var col in go.GetComponentsInChildren<Collider>()) Object.Destroy(col);
                list.Add(go);
            }
            partGo.Clear();
            renderers.Clear();
            renderersExtra.Clear();
            Destroy();
            return list;
        }
    }

    /// <summary>
    /// One planet or moon: a whole-body sphere drawn by the far camera, a detailed ground patch under the
    /// vessel drawn by the near camera, and (for Arden) the launch site.
    /// </summary>
    public class BodyView
    {
        public readonly CelestialBody body;
        readonly GameObject sphere, patch, site, clouds, atmosphere;
        readonly Material planetMat, cloudMat, airMat, ringMat;
        MaterialPropertyBlock patchProps;
        bool mapsReady;
        static Texture2DArray terrainA, terrainN;

        /// <summary>
        /// Which ground photos each kind of world uses (layers of TerrainDetail: 0 grass, 1 rock, 2 beach sand, 3 snow,
        /// 4 grey gravel, 5 ice, 6 desert sand, 7 rocky dirt): base, A, B, C (see MeshFactory.GroundWeights), then rock.
        /// </summary>
        static Vector4 Layers(CelestialBody b) => b.style switch
        {
            BodyStyle.Earthlike => new Vector4(0, 2, 3, 7),
            BodyStyle.Barren => new Vector4(4, 7, 4, 4),
            BodyStyle.Icy => new Vector4(5, 7, 3, 5),
            BodyStyle.Desert => new Vector4(7, 6, 3, 4),
            BodyStyle.Volcanic => new Vector4(4, 7, 4, 4),
            _ => new Vector4(4, 4, 4, 4),
        };

        void UseMaps(PlanetBaker.Maps maps)
        {
            planetMat.SetTexture("_ColorMap", maps.color);
            planetMat.SetTexture("_NormalMap", maps.normal);
            if (maps.clouds != null)
            {
                planetMat.SetTexture("_CloudMap", maps.clouds);
                if (cloudMat != null) { cloudMat.SetTexture("_CloudMap", maps.clouds); cloudMat.SetFloat("_HasMaps", 1f); }
            }
            planetMat.SetFloat("_HasMaps", 1f);
            mapsReady = true;
            patchClouds = -1;   // rebuild the patch without painted-on clouds (the shader paints sharper ones)
        }
        float patchClouds = -1;
        readonly Mesh patchMesh;
        readonly Mesh scatterMesh;
        Vector3d patchCenter = Vector3d.zero;
        double patchHalf;
        float patchMapBlend;
        readonly Dictionary<string, GameObject> landmarkObjs = new Dictionary<string, GameObject>();   // milestone 27
        int patchResUsed;
        /// <summary>Goes up each time the close-up ground is rebuilt (prints and tracks then move onto the new ground).</summary>
        public int PatchVersion { get; private set; }
        readonly Quaternion siteLocalRot;
        readonly Vector3d siteFixedPos;

        public BodyView(CelestialBody body, bool withLaunchSite)
        {
            this.body = body;
            sphere = new GameObject("Body " + body.name) { layer = GameRoot.LayerFar };
            sphere.AddComponent<MeshFilter>().sharedMesh = MeshFactory.PlanetMesh(body, body.IsPlanet ? 72 : 48);
            var sr = sphere.AddComponent<MeshRenderer>();
            // Each world has its own copy of the ground material: its own air, sky colour and kind of ground
            planetMat = new Material(Mats.Planet) { name = "Planet " + body.name };
            RenderEnv.ApplyAir(planetMat, body);
            planetMat.SetFloat("_Regolith", body.HasAtmosphere || body.style == BodyStyle.Gas ? 0f : 1f);
            if (terrainA == null)
            {
                terrainA = Resources.Load<Texture2DArray>("Textures/TerrainDetailA");
                terrainN = Resources.Load<Texture2DArray>("Textures/TerrainDetailN");
                if (terrainA == null || terrainN == null) Debug.LogError("Ground textures missing from Resources/Textures");
            }
            planetMat.SetTexture("_TerrainA", terrainA);
            planetMat.SetTexture("_TerrainN", terrainN);
            planetMat.SetVector("_Layers", Layers(body));
            planetMat.SetFloat("_LayerRock", 1f);
            planetMat.SetFloat("_Detail", body.style == BodyStyle.Gas ? 0f : 1f);
            planetMat.SetFloat("_CloudsOn", body.style == BodyStyle.Earthlike && body.HasAtmosphere ? 1f : 0f);
            var baked = PlanetBaker.Get(body);
            PlanetBaker.Ready += (b, m) => { if (b == this.body) UseMaps(m); };
            if (body.isStar)
            {
                // The sun shines by itself: bright enough to bloom
                var sunMat = new Material(Mats.Unlit) { name = "Sun surface" };
                sunMat.SetColor("_Color", new Color(9f, 8f, 6.5f, 1f));
                sr.sharedMaterial = sunMat;
            }
            else sr.sharedMaterial = planetMat;
            sr.shadowCastingMode = ShadowCastingMode.Off;

            patch = new GameObject("GroundPatch " + body.name);
            patchMesh = new Mesh { name = "GroundPatch" };
            patch.AddComponent<MeshFilter>().sharedMesh = patchMesh;
            var pr = patch.AddComponent<MeshRenderer>();
            pr.sharedMaterial = planetMat;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = true;
            patch.SetActive(false);
            // Milestone 25: rocks and boulders strewn round wherever the close-up ground is (a child of the patch, same frame)
            var sc = new GameObject("Rocks " + body.name);
            sc.transform.SetParent(patch.transform, false);
            scatterMesh = new Mesh { name = "Rocks", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            sc.AddComponent<MeshFilter>().sharedMesh = scatterMesh;
            var scr = sc.AddComponent<MeshRenderer>();
            scr.sharedMaterial = Mats.Lit;
            scr.shadowCastingMode = ShadowCastingMode.On;
            scr.receiveShadows = true;

            // Worlds with air get a glowing rim in their sky's colour; only Arden has water clouds.
            if (body.HasAtmosphere)
            {
                if (body.style == BodyStyle.Earthlike)
                {
                    clouds = new GameObject("Clouds " + body.name) { layer = GameRoot.LayerFar };
                    var b = body;
                    clouds.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Shell((float)body.radius + MeshFactory.CloudAltitude, 140,
                        d => { var c = new Color(0.97f, 0.98f, 1f); c.a = MeshFactory.CloudCover(b, d); return c; }, "Clouds");
                    var cr = clouds.AddComponent<MeshRenderer>();
                    cloudMat = new Material(Mats.Cloud) { name = "Clouds " + body.name };
                    RenderEnv.ApplyAir(cloudMat, body);
                    cr.sharedMaterial = cloudMat;
                    cr.shadowCastingMode = ShadowCastingMode.Off;
                }
                // The sky: a shell at the top of the air, scattering sunlight (see Rocket/Atmosphere)
                atmosphere = new GameObject("Atmosphere " + body.name) { layer = GameRoot.LayerFar };
                atmosphere.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Shell(RenderEnv.AirOf(body).top, 64, d => Color.white, "Atmosphere");
                var ar = atmosphere.AddComponent<MeshRenderer>();
                airMat = new Material(Mats.Atmosphere) { name = "Atmosphere " + body.name };
                RenderEnv.ApplyAir(airMat, body);
                ar.sharedMaterial = airMat;
                ar.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (baked != null) UseMaps(baked);

            if (body.ringOuter > 0)
            {
                // Rings: lit, see-through, both sides drawn; they turn with the planet (they look the same all round)
                var ring = new GameObject("Rings " + body.name) { layer = GameRoot.LayerFar };
                ring.transform.SetParent(sphere.transform, false);
                ring.AddComponent<MeshFilter>().sharedMesh = MeshFactory.RingMesh(body);
                var rr = ring.AddComponent<MeshRenderer>();
                ringMat = new Material(Mats.Cloud) { name = "Rings " + body.name };
                RenderEnv.ApplyAir(ringMat, body);
                ringMat.SetFloat("_AtmoOn", 0f);   // the rings sit in vacuum (but the planet still shadows them)
                rr.sharedMaterial = ringMat;
                rr.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (withLaunchSite)
            {
                Vector3d up = LaunchSite.Direction;
                siteFixedPos = up * body.radius;
                siteLocalRot = Quaternion.LookRotation((-CelestialBody.NorthAt(up)).ToFloat(), up.ToFloat());
                site = BuildSite();
                siteLights = BuildSiteLights(site.transform);
                BuildPadArms();
                siteCrowd = BuildCrowd(site.transform);
                var rw = BuildRunway();
                rw.transform.SetParent(site.transform, false);
            }
        }

        /// <summary>Runway strip that follows the planet's curve (it drops ~1 m at the ends of 2.4 km).</summary>
        GameObject BuildRunway()
        {
            var b = new MeshBuilder();
            Quaternion inv = Quaternion.Inverse(siteLocalRot);
            Vector3 L(double e, double nn, double h) => inv * ((LaunchSite.DirectionAt(body, e, nn) * (body.radius + h)) - siteFixedPos).ToFloat();
            Vector3 N(double e, double nn) => inv * LaunchSite.DirectionAt(body, e, nn).ToFloat();
            Color asphalt = new Color(0.42f, 0.43f, 0.44f), paint = new Color(0.9f, 0.9f, 0.88f);
            b.Mat = MeshBuilder.MatAsphalt;
            double len = LaunchSite.RunwayLength, w = LaunchSite.RunwayWidth / 2, top = LaunchSite.RunwayHeight, c = LaunchSite.RunwayNorth;
            const int segs = 60;
            for (int i = 0; i < segs; i++)
            {
                double e0 = LaunchSite.RunwayEast - len / 2 + len * i / segs, e1 = LaunchSite.RunwayEast - len / 2 + len * (i + 1) / segs;
                Vector3 up = N((e0 + e1) / 2, c);
                b.AddQuad(L(e0, c - w, top), L(e1, c - w, top), L(e1, c + w, top), L(e0, c + w, top), up, asphalt);
                Vector3 sideN = inv * CelestialBody.NorthAt(LaunchSite.Direction).ToFloat();
                b.AddQuad(L(e0, c + w, top), L(e1, c + w, top), L(e1, c + w, -1.5), L(e0, c + w, -1.5), sideN, asphalt);
                b.AddQuad(L(e0, c - w, top), L(e1, c - w, top), L(e1, c - w, -1.5), L(e0, c - w, -1.5), -sideN, asphalt);
            }
            b.Mat = MeshBuilder.MatMatte;
            for (double e = LaunchSite.RunwayEast - len / 2 + 60; e < LaunchSite.RunwayEast + len / 2 - 60; e += 60)
                b.AddQuad(L(e, c - 0.5, top + 0.03), L(e + 30, c - 0.5, top + 0.03), L(e + 30, c + 0.5, top + 0.03), L(e, c + 0.5, top + 0.03), N(e, c), paint);
            foreach (double end in new[] { LaunchSite.RunwayEast - len / 2 + 8, LaunchSite.RunwayEast + len / 2 - 38 })
                for (int k = -4; k <= 4; k++)
                {
                    if (k == 0) continue;
                    double nn = c + k * 3.6;
                    b.AddQuad(L(end, nn - 1, top + 0.03), L(end + 30, nn - 1, top + 0.03), L(end + 30, nn + 1, top + 0.03), L(end, nn + 1, top + 0.03), N(end, nn), paint);
                }
            // Edge lights every 60 m
            b.Mat = MeshBuilder.MatLamp;
            for (double e = LaunchSite.RunwayEast - len / 2; e <= LaunchSite.RunwayEast + len / 2; e += 60)
                foreach (double side in new[] { -w - 1.5, w + 1.5 })
                {
                    Vector3 lp = L(e, c + side, top + 0.25);
                    b.AddBox(lp, new Vector3(0.35f, 0.5f, 0.35f), new Color(1f, 0.95f, 0.8f), Quaternion.identity);
                }
            var go = new GameObject("Runway");
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("Runway");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            return go;
        }

        Crowd siteCrowd;

        // ---------------------------------------------------------------- swing arms (milestone 26)
        readonly System.Collections.Generic.List<(Transform t, Quaternion closed, int pad)> padArms = new System.Collections.Generic.List<(Transform, Quaternion, int)>();

        /// <summary>Each pad's two swing arms (the upper one with a covered walkway to the hatch), pivoting at the tower.</summary>
        void BuildPadArms()
        {
            Vector3 E = At(1, 0).normalized, N = At(0, 1).normalized, U = Vector3.up;
            Vector3[] towerSide = { -E, -N, E, N };       // where each pad's tower stands (matches BuildSite and ExtraPad)
            float[] towerOff = { 7f, 7.5f, 7.5f, 7.5f };
            Color white = new Color(0.6f, 0.61f, 0.62f), dark = new Color(0.28f, 0.29f, 0.31f), glass = new Color(0.1f, 0.16f, 0.22f);
            Mesh ArmMesh(float len, float w, bool walkway)
            {
                var mb = new MeshBuilder();
                mb.Mat = MeshBuilder.MatPaint;
                mb.AddBox(new Vector3(0, 0, len * 0.5f + 0.1f), new Vector3(w, 0.45f, len), white, Quaternion.identity);
                mb.Mat = MeshBuilder.MatMetal;
                foreach (int sgn in new[] { -1, 1 }) mb.AddBox(new Vector3(sgn * w * 0.45f, 0.6f, len * 0.5f + 0.1f), new Vector3(0.05f, 0.05f, len), dark, Quaternion.identity);
                if (walkway)
                {
                    mb.Mat = MeshBuilder.MatPaint;
                    mb.AddBox(new Vector3(0, 1.1f, len * 0.5f + 0.1f), new Vector3(w, 1.7f, len * 0.92f), white, Quaternion.identity);
                    mb.Mat = MeshBuilder.MatGlass;
                    foreach (int sgn in new[] { -1, 1 }) mb.AddBox(new Vector3(sgn * (w * 0.5f + 0.01f), 1.25f, len * 0.5f), new Vector3(0.02f, 0.6f, len * 0.7f), glass, Quaternion.identity);
                }
                return mb.ToMesh("Swing arm");
            }
            var upper = ArmMesh(4.6f, 1.1f, true);
            var lower = ArmMesh(3.6f, 0.8f, false);
            for (int i = 0; i < LaunchSite.Pads.Length; i++)
            {
                var pd = LaunchSite.Pads[i];
                Vector3 T = towerSide[i], pivot0 = At(pd.e, pd.n) + U * (float)LaunchSite.PadHeight + T * towerOff[i];
                foreach (var (h, mesh) in new[] { (i == 0 ? 24f : 22f, upper), (i == 0 ? 14f : 13f, lower) })
                {
                    var go = new GameObject("Swing arm " + pd.name);
                    go.transform.SetParent(site.transform, false);
                    Quaternion closed = Quaternion.LookRotation(-T, U);
                    go.transform.SetLocalPositionAndRotation(pivot0 + U * h, closed);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = Mats.Lit;
                    mr.shadowCastingMode = ShadowCastingMode.On;
                    go.layer = site.layer;
                    padArms.Add((go.transform, closed, i));
                }
            }
        }

        /// <summary>Swings a pad's arms away from the rocket (0 in at the rocket, 1 swung clear); pad -1 sets them all.</summary>
        public void SetPadArms(int pad, float open)
        {
            foreach (var (t, closed, p) in padArms)
                if (pad < 0 || p == pad) t.localRotation = closed * Quaternion.Euler(0, Mathf.SmoothStep(0f, 1f, open) * 78f, 0);
        }
        Light[] siteLights;
        bool siteLightsOn = true;

        /// <summary>
        /// Milestone 25: real lights at the space centre for the night: floodlights on every pad, a lamp over the assembly
        /// building's door, lights along the campus road, the airport apron, the helipads, the town's streets and the pier.
        /// They come on when the sun goes down (BodyView.Sync).
        /// </summary>
        Light[] BuildSiteLights(Transform siteT)
        {
            var list = new List<Light>();
            Vector3 U = Vector3.up;
            void Spot(double e, double n, float h, double ae, double an, float ah, float range, float angle, float intensity)
            {
                var go = new GameObject("Floodlight");
                go.transform.SetParent(siteT, false);
                Vector3 p = At(e, n) + U * h, aim = At(ae, an) + U * ah;
                go.transform.SetLocalPositionAndRotation(p, Quaternion.LookRotation(aim - p, U));
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot; l.range = range; l.spotAngle = angle; l.intensity = intensity;
                l.color = new Color(1f, 0.93f, 0.8f); l.shadows = LightShadows.None;
                list.Add(l);
            }
            void Lamp(double e, double n, float h, float range, float intensity)
            {
                var go = new GameObject("Lamp light");
                go.transform.SetParent(siteT, false);
                go.transform.localPosition = At(e, n) + U * h;
                var l = go.AddComponent<Light>();
                l.type = LightType.Point; l.range = range; l.intensity = intensity;
                l.color = new Color(1f, 0.82f, 0.58f); l.shadows = LightShadows.None;
                list.Add(l);
            }
            foreach (var c in new[] { new Vector2(16, 16), new Vector2(16, -16), new Vector2(-16, 16), new Vector2(-16, -16) })
                Spot(c.x, -c.y, 22f, 0, 0, 4f, 90f, 60f, 2600f);
            for (int i = 1; i < LaunchSite.Pads.Length; i++)
            {
                var pd = LaunchSite.Pads[i];
                Spot(pd.e + 24, pd.n + 24, 22f, pd.e, pd.n, 4f, 90f, 60f, 2600f);
                Spot(pd.e - 24, pd.n - 24, 22f, pd.e, pd.n, 4f, 90f, 60f, 2600f);
            }
            Spot(0, 292, 72f, 0, 240, 0f, 140f, 70f, 9000f);                                  // over the assembly building's door
            for (double e = -290; e <= 320; e += 61) Lamp(e, 207, 6f, 30f, 70f);           // the campus road
            Lamp(-168, 220, 6f, 34f, 80f);                                                  // headquarters' plaza
            foreach (double n in new[] { 440.0, 540.0, 630.0 }) Spot(-330, n, 14f, -380, n, 0f, 80f, 80f, 1400f);   // the airport apron
            Spot(330, 112, 12f, 330, 60, 0f, 70f, 80f, 1200f);                                // the helipads
            foreach (double n in new[] { 560.0, 650.0, 740.0 }) foreach (double e in new[] { 230.0, 365.0, 500.0 }) Lamp(e, n + 5, 6f, 28f, 60f);   // the town
            Lamp(3000, 125, 8f, 34f, 90f); Lamp(3300, 125, 8f, 34f, 90f);                    // the pier
            Spot(1270, 182, 50f, 1270, 120, 0f, 120f, 70f, 6000f);                             // the east hall's door
            foreach (var l in list) l.enabled = false;
            siteLightsOn = false;
            return list.ToArray();
        }
        readonly List<double[]> keepClear = new List<double[]>();   // e0, n0, e1, n1: no random trees here
        Vector3d lastSyncDir;   // where the vessel was at the last Sync, to tell whether it's standing still
        double lastSyncUt = double.NegativeInfinity;

        /// <summary>A spot on the ground near the space center, by metres east and north of the pad, in the site's own coordinates.</summary>
        Vector3 At(double east, double north)
        {
            Quaternion inv = Quaternion.Inverse(siteLocalRot);
            return inv * ((LaunchSite.DirectionAt(body, east, north) * body.radius) - siteFixedPos).ToFloat();
        }

        GameObject BuildSite()
        {
            // Milestone 20b: the space centre in detail. Same footprint as before (the sim knows the pad and tower).
            var b = new MeshBuilder();
            Color dark = new Color(0.28f, 0.29f, 0.31f), steel = new Color(0.72f, 0.73f, 0.76f);
            Color white = new Color(0.6f, 0.61f, 0.62f), teal = new Color(0.3f, 0.31f, 0.33f), red = new Color(0.55f, 0.1f, 0.07f), yellow = new Color(0.85f, 0.65f, 0.12f);
            float ph = (float)LaunchSite.PadHeight, hs = (float)LaunchSite.PadHalfSize;

            // Pad: concrete deck, a steel launch plate, a blackened flame trench and yellow edge markings
            b.Mat = MeshBuilder.MatConcrete;
            b.AddBox(new Vector3(0, ph - 2f, 0), new Vector3(hs * 2, 4f, hs * 2), new Color(0.62f, 0.62f, 0.6f), Quaternion.identity);
            b.Mat = MeshBuilder.MatDarkMetal;
            b.AddBox(new Vector3(0, ph + 0.02f, 0), new Vector3(3.2f, 0.06f, 3.2f), dark * 2f, Quaternion.identity);
            b.AddBox(new Vector3(6.5f, ph + 0.01f, 0), new Vector3(10f, 0.03f, 3.6f), new Color(0.12f, 0.12f, 0.12f), Quaternion.identity);
            b.Mat = MeshBuilder.MatMatte;
            foreach (int sgn in new[] { -1, 1 })
            {
                b.AddBox(new Vector3(0, ph + 0.012f, sgn * (hs - 0.6f)), new Vector3(hs * 2 - 1.2f, 0.02f, 0.25f), yellow, Quaternion.identity);
                b.AddBox(new Vector3(sgn * (hs - 0.6f), ph + 0.012f, 0), new Vector3(0.25f, 0.02f, hs * 2 - 1.2f), yellow, Quaternion.identity);
            }

            // The service tower: an open steel lattice, red, with walkways, two swing arms and a lightning mast
            b.Mat = MeshBuilder.MatPaint;
            const float tx = -7f, tw = 2.4f, th = 34f;
            for (int cx = -1; cx <= 1; cx += 2)
                for (int cz = -1; cz <= 1; cz += 2)
                    b.AddBox(new Vector3(tx + cx * tw * 0.5f, ph + th * 0.5f, cz * tw * 0.5f), new Vector3(0.28f, th, 0.28f), red, Quaternion.identity);
            for (float y = 0; y <= th; y += 2.8f)
            {
                foreach (int sgn in new[] { -1, 1 })
                {
                    b.AddBox(new Vector3(tx, ph + y, sgn * tw * 0.5f), new Vector3(tw, 0.16f, 0.16f), red, Quaternion.identity);
                    b.AddBox(new Vector3(tx + sgn * tw * 0.5f, ph + y, 0), new Vector3(0.16f, 0.16f, tw), red, Quaternion.identity);
                    // X braces on every face
                    float diag = Mathf.Sqrt(tw * tw + 2.8f * 2.8f), ang = Mathf.Atan2(2.8f, tw) * Mathf.Rad2Deg;
                    if (y + 2.8f <= th + 0.01f)
                    {
                        b.AddBox(new Vector3(tx, ph + y + 1.4f, sgn * tw * 0.5f), new Vector3(diag, 0.1f, 0.1f), red, Quaternion.Euler(0, 0, sgn * ang));
                        b.AddBox(new Vector3(tx + sgn * tw * 0.5f, ph + y + 1.4f, 0), new Vector3(0.1f, 0.1f, diag), red, Quaternion.Euler(sgn * ang, 0, 0));
                    }
                }
            }
            b.Mat = MeshBuilder.MatMetal;
            for (int i = 0; i < 6; i++) b.AddBox(new Vector3(tx, ph + 3f + i * 5.5f, 0), new Vector3(2.9f, 0.18f, 2.9f), steel, Quaternion.identity);   // walkway decks
            b.Mat = MeshBuilder.MatPaint;   // (milestone 26: the swing arms are their own objects so they can pull away, BuildPadArms)
            b.Mat = MeshBuilder.MatMetal;
            b.AddFrustum(ph + th + 9f, ph + th, 0.06f, 0.14f, steel, 8);                                                // lightning mast
            int ms = b.v.Count - 18;
            b.Transform(ms, Quaternion.identity, new Vector3(tx, 0, 0));
            b.Mat = MeshBuilder.MatLamp;
            b.AddSphere(new Vector3(tx, ph + th + 9.15f, 0), 0.18f, new Color(1f, 0.15f, 0.1f), 10, 6);
            b.Mat = MeshBuilder.MatPaint;

            // Floodlight masts at the pad corners
            foreach (var c in new[] { new Vector2(16, 16), new Vector2(16, -16), new Vector2(-16, 16), new Vector2(-16, -16) })
            {
                b.Mat = MeshBuilder.MatMetal;
                b.AddBox(new Vector3(c.x, 11f, c.y), new Vector3(0.35f, 22f, 0.35f), steel, Quaternion.identity);
                b.AddBox(new Vector3(c.x, 22.2f, c.y), new Vector3(2.4f, 0.8f, 0.5f), dark * 2f, Quaternion.LookRotation(new Vector3(-c.x, 0, -c.y)));
                b.Mat = MeshBuilder.MatLamp;
                b.AddBox(new Vector3(c.x, 22.2f, c.y) + new Vector3(-c.x, 0, -c.y).normalized * 0.27f, new Vector3(2.1f, 0.55f, 0.05f), new Color(1f, 0.97f, 0.9f), Quaternion.LookRotation(new Vector3(-c.x, 0, -c.y)));
            }

            // Propellant farm: two big spherical tanks on legs, and a water tower
            b.Mat = MeshBuilder.MatGloss;
            foreach (float x in new[] { 60f, 76f })
            {
                b.AddSphere(new Vector3(x, 9f, -42f), 6f, white, 28, 16);
                b.Mat = MeshBuilder.MatMetal;
                for (int k = 0; k < 6; k++) { Quaternion q = Quaternion.Euler(0, 60 * k, 0); b.AddBox(new Vector3(x, 0, -42f) + q * new Vector3(5f, 3.5f, 0), new Vector3(0.4f, 7f, 0.4f), steel, q); }
                b.Mat = MeshBuilder.MatGloss;
            }
            b.AddSphere(new Vector3(36f, 26f, 34f), 4.5f, white, 24, 12);
            b.Mat = MeshBuilder.MatMetal;
            for (int k = 0; k < 4; k++) { Quaternion q = Quaternion.Euler(0, 45 + 90 * k, 0); b.AddBox(new Vector3(36f, 12f, 34f) + q * new Vector3(2.6f, 0, 0), new Vector3(0.45f, 24f, 0.45f), steel, q); }
            b.Mat = MeshBuilder.MatPaint;
            b.AddBox(new Vector3(36f, 26.2f, 34f + 4.4f), new Vector3(3f, 1.2f, 0.1f), teal, Quaternion.identity);

            // (milestone 24: the crawlerway is now the two-lane rocket road in BuildCompound)

            // Assembly building: the tall main hall (corrugated cladding) with a lower office wing on its east side, vertical
            // ribs, the big door facing the pad with its teal frame, our emblem and hazard stripes
            b.Mat = MeshBuilder.MatFacade(MeshBuilder.Cladding);
            b.AddBox(new Vector3(0, 41f, -340f), new Vector3(110f, 82f, 84f), new Color(0.71f, 0.7f, 0.67f), Quaternion.identity);
            b.Mat = MeshBuilder.MatMatte;
            b.AddBox(new Vector3(0, 82.6f, -340f), new Vector3(112f, 1.2f, 86f), teal, Quaternion.identity);
            b.Mat = MeshBuilder.MatFacade(MeshBuilder.Ribbon);
            b.AddBox(new Vector3(77f, 14f, -332f), new Vector3(44f, 28f, 60f), new Color(0.78f, 0.72f, 0.62f), Quaternion.identity);
            b.Mat = MeshBuilder.MatMatte;
            b.AddBox(new Vector3(77f, 28.4f, -332f), new Vector3(45f, 0.8f, 61f), teal, Quaternion.identity);
            Color ribC = new Color(0.68f, 0.69f, 0.7f);
            for (float x = -53f; x <= 53f; x += 4f) b.AddBox(new Vector3(x, 41f, -297.9f), new Vector3(0.45f, 82f, 0.25f), ribC, Quaternion.identity);
            for (float z = -380f; z <= -300f; z += 4f) foreach (int sgn in new[] { -1, 1 }) b.AddBox(new Vector3(sgn * 55.1f, 41f, z), new Vector3(0.25f, 82f, 0.45f), ribC, Quaternion.identity);
            b.Mat = MeshBuilder.MatDarkMetal;
            b.AddBox(new Vector3(0, 34f, -297.5f), new Vector3(30f, 68f, 0.5f), new Color(0.22f, 0.24f, 0.27f), Quaternion.identity);
            for (float y = 6f; y < 68f; y += 6f) b.AddBox(new Vector3(0, y, -297.2f), new Vector3(30f, 0.25f, 0.2f), new Color(0.1f, 0.11f, 0.12f), Quaternion.identity);
            b.AddBox(new Vector3(0, 34f, -297.2f), new Vector3(0.25f, 68f, 0.2f), new Color(0.1f, 0.11f, 0.12f), Quaternion.identity);
            b.Mat = MeshBuilder.MatPaint;
            b.AddBox(new Vector3(0, 68.8f, -297.3f), new Vector3(32f, 1.6f, 0.6f), teal, Quaternion.identity);
            foreach (int sgn in new[] { -1, 1 }) b.AddBox(new Vector3(sgn * 15.8f, 34f, -297.3f), new Vector3(1.6f, 69f, 0.6f), teal, Quaternion.identity);
            b.AddBox(new Vector3(36f, 50f, -297.6f), new Vector3(22f, 32f, 0.25f), teal, Quaternion.identity);
            b.AddBox(new Vector3(36f, 50f, -297.4f), new Vector3(8f, 8f, 0.3f), Color.white, Quaternion.Euler(0, 0, 45));
            b.AddBox(new Vector3(-36f, 70f, -297.6f), new Vector3(28f, 4f, 0.25f), teal, Quaternion.identity);
            for (int k = 0; k < 12; k++) b.AddBox(new Vector3(-14.5f + k * 2.6f, 1.2f, -297.1f), new Vector3(1.3f, 2.4f, 0.1f), k % 2 == 0 ? yellow : dark, Quaternion.identity);
            b.Mat = MeshBuilder.MatMatte;

            // Mission control: a low block with a band of dark windows and a dish on the roof
            b.Mat = MeshBuilder.MatFacade(MeshBuilder.Punched);
            b.AddBox(new Vector3(170f, 7f, -140f), new Vector3(36f, 14f, 22f), new Color(0.8f, 0.72f, 0.58f), Quaternion.identity);
            b.Mat = MeshBuilder.MatMatte;
            b.AddBox(new Vector3(170f, 14.4f, -140f), new Vector3(37f, 0.8f, 23f), teal, Quaternion.identity);
            b.Mat = MeshBuilder.MatMetal;
            b.AddFrustum(18.5f, 14.8f, 0.3f, 0.5f, steel, 10);
            ms = b.v.Count - 22;
            b.Transform(ms, Quaternion.identity, new Vector3(178f, 0, -142f));
            {
                int ds = b.v.Count;
                b.Mat = MeshBuilder.MatPaint;
                b.AddSphere(Vector3.zero, 4f, white, 24, 6, Mathf.PI * 0.32f, doubleSided: true);
                for (int i = ds; i < b.v.Count; i++) b.v[i] = new Vector3(b.v[i].x, -b.v[i].y + 4f * Mathf.Cos(Mathf.PI * 0.32f), b.v[i].z);
                b.Transform(ds, Quaternion.Euler(-35f, 30f, 0), new Vector3(178f, 19.5f, -142f));
            }
            b.Mat = MeshBuilder.MatMatte;

            // Vehicle sheds south of the pad (the tracking station moved west in milestone 24)
            for (int i = 0; i < 3; i++)
            {
                b.Mat = MeshBuilder.MatFacade(MeshBuilder.Cladding);
                b.AddBox(new Vector3(70f + i * 16f, 6f, 70f), new Vector3(10f, 12f, 10f), new Color(0.6f, 0.62f, 0.6f), Quaternion.Euler(0, 45, 0));
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(new Vector3(70f + i * 16f, 12.3f, 70f), new Vector3(10.4f, 0.6f, 10.4f), teal, Quaternion.Euler(0, 45, 0));
            }
            BuildCampus(b, white, teal, dark, steel, yellow);
            Debug.Log($"[site] space centre mesh: {b.v.Count} vertices, {b.t.Count / 3} triangles");
            var go = new GameObject("LaunchSite");
            go.AddComponent<MeshFilter>().sharedMesh = b.ToMesh("LaunchSite");
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            return go;
        }

        /// <summary>
        /// Milestone 23: the rest of the space center. Headquarters with a flag, the astronaut complex, an R&D campus with a
        /// big dish, an airport by the runway (control tower, arched hangars, an apron with a parked plane), roads between
        /// them, parking lots and a few hundred trees.
        /// </summary>
        void BuildCampus(MeshBuilder b, Color white, Color teal, Color dark, Color steel, Color yellow)
        {
            Color glass = new Color(0.1f, 0.16f, 0.22f), asphalt = new Color(0.5f, 0.5f, 0.52f), concrete = new Color(0.62f, 0.62f, 0.6f);
            Color trunk = new Color(0.32f, 0.22f, 0.14f), leaf = new Color(0.12f, 0.24f, 0.09f), leaf2 = new Color(0.17f, 0.3f, 0.11f);
            Vector3 E = At(1, 0).normalized, N = At(0, 1).normalized;
            Quaternion face(double e, double n) => Quaternion.LookRotation(E * (float)e + N * (float)n, Vector3.up);
            // A building: a box of size (east-west, height, north-south) standing on the ground at (east, north)
            void Block(double e, double n, Vector3 size, Color c, double turn = 0)
            {
                Vector3 at = At(e, n);
                b.AddBox(at + Vector3.up * size.y * 0.5f, size, c, Quaternion.LookRotation(N, Vector3.up) * Quaternion.Euler(0, (float)turn, 0));
            }
            void Road(double e0, double n0, double e1, double n1, float w)
            {
                b.Mat = MeshBuilder.MatAsphalt;
                int segs = Mathf.Max(1, (int)(Vector2.Distance(new Vector2((float)e0, (float)n0), new Vector2((float)e1, (float)n1)) / 60));
                var side = new Vector2((float)-(n1 - n0), (float)(e1 - e0)).normalized * (w * 0.5f);
                for (int i = 0; i < segs; i++)
                {
                    double t0 = (double)i / segs, t1 = (double)(i + 1) / segs;
                    double ea = e0 + (e1 - e0) * t0, na = n0 + (n1 - n0) * t0, eb = e0 + (e1 - e0) * t1, nb = n0 + (n1 - n0) * t1;
                    Vector3 a0 = At(ea - side.x, na - side.y) + Vector3.up * 0.16f, a1 = At(ea + side.x, na + side.y) + Vector3.up * 0.16f;
                    Vector3 b0 = At(eb - side.x, nb - side.y) + Vector3.up * 0.16f, b1 = At(eb + side.x, nb + side.y) + Vector3.up * 0.16f;
                    b.AddQuad(a0, b0, b1, a1, Vector3.up, asphalt);
                }
                b.Mat = MeshBuilder.MatMatte;
                if (w < 7) return;
                double len = System.Math.Sqrt((e1 - e0) * (e1 - e0) + (n1 - n0) * (n1 - n0));
                double ue = (e1 - e0) / len, un = (n1 - n0) / len;
                for (double t = 6; t + 4 < len - 6; t += 13)
                    PaveStrip(b, e0 + ue * t, n0 + un * t, e0 + ue * (t + 4), n0 + un * (t + 4), 0.22, 0.175f, new Color(0.86f, 0.86f, 0.84f));
            }

            // Roads (milestone 24, captaincarl24's map): across the campus, round the assembly building to the airport and the
            // north blocks, east to the west launch complex, north to the mainland, and the far side's road past the
            // north-west complex down to the research campus
            var roads = new[]
            {
                new double[] { -90, 200, -90, 525, 9 }, new double[] { -94, 525, 150, 525, 8 },          // round the assembly building
                new double[] { -312, 200, 332, 200, 8 }, new double[] { 150, 200, 150, 300, 7 },         // the campus road
                new double[] { -94, 525, -340, 525, 8 },                                                 // to the airport
                new double[] { -90, 525, -90, 780, 8 },                                                  // to the north blocks
                new double[] { 332, 200, 332, 118, 7 },                                                  // to the helipads
                new double[] { 195, 382, 1030, 200, 8 }, new double[] { 1030, 200, 1200, 168, 8 },       // east to the launch complex
                new double[] { 150, 525, 180, 985, 8 }, new double[] { 180, 985, 490, 1316, 8 },         // north
                new double[] { 490, 1316, 857, 1724, 8 }, new double[] { 857, 1724, 1574, 2102, 8 }, new double[] { 1574, 2102, 2700, 2800, 8 },
                new double[] { 490, 1316, -740, 1204, 8 }, new double[] { -740, 1204, -1262, 1143, 8 },  // the north road
                new double[] { -1800, 3000, -1444, 2300, 8 }, new double[] { -1444, 2300, -1085, 1508, 8 },
                new double[] { -1085, 1508, -997, 998, 8 }, new double[] { -997, 998, -956, 598, 8 },
                new double[] { -956, 598, -913, 182, 8 }, new double[] { -913, 182, -905, 130, 8 },      // down the far side
                new double[] { -940, 332, -1538, 298, 8 },                                               // to the research campus
                new double[] { -959, 657, -735, 505, 7 },                                                // to the runways
                // (more, for a busier place)
                new double[] { 332, 200, 470, 322, 8 },                                                  // campus road to the east road
                new double[] { 152, 560, 575, 560, 7 }, new double[] { 158, 650, 575, 650, 7 }, new double[] { 164, 740, 575, 740, 7 },   // the town
                new double[] { 300, 359, 300, 740, 7 }, new double[] { 430, 331, 430, 740, 7 }, new double[] { 575, 300, 575, 740, 7 },
                new double[] { 490, 1316, 600, 1255, 8 }, new double[] { 600, 1255, 790, 1240, 7 },     // the visitor centre
                new double[] { 640, 285, 640, 110, 7 }, new double[] { 640, 110, 1000, 110, 7 }, new double[] { 1000, 110, 1000, 207, 7 },   // industrial loop
                new double[] { 520, 311, 520, 282, 6 },                                                  // the substation
                new double[] { 1000, 110, 1130, 110, 7 }, new double[] { 1130, 110, 1160, -300, 7 }, new double[] { 1160, -300, 1300, -1393, 7 },   // to the lighthouse
                new double[] { 1420, 262, 1750, 330, 8 }, new double[] { 1750, 330, 2500, 300, 8 }, new double[] { 2500, 300, 2600, 222, 8 },   // to the harbour
                new double[] { -956, 598, -1140, 540, 7 },                                               // the test stand
                new double[] { -1190, 1082, -1150, 1082, 6 },                                            // the north-west car park
            };
            foreach (var r in roads) Road(r[0], r[1], r[2], r[3], (float)r[4]);

            // Milestone 24: the paved compound, the tracking station, the research campus and the airport
            BuildCompound(b, E, N, white, teal, steel, dark);

            // Headquarters (milestone 24: rebuilt in detail, see BuildHeadquarters)
            BuildHeadquarters(b, E, N, white, teal, steel, dark);

            // Astronaut complex: a round building with a domed roof
            {
                Vector3 at = At(150, 320);
                b.Mat = MeshBuilder.MatMatte;
                int s0 = b.v.Count;
                b.AddFrustum(9f, 0f, 22f, 22f, white, 36, capBottom: true);
                b.Transform(s0, Quaternion.identity, at);
                b.Mat = MeshBuilder.MatGloss;
                s0 = b.v.Count;
                b.AddSphere(Vector3.zero, 22f, new Color(0.82f, 0.84f, 0.86f), 36, 10, Mathf.PI * 0.5f);
                for (int i = s0; i < b.v.Count; i++) b.v[i] = new Vector3(b.v[i].x, b.v[i].y * 0.45f, b.v[i].z) + at + Vector3.up * 9f;
                b.Mat = MeshBuilder.MatGlass;
                s0 = b.v.Count;
                b.AddFrustum(6f, 3f, 22.1f, 22.1f, glass, 36);
                b.Transform(s0, Quaternion.identity, at);
                b.Mat = MeshBuilder.MatPaint;
                s0 = b.v.Count;
                b.AddFrustum(9.2f, 8.6f, 22.4f, 22.4f, teal, 36);
                b.Transform(s0, Quaternion.identity, at);
            }

            // Parking lots            // Parking lots with parked cars
            var rng = new System.Random(2026);
            b.Mat = MeshBuilder.MatAsphalt;
            foreach (var (pe, pn) in new[] { (-100.0, 228.0), (210.0, 225.0) })
            {
                Vector3 at = At(pe, pn);
                b.AddBox(at + Vector3.up * 0.05f, new Vector3(36, 0.1f, 18), asphalt, Quaternion.LookRotation(N, Vector3.up));
            }
            foreach (var (pe, pn) in new[] { (-100.0, 228.0), (210.0, 225.0) })
                for (int k = 0; k < 9; k++)
                {
                    if (rng.NextDouble() < 0.3) continue;
                    Vector3 at = At(pe - 14 + (k % 5) * 6.8, pn - 4 + (k / 5) * 8);
                    var car = Crowd.VehicleMesh("carLow", new[] { new Color(0.85f, 0.85f, 0.83f), new Color(0.2f, 0.25f, 0.35f), new Color(0.7f, 0.15f, 0.12f), new Color(0.12f, 0.45f, 0.52f) }[rng.Next(4)]);
                    var verts = car.vertices; var norms = car.normals; var cols = car.colors; var tris = car.triangles;
                    var uv = new System.Collections.Generic.List<Vector4>(); car.GetUVs(0, uv);
                    Quaternion q = Quaternion.LookRotation(N, Vector3.up);
                    int s0 = b.v.Count;
                    for (int i = 0; i < verts.Length; i++) { b.v.Add(at + q * verts[i]); b.n.Add(q * norms[i]); b.c.Add(cols[i]); }
                    b.AddRawUv(uv);
                    foreach (int t in tris) b.t.Add(s0 + t);
                }

            // Trees (milestone 24: open grass like captaincarl24's map, so fewer, in clumps, kept off everything) and palms along
            // the top of the beach
            var keep = new List<double[]>();   // strips: e0, n0, e1, n1, how far to keep clear
            foreach (var r in roads) keep.Add(new[] { r[0], r[1], r[2], r[3], r[4] * 0.5 + 14 });
            keep.Add(new[] { LaunchSite.RunwayEast - 1240, LaunchSite.RunwayNorth, LaunchSite.RunwayEast + 1240, LaunchSite.RunwayNorth, 80.0 });
            keep.Add(new[] { -460, -600, -460, 1140, 80.0 });
            keep.Add(new[] { -680, -690, -680, 1080, 80.0 });
            keep.Add(new[] { 1305, 150, 2192, -206, 34.0 });
            keep.Add(new[] { 1305, 150, 2037, -428, 34.0 });
            keep.Add(new[] { -1372, 1125, -1770, 1020, 34.0 });
            var areas = new[]
            {
                new double[] { -430, -190, 470, 440 }, new double[] { -440, 300, -230, 680 }, new double[] { -700, -600, -470, 540 },
                new double[] { -220, 760, 200, 990 }, new double[] { -310, 1010, -60, 1240 }, new double[] { 1150, -80, 1470, 300 },
                new double[] { 680, 260, 850, 340 }, new double[] { 1030, 260, 1160, 330 }, new double[] { -1480, 1020, -1220, 1240 },
                new double[] { -1360, 80, -1090, 310 }, new double[] { -950, 830, -840, 990 }, new double[] { -1140, 860, -1080, 930 },
            };
            var rounds = new[]
            {
                new double[] { 2192, -206, 105 }, new double[] { 2037, -428, 105 }, new double[] { 2066, -300, 55 }, new double[] { 2345, -268, 50 },
                new double[] { -1770, 1020, 105 }, new double[] { -1892, 982, 45 }, new double[] { 1228, -27, 48 }, new double[] { 1420, 253, 48 },
                new double[] { 5, -150, 45 }, new double[] { -1110, 895, 45 }, new double[] { 0, 0, 280 },
            };
            bool Clear(double e, double n)
            {
                foreach (var a in areas) if (e > a[0] && e < a[2] && n > a[1] && n < a[3]) return false;
                foreach (var a in keepClear) if (e > a[0] && e < a[2] && n > a[1] && n < a[3]) return false;
                foreach (var c in rounds) if ((e - c[0]) * (e - c[0]) + (n - c[1]) * (n - c[1]) < c[2] * c[2]) return false;
                foreach (var k in keep)
                {
                    double de = k[2] - k[0], dn = k[3] - k[1], len2 = de * de + dn * dn;
                    double t = len2 > 0 ? System.Math.Max(0, System.Math.Min(1, ((e - k[0]) * de + (n - k[1]) * dn) / len2)) : 0;
                    double pe = k[0] + de * t - e, pn = k[1] + dn * t - n;
                    if (pe * pe + pn * pn < k[4] * k[4]) return false;
                }
                return true;
            }
            float Ground(double e, double n) => Terrain.ArdenField(body, LaunchSite.DirectionAt(body, e, n).ToFloat());
            b.Mat = MeshBuilder.MatMatte;
            int planted = 0, palms = 0;
            for (int tries = 0; tries < 40000 && planted < 520; tries++)
            {
                double e = -2900 + rng.NextDouble() * 5800, n = -1700 + rng.NextDouble() * 4300;
                if (Mathf.PerlinNoise((float)e * 0.0035f + 13.1f, (float)n * 0.0035f + 7.7f) < 0.58f) continue;   // in clumps
                if (!Clear(e, n) || Ground(e, n) < 0.075f) continue;
                Conifer(b, At(e, n), 7f + (float)rng.NextDouble() * 10f, rng);
                planted++;
            }
            for (int tries = 0; tries < 30000 && palms < 60; tries++)
            {
                double e = -2900 + rng.NextDouble() * 5800, n = -1700 + rng.NextDouble() * 2600;
                float g = Ground(e, n);
                if (g < 0.02f || g > 0.06f || !Clear(e, n)) continue;
                Palm(b, At(e, n), 7f + (float)rng.NextDouble() * 5f, rng);
                palms++;
            }
        }

        // ---------------------------------------------------------------- the compound (milestone 24)

        /// <summary>
        /// One joined-up campus: every building stands on paved plazas (rounded, with a darker kerb and lawn islands with
        /// trees) linked by wide walkways with lamps. A round apron round the pad and a two-lane rocket road to the
        /// assembly building; a tracking station with three big dishes round its control building; a research campus with
        /// a skybridge between two labs and an observatory dome; the airport beside the runway with a big arched hangar,
        /// a control tower, an ops building and taxiways.
        /// </summary>
        void BuildCompound(MeshBuilder b, Vector3 E, Vector3 N, Color white, Color teal, Color steel, Color dark)
        {
            Color pave = new Color(0.64f, 0.64f, 0.63f), kerb = new Color(0.4f, 0.4f, 0.39f), lawn = new Color(0.82f, 0.86f, 0.76f);
            Color glass = new Color(0.1f, 0.16f, 0.22f), panel = new Color(0.8f, 0.77f, 0.71f), yellow = new Color(0.9f, 0.7f, 0.12f);
            Vector3 U = Vector3.up;
            Quaternion qN = Quaternion.LookRotation(N, U);
            Vector3 P(double e, double n, double h = 0) => At(e, n) + U * (float)h;
            void Box(double e, double n, double h, Vector3 size, Color c) => b.AddBox(P(e, n, h), size, c, qN);
            var rng = new System.Random(424);
            const float KerbH = 0.06f, PaveH = 0.09f, WalkH = 0.12f, LawnH = 0.15f;

            // A rounded rectangle (corner radius c) of paving with a kerb just outside it
            void Plaza(double e0, double n0, double e1, double n1, double c)
            {
                b.Mat = MeshBuilder.MatConcrete;
                PaveRect(b, e0 - 1.2, n0 - 1.2, e1 + 1.2, n1 + 1.2, c + 1.2, KerbH, kerb);
                b.Mat = MeshBuilder.MatPaving;
                PaveRect(b, e0, n0, e1, n1, c, PaveH, pave);
            }
            void Round(double e, double n, double r)
            {
                b.Mat = MeshBuilder.MatConcrete;
                PaveRect(b, e - r - 1.2, n - r - 1.2, e + r + 1.2, n + r + 1.2, r + 1.2, KerbH, kerb);
                b.Mat = MeshBuilder.MatPaving;
                PaveRect(b, e - r, n - r, e + r, n + r, r, PaveH, pave);
            }
            void Walk(double e0, double n0, double e1, double n1, double w, bool lamps = true)
            {
                b.Mat = MeshBuilder.MatConcrete;
                PaveStrip(b, e0, n0, e1, n1, w + 2.4, KerbH, kerb);
                b.Mat = MeshBuilder.MatPaving;
                PaveStrip(b, e0, n0, e1, n1, w, WalkH, pave);
                if (!lamps) return;
                double len = System.Math.Sqrt((e1 - e0) * (e1 - e0) + (n1 - n0) * (n1 - n0));
                double se = -(n1 - n0) / len, sn = (e1 - e0) / len;
                for (double t = 12; t < len - 6; t += 26)
                {
                    double f = t / len;
                    Lamp(e0 + (e1 - e0) * f + se * (w * 0.5 + 0.8), n0 + (n1 - n0) * f + sn * (w * 0.5 + 0.8));
                }
            }
            void Lamp(double e, double n)
            {
                b.Mat = MeshBuilder.MatDarkMetal;
                Box(e, n, 3f, new Vector3(0.15f, 6f, 0.15f), dark);
                Box(e, n, 6.05f, new Vector3(0.9f, 0.12f, 0.3f), dark);
                b.Mat = MeshBuilder.MatLamp;
                Box(e, n, 5.95f, new Vector3(0.7f, 0.06f, 0.22f), new Color(1f, 0.93f, 0.78f));
            }
            // A lawn island (rounded) with round-crowned trees
            void Lawn(double e0, double n0, double e1, double n1, double c, int trees)
            {
                b.Mat = MeshBuilder.MatLawn;
                PaveRect(b, e0, n0, e1, n1, c, LawnH, lawn);
                for (int k = 0; k < trees; k++)
                {
                    double e = e0 + c * 0.6 + rng.NextDouble() * System.Math.Max(0.1, e1 - e0 - c * 1.2);
                    double n = n0 + c * 0.6 + rng.NextDouble() * System.Math.Max(0.1, n1 - n0 - c * 1.2);
                    RoundTree(b, P(e, n, LawnH), 6f + (float)rng.NextDouble() * 4f, rng);
                }
            }

            // ---- The pad's apron, the rocket road and the small vehicle sheds south of the pad
            Round(0, 0, 96);
            b.Mat = MeshBuilder.MatConcrete;
            PaveRect(b, -34, -34, 34, 34, 34, PaveH + 0.02f, new Color(0.5f, 0.5f, 0.49f));          // scorched round the pad
            b.Mat = MeshBuilder.MatMatte;
            Annulus(b, P(0, 0, PaveH + 0.04f), 52f, 53.2f, 64, yellow);                                  // the keep-out ring
            foreach (int lane in new[] { -1, 1 }) Walk(lane * 9, 92, lane * 9, 272, 11, false);          // two crawler lanes
            b.Mat = MeshBuilder.MatLawn;
            PaveStrip(b, 0, 96, 0, 270, 6.2, WalkH - 0.02f, lawn);             // grass between them
            Plaza(52, -112, 122, -48, 10);                                                               // the sheds' yard

            // ---- South of the pad: a launch-control blockhouse on its own round
            Round(5, -150, 26);
            Walk(5, -124, 5, -97, 7, false);
            b.Mat = MeshBuilder.MatConcrete;
            Box(5, -150, PaveH + 2.6f, new Vector3(18f, 5.2f, 13f), new Color(0.6f, 0.6f, 0.58f));
            Box(5, -150, PaveH + 5.5f, new Vector3(19f, 0.6f, 14f), new Color(0.52f, 0.52f, 0.5f));
            b.Mat = MeshBuilder.MatDarkMetal;
            Box(5, -143.4, PaveH + 1.4f, new Vector3(3f, 2.8f, 0.3f), new Color(0.3f, 0.32f, 0.35f));
            b.Mat = MeshBuilder.MatMetal;
            Box(-2, -152, PaveH + 8f, new Vector3(0.18f, 5f, 0.18f), steel);

            // ---- The assembly building's plaza, the astronaut complex and mission control
            Plaza(-78, 268, 132, 412, 22);
            Round(150, 320, 40);
            Lawn(-70, 384, -40, 404, 8, 3);
            Lawn(104, 380, 126, 404, 8, 2);
            Plaza(138, 116, 206, 166, 12);
            Lawn(140, 118, 152, 128, 4, 1);
            Walk(170, 166, 170, 194, 9);
            Walk(40, 204, 40, 268, 12);                                                                  // from the promenade to the assembly building
            Walk(68, 66, 140, 118, 9);                                                                   // the pad apron to mission control
            Walk(-36, 86, -110, 194, 9);                                                                 // and to headquarters

            // ---- The campus core: paving between the road, the assembly building and mission control
            Plaza(-80, 208, 136, 270, 10);
            Plaza(18, 108, 136, 193, 10);
            Plaza(-78, 112, -18, 193, 10);
            Lawn(-70, 214, -38, 238, 8, 3);
            Lawn(56, 214, 96, 236, 8, 3);
            Lawn(100, 244, 130, 264, 7, 2);
            Lawn(24, 114, 60, 134, 8, 3);
            Lawn(-48, 116, -22, 140, 8, 3);
            Lawn(-72, 174, -44, 190, 7, 2);
            {
                // a fuel depot: three long tanks in a bunded yard
                b.Mat = MeshBuilder.MatConcrete;
                Box(96, 168, PaveH + 0.5f, new Vector3(34f, 1f, 22f), new Color(0.55f, 0.55f, 0.53f));
                for (int k = 0; k < 3; k++)
                {
                    b.Mat = MeshBuilder.MatGloss;
                    int s0 = b.v.Count;
                    b.AddFrustum(14f, -14f, 2.6f, 2.6f, white, 20, capTop: true, capBottom: true);
                    b.Transform(s0, qN * Quaternion.Euler(0, 0, 90f), P(96, 161 + k * 7, PaveH + 3.6f));
                    b.Mat = MeshBuilder.MatPaint;
                    s0 = b.v.Count;
                    b.AddFrustum(1f, -1f, 2.65f, 2.65f, teal, 20);
                    b.Transform(s0, qN * Quaternion.Euler(0, 0, 90f), P(96, 161 + k * 7, PaveH + 3.6f));
                    b.Mat = MeshBuilder.MatMetal;
                    foreach (double de in new[] { -9.0, 0.0, 9.0 }) Box(96 + de, 161 + k * 7, PaveH + 1.3f, new Vector3(0.8f, 1.6f, 4f), steel);
                }
            }
            {
                // a maintenance shed with roller doors
                b.Mat = MeshBuilder.MatFacade(MeshBuilder.Cladding);
                Box(-48, 160, PaveH + 4f, new Vector3(36f, 8f, 18f), new Color(0.66f, 0.6f, 0.5f));
                b.Mat = MeshBuilder.MatPaint;
                Box(-48, 160, PaveH + 8.3f, new Vector3(36.6f, 0.6f, 18.6f), teal);
                b.Mat = MeshBuilder.MatDarkMetal;
                for (int k = 0; k < 3; k++) Box(-60 + k * 12, 150.8, PaveH + 2.6f, new Vector3(8f, 5.2f, 0.3f), new Color(0.42f, 0.44f, 0.47f));
            }
            {
                // a cafe pavilion: a round glass room under a wide white disc roof
                Vector3 c = P(75, 252, PaveH);
                b.Mat = MeshBuilder.MatGlass;
                int s0 = b.v.Count;
                b.AddFrustum(4f, 0f, 9f, 9f, glass, 32);
                b.Transform(s0, Quaternion.identity, c);
                b.Mat = MeshBuilder.MatPaint;
                s0 = b.v.Count;
                b.AddFrustum(4.6f, 4f, 13f, 12.6f, panel, 32, capTop: true, capBottom: true);
                b.Transform(s0, Quaternion.identity, c);
                b.Mat = MeshBuilder.MatMatte;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    Vector3 tp = c + (E * Mathf.Cos(a) + N * Mathf.Sin(a)) * 11.5f;
                    b.AddBox(tp + U * 0.75f, new Vector3(1f, 0.06f, 1f), panel, qN);
                    b.AddBox(tp + U * 0.38f, new Vector3(0.08f, 0.75f, 0.08f), dark, qN);
                }
            }

            // ---- Avenue trees along both sides of the campus road (gaps where paths and roads cross)
            foreach (double tn in new[] { 189.5, 210.6 })
                for (double te = -300; te <= 326; te += 15)
                {
                    bool gap = System.Math.Abs(te + 300) < 9 || System.Math.Abs(te + 90) < 10 || System.Math.Abs(te - 40) < 10 || System.Math.Abs(te) < 18
                        || System.Math.Abs(te - 150) < 8 || System.Math.Abs(te - 170) < 8 || System.Math.Abs(te - 300) < 9 || (te > -196 && te < -138) || (tn < 200 && te > -112 && te < -100);
                    if (!gap) RoundTree(b, P(te, tn, 0), 7.5f + (float)rng.NextDouble() * 2.5f, rng);
                }

            // ---- The promenade along the campus road: pavements on both sides
            Walk(-312, 205.6, 332, 205.6, 3.4, true);
            Walk(-312, 194.4, 332, 194.4, 3.4, false);
            Plaza(-240, 238, -118, 302, 10);                                                             // headquarters' back yard

            // ---- Tracking station (north of the campus on captaincarl24's map): a round plaza with its control building, three
            // dishes on their own pads, and a white radome beside it
            const double TsE = -200, TsN = 1100;
            Round(TsE, TsN, 27);
            Walk(TsE, TsN + 27, TsE, 1248, 8);
            for (int k = 0; k < 3; k++)
            {
                double a = (30 + k * 120) * System.Math.PI / 180;
                double de = System.Math.Cos(a) * 58, dn = System.Math.Sin(a) * 58;
                Round(TsE + de, TsN + dn, 17);
                Walk(TsE + de * 0.45, TsN + dn * 0.45, TsE + de * 0.75, TsN + dn * 0.75, 6);
                BigDish(b, P(TsE + de, TsN + dn, PaveH), 13f, (float)(k * 120 + 25), 38f + k * 12f, E, N, white, steel, dark);
            }
            {
                Vector3 c = P(TsE, TsN, PaveH);
                b.Mat = MeshBuilder.MatMatte;
                int s0 = b.v.Count;
                b.AddFrustum(9f, 0f, 13f, 13f, panel, 8, capTop: true);
                b.Transform(s0, Quaternion.Euler(0, 22.5f, 0), c);
                b.Mat = MeshBuilder.MatGlass;
                s0 = b.v.Count;
                b.AddFrustum(6.6f, 3.4f, 13.1f, 13.1f, glass, 8);
                b.Transform(s0, Quaternion.Euler(0, 22.5f, 0), c);
                b.Mat = MeshBuilder.MatPaint;
                s0 = b.v.Count;
                b.AddFrustum(9.6f, 8.8f, 13.6f, 13.6f, teal, 8, capTop: true);
                b.Transform(s0, Quaternion.Euler(0, 22.5f, 0), c);
                s0 = b.v.Count;
                b.AddFrustum(12.5f, 9.6f, 5f, 6f, panel, 8, capTop: true);                               // a rooftop operations room
                b.Transform(s0, Quaternion.Euler(0, 22.5f, 0), c);
                b.Mat = MeshBuilder.MatGlass;
                s0 = b.v.Count;
                b.AddFrustum(11.8f, 10.2f, 5.1f, 5.9f, glass, 8);
                b.Transform(s0, Quaternion.Euler(0, 22.5f, 0), c);
                BigDish(b, c + U * 12.5f, 2.6f, 200f, 50f, E, N, white, steel, dark);
            }
            {
                // the radome: a faceted white ball on a short drum
                Round(-92, 1168, 22);
                Walk(-92, 1146, -150, 1112, 6);
                Vector3 c = P(-92, 1168, PaveH);
                b.Mat = MeshBuilder.MatMatte;
                int s0 = b.v.Count;
                b.AddFrustum(3.2f, 0f, 12.6f, 13f, panel, 24, capTop: true);
                b.Transform(s0, Quaternion.identity, c);
                b.Mat = MeshBuilder.MatPaint;
                s0 = b.v.Count;
                b.AddFrustum(3.6f, 3.2f, 12.8f, 12.8f, teal, 24);
                b.Transform(s0, Quaternion.identity, c);
                b.Mat = MeshBuilder.MatGloss;
                b.AddSphere(c + U * 12.4f, 15.5f, new Color(0.9f, 0.92f, 0.95f), 14, 9, Mathf.PI * 0.7f);
            }

            // ---- Research campus (out beyond the runways on captaincarl24's map): two labs joined by a glass skybridge, a workshop,
            // an observatory dome. Drawn round its old spot and moved over by (RE, RN).
            const double RE = -1590, RN = 90;
            Plaza(RE + 272, RN + 22, RE + 452, RN + 176, 16);
            Walk(RE + 300, RN + 176, RE + 300, RN + 218, 10);                                                                // up to the road
            Walk(RE + 452, RN + 160, RE + 674, RN + 160, 8);                                                                 // over to the far-side road
            Lawn(RE + 380, RN + 30, RE + 446, RN + 52, 8, 4);
            Lawn(RE + 276, RN + 26, RE + 300, RN + 46, 6, 2);
            // An office or lab block (milestone 25: a real roof and an entrance): the facade (its top draws as a gravel roof), a
            // darker plinth, a parapet with a coping round the roof, rooftop plant with fan grilles, a stair housing, and glass
            // doors under a canopy on the south side
            void Lab(double e, double n, float w, float d, int floors, float style = MeshBuilder.Ribbon)
            {
                float hgt = floors * 4f + 0.8f;
                Color wall = WallTint(e, n), coping = new Color(0.36f, 0.37f, 0.38f);
                b.Mat = MeshBuilder.MatFacade(style);
                Box(e, n, PaveH + hgt * 0.5f, new Vector3(w, hgt, d), wall);
                b.Mat = MeshBuilder.MatConcrete;
                Box(e, n, PaveH + 0.45f, new Vector3(w + 0.3f, 0.9f, d + 0.3f), wall * 0.62f);
                b.Mat = MeshBuilder.MatPaint;
                foreach (int sgn in new[] { -1, 1 })
                {
                    Box(e, n + sgn * (d * 0.5f - 0.15f), PaveH + hgt + 0.45f, new Vector3(w + 0.2f, 0.9f, 0.3f), wall * 0.92f);
                    Box(e + sgn * (w * 0.5f - 0.15f), n, PaveH + hgt + 0.45f, new Vector3(0.3f, 0.9f, d + 0.2f), wall * 0.92f);
                    Box(e, n + sgn * (d * 0.5f - 0.15f), PaveH + hgt + 0.96f, new Vector3(w + 0.4f, 0.12f, 0.45f), coping);
                    Box(e + sgn * (w * 0.5f - 0.15f), n, PaveH + hgt + 0.96f, new Vector3(0.45f, 0.12f, d + 0.4f), coping);
                }
                int units = Mathf.Clamp((int)(w / 12f), 1, 4);
                for (int k = 0; k < units; k++)
                {
                    double ux = units == 1 ? e : e - w * 0.35 + k * w * 0.7 / (units - 1);
                    b.Mat = MeshBuilder.MatMetal;
                    Box(ux, n + d * 0.18, PaveH + hgt + 0.9f, new Vector3(3.2f, 1.8f, 2.4f), new Color(0.62f, 0.63f, 0.64f));
                    b.Mat = MeshBuilder.MatDarkMetal;
                    Box(ux, n + d * 0.18, PaveH + hgt + 1.84f, new Vector3(1.6f, 0.1f, 1.6f), dark);
                }
                b.Mat = MeshBuilder.MatFacade(MeshBuilder.Cladding);
                Box(e + w * 0.3, n - d * 0.22, PaveH + hgt + 1.4f, new Vector3(4f, 2.8f, 3.4f), wall * 0.9f);
                b.Mat = MeshBuilder.MatGlass;
                Box(e - w * 0.18, n - d * 0.5 - 0.06, PaveH + 1.45f, new Vector3(4.2f, 2.8f, 0.12f), glass);
                b.Mat = MeshBuilder.MatPaint;
                Box(e - w * 0.18, n - d * 0.5 - 1.3, PaveH + 3.1f, new Vector3(6f, 0.3f, 2.6f), coping);
                b.Mat = MeshBuilder.MatMatte;
            }
            Lab(RE + 310, RN + 146, 42, 24, 3);
            Lab(RE + 406, RN + 146, 36, 22, 2, MeshBuilder.Curtain);
            {
                // the skybridge: a glass corridor on pillars at second-floor height
                b.Mat = MeshBuilder.MatGlass;
                Box(RE + 359, RN + 146, PaveH + 7.6f, new Vector3(58, 3.2f, 4f), new Color(0.26f, 0.42f, 0.46f));
                b.Mat = MeshBuilder.MatPaint;
                Box(RE + 359, RN + 146, PaveH + 9.35f, new Vector3(58, 0.4f, 4.4f), panel);
                Box(RE + 359, RN + 146, PaveH + 5.85f, new Vector3(58, 0.4f, 4.4f), panel);
                for (int k = 0; k < 4; k++) Box(RE + 338 + k * 14, RN + 146, PaveH + 2.8f, new Vector3(0.7f, 5.6f, 0.7f), panel);
            }
            {
                // the workshop: a long low shed with a sawtooth roof of north lights
                b.Mat = MeshBuilder.MatFacade(MeshBuilder.Cladding);
                Box(RE + 318, RN + 74, PaveH + 3.5f, new Vector3(52, 7f, 28), new Color(0.62f, 0.63f, 0.6f));
                for (int k = 0; k < 6; k++)
                {
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddBox(P(RE + 296.7 + k * 8.6, RN + 74, PaveH + 8.2f), new Vector3(8.6f, 0.3f, 28.2f) , new Color(0.7f, 0.71f, 0.73f), qN * Quaternion.Euler(0, 0, 18f));
                    b.Mat = MeshBuilder.MatGlass;
                    b.AddBox(P(RE + 300.6 + k * 8.6, RN + 74, PaveH + 8.3f), new Vector3(0.2f, 2.6f, 27.6f), glass, qN);
                }
                b.Mat = MeshBuilder.MatDarkMetal;
                Box(RE + 318, RN + 59.8, PaveH + 2.8f, new Vector3(10f, 5.6f, 0.3f), new Color(0.25f, 0.27f, 0.3f));      // roller door
            }
            {
                // the observatory: a drum with a white dome and its slit, a little stair up the side
                Vector3 c = P(RE + 408, RN + 82, PaveH);
                b.Mat = MeshBuilder.MatMatte;
                int s0 = b.v.Count;
                b.AddFrustum(8f, 0f, 11f, 11f, panel, 36, capTop: true);
                b.Transform(s0, Quaternion.identity, c);
                b.Mat = MeshBuilder.MatPaint;
                s0 = b.v.Count;
                b.AddFrustum(8.4f, 7.9f, 11.4f, 11.4f, teal, 36);
                b.Transform(s0, Quaternion.identity, c);
                b.Mat = MeshBuilder.MatGloss;
                b.AddSphere(c + U * 8f, 10.6f, new Color(0.93f, 0.94f, 0.95f), 36, 12, Mathf.PI * 0.5f);
                b.Mat = MeshBuilder.MatDarkMetal;
                Vector3 slitDir = (E * 0.6f + N * 0.8f).normalized;
                for (int k = 0; k < 12; k++)
                {
                    float th = k / 11f * Mathf.PI * 0.5f;
                    Vector3 p = c + U * 8f + (slitDir * Mathf.Cos(th) + U * Mathf.Sin(th)) * 10.65f;
                    b.AddBox(p, new Vector3(2.4f, 0.1f, 1.5f), new Color(0.08f, 0.09f, 0.1f), Quaternion.LookRotation(Vector3.Cross(slitDir, U), slitDir * Mathf.Cos(th) + U * Mathf.Sin(th)));
                }
            }

            // ================================================================ captaincarl24's map (milestone 24)
            // Two parallel runways west of the campus, crossed near their south ends by the main east-west runway (the sim's,
            // drawn by BuildRunway); the airport between them and the campus; helipads by mission control; a launch complex
            // out east with two pads near the coast; a north-west complex with its own pad. (East is +e: the game draws it
            // on the left when you look north, so this matches the map seen from the sea in the south.)
            const double R1 = -460, R2 = -680;

            void RunwayStrip(double e0, double n0, double e1, double n1, double w)
            {
                double len = System.Math.Sqrt((e1 - e0) * (e1 - e0) + (n1 - n0) * (n1 - n0));
                double ue = (e1 - e0) / len, un = (n1 - n0) / len, se = -un, sn = ue;
                b.Mat = MeshBuilder.MatConcrete;
                PaveStrip(b, e0 - ue * 3, n0 - un * 3, e1 + ue * 3, n1 + un * 3, w + 8, KerbH, kerb);
                b.Mat = MeshBuilder.MatAsphalt;
                float rh = (float)LaunchSite.RunwayHeight;
                PaveStrip(b, e0, n0, e1, n1, w, rh - 0.005f, new Color(0.42f, 0.43f, 0.44f));
                b.Mat = MeshBuilder.MatMatte;
                Color paint = new Color(0.9f, 0.9f, 0.88f);
                for (double t = 60; t < len - 60; t += 60)
                {
                    double mn = n0 + un * (t + 15);
                    if (System.Math.Abs(mn - LaunchSite.RunwayNorth) < 40) continue;   // no centre line across the main runway
                    PaveStrip(b, e0 + ue * t, n0 + un * t, e0 + ue * (t + 30), n0 + un * (t + 30), 1.0, rh + 0.025f, paint);
                }
                foreach (double t0 in new[] { 8.0, len - 38 })
                    for (int k = -4; k <= 4; k++)
                    {
                        if (k == 0) continue;
                        double oe = se * k * 3.6, on = sn * k * 3.6;
                        PaveStrip(b, e0 + ue * t0 + oe, n0 + un * t0 + on, e0 + ue * (t0 + 30) + oe, n0 + un * (t0 + 30) + on, 2.0, rh + 0.025f, paint);
                    }
                b.Mat = MeshBuilder.MatLamp;
                for (double t = 0; t <= len; t += 60)
                    foreach (int sgn in new[] { -1, 1 })
                        Box(e0 + ue * t + se * sgn * (w * 0.5 + 2), n0 + un * t + sn * sgn * (w * 0.5 + 2), PaveH + 0.25f, new Vector3(0.35f, 0.5f, 0.35f), new Color(1f, 0.95f, 0.8f));
                b.Mat = MeshBuilder.MatMatte;
            }
            void Taxiway(double e0, double n0, double e1, double n1)
            {
                b.Mat = MeshBuilder.MatAsphalt;
                PaveStrip(b, e0, n0, e1, n1, 20, PaveH + 0.005f, new Color(0.46f, 0.47f, 0.48f));
                b.Mat = MeshBuilder.MatMatte;
                PaveStrip(b, e0, n0, e1, n1, 0.5, PaveH + 0.035f, yellow);
            }
            // A plane parked on an apron, nose along 'dir'
            void Plane(double e, double n, Vector3 dir)
            {
                Vector3 at = P(e, n, PaveH + 1.6f);
                Quaternion q = Quaternion.LookRotation(dir, U);
                b.Mat = MeshBuilder.MatGloss;
                int s0 = b.v.Count;
                b.AddFrustum(5f, -5f, 0.9f, 0.9f, white, 18, capTop: true, capBottom: true);
                b.AddSphere(new Vector3(0, 5f, 0), 0.9f, white, 18, 6, Mathf.PI * 0.5f);
                b.Transform(s0, q * Quaternion.Euler(90f, 0, 0), at);
                b.AddBox(at + U * 0.2f, new Vector3(12f, 0.15f, 1.6f), white, q);
                b.AddBox(at - dir * 4.6f + U * 1.4f, new Vector3(0.15f, 2.4f, 1.6f), teal, q);
                b.AddBox(at - dir * 4.6f + U * 0.3f, new Vector3(4f, 0.12f, 1.2f), white, q);
                b.Mat = MeshBuilder.MatGlass;
                b.AddBox(at + dir * 4.3f + U * 0.55f, new Vector3(1.2f, 0.35f, 1.0f), glass, q);
            }
            // A small hangar with a roller door on the side facing 'door'
            // A metal-clad shed (milestone 25: a pitched roof and some life): cladding walls, a low pitched roof with its gables
            // and gutters, a big roller door in a frame on the 'door' side with a person door beside it, and a window strip
            void SmallHangar(double e, double n, float w, float d, float h, Vector3 door)
            {
                Quaternion q = Quaternion.LookRotation(door, U);
                Vector3 at = P(e, n, PaveH), fz = door.normalized, fx = Vector3.Cross(U, fz).normalized;
                Color clad = CladTint(e, n), roofC = new Color(0.4f, 0.41f, 0.42f), trim = new Color(0.3f, 0.31f, 0.33f);
                b.Mat = MeshBuilder.MatFacade(MeshBuilder.Cladding);
                b.AddBox(at + U * (h * 0.5f), new Vector3(w, h, d), clad, q);
                // the roof: two slopes meeting in a ridge along the width, eaves sticking out a little
                float rise = Mathf.Min(d * 0.14f, 3f), hw = w * 0.5f + 0.4f, hd = d * 0.5f + 0.5f;
                Vector3 top = at + U * h;
                Vector3 A = top - fx * hw + fz * hd, B = top + fx * hw + fz * hd, C = top + fx * hw - fz * hd, D = top - fx * hw - fz * hd;
                Vector3 R1 = top - fx * hw + U * rise, R2 = top + fx * hw + U * rise;
                b.Mat = MeshBuilder.MatMetal;
                b.AddQuad(A, B, R2, R1, (fz * rise + U * hd).normalized, roofC);
                b.AddQuad(C, D, R1, R2, (-fz * rise + U * hd).normalized, roofC);
                b.Mat = MeshBuilder.MatFacade(MeshBuilder.Cladding);
                foreach (int sgn in new[] { -1, 1 })
                    b.AddTri(top + fx * (sgn * w * 0.5f) + fz * (d * 0.5f), top + fx * (sgn * w * 0.5f) - fz * (d * 0.5f), top + fx * (sgn * w * 0.5f) + U * (rise * d * 0.5f / hd), fx * sgn, clad);
                b.Mat = MeshBuilder.MatPaint;
                foreach (int sgn in new[] { -1, 1 }) b.AddBox(top + fz * (sgn * (hd - 0.1f)) - U * 0.15f, new Vector3(w + 0.9f, 0.25f, 0.3f), trim, q);
                // the roller door in its frame, a person door beside it
                float dw = Mathf.Min(w * 0.62f, 14f), dh = Mathf.Min(h * 0.72f, 7f);
                b.Mat = MeshBuilder.MatDarkMetal;
                b.AddBox(at + U * (dh * 0.5f) + fz * (d * 0.5f + 0.12f), new Vector3(dw, dh, 0.25f), new Color(0.46f, 0.48f, 0.5f), q);
                for (float y = 0.6f; y < dh; y += 0.6f) b.AddBox(at + U * y + fz * (d * 0.5f + 0.26f), new Vector3(dw, 0.05f, 0.04f), new Color(0.34f, 0.36f, 0.38f), q);
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(at + U * (dh + 0.25f) + fz * (d * 0.5f + 0.2f), new Vector3(dw + 0.8f, 0.5f, 0.35f), trim, q);
                foreach (int sgn in new[] { -1, 1 }) b.AddBox(at + U * (dh * 0.5f) + fx * (sgn * (dw * 0.5f + 0.2f)) + fz * (d * 0.5f + 0.2f), new Vector3(0.4f, dh + 0.5f, 0.35f), trim, q);
                if (w * 0.5f - dw * 0.5f > 2.6f)
                {
                    b.AddBox(at + U * 1.05f + fx * (dw * 0.5f + 1.7f) + fz * (d * 0.5f + 0.1f), new Vector3(1f, 2.1f, 0.1f), trim, q);
                    b.Mat = MeshBuilder.MatLamp;
                    b.AddBox(at + U * 2.6f + fx * (dw * 0.5f + 1.7f) + fz * (d * 0.5f + 0.15f), new Vector3(0.35f, 0.15f, 0.12f), new Color(1f, 0.92f, 0.75f), q);
                }
                // a strip of windows high along both long sides
                b.Mat = MeshBuilder.MatGlass;
                foreach (int sgn in new[] { -1, 1 }) b.AddBox(at + U * (h * 0.78f) + fx * (sgn * (w * 0.5f + 0.05f)), new Vector3(0.1f, Mathf.Min(1.2f, h * 0.12f), d * 0.7f), glass, q);
                b.Mat = MeshBuilder.MatMatte;
            }

            // ---- The runways and taxiways
            RunwayStrip(R1, -560, R1, 1100, 40);
            RunwayStrip(R2, -650, R2, 1040, 40);
            Taxiway(-415, -300, -415, 1060);                                                             // alongside runway 1
            foreach (double tn in new[] { -300.0, 300.0, 640.0, 1040.0 }) Taxiway(-415, tn, R1, tn);
            Taxiway(R1, 500, R2, 500);                                                                   // across, between the two
            Taxiway(R1, -500, -490, -500);

            // ---- The airport between runway 1 and the campus: an arched hangar facing the runway, the control tower, an
            // operations building, the apron with a plane on it
            Plaza(-410, 400, -335, 650, 12);
            Plaza(-345, 410, -255, 512, 8);
            ArchHangar(b, P(-300, 460, PaveH), 76f, 70f, 27f, N, -E, panel, glass, steel, dark, teal);
            Plaza(-302, 552, -258, 598, 6);
            {
                // the control tower: a tapering shaft, a flared glass cab, a beacon
                Vector3 at = P(-280, 575, PaveH);
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(at + U * 3.5f, new Vector3(22f, 7f, 14f), panel, qN);
                int s0 = b.v.Count;
                b.AddFrustum(38f, 0f, 3.2f, 4.4f, panel, 12);
                b.Transform(s0, Quaternion.identity, at);
                b.Mat = MeshBuilder.MatPaint;
                s0 = b.v.Count;
                b.AddFrustum(38.6f, 37.6f, 6.4f, 4.6f, teal, 12, capBottom: true);
                b.Transform(s0, Quaternion.identity, at);
                b.Mat = MeshBuilder.MatGlass;
                s0 = b.v.Count;
                b.AddFrustum(43.4f, 38.6f, 6.8f, 6.0f, glass, 12);
                b.Transform(s0, Quaternion.identity, at);
                b.Mat = MeshBuilder.MatPaint;
                s0 = b.v.Count;
                b.AddFrustum(44.4f, 43.4f, 7.2f, 7.2f, panel, 12, capTop: true);
                b.Transform(s0, Quaternion.identity, at);
                b.Mat = MeshBuilder.MatMetal;
                b.AddBox(at + U * 48f, new Vector3(0.2f, 7f, 0.2f), steel, Quaternion.identity);
                b.Mat = MeshBuilder.MatLamp;
                b.AddSphere(at + U * 51.6f, 0.4f, new Color(1f, 0.2f, 0.15f), 8, 5);
            }
            Plaza(-322, 328, -250, 392, 8);
            Lab(-288, 360, 40, 50, 2, MeshBuilder.Punched);                                              // operations and terminal
            Plane(-378, 575, -E);
            Plane(-372, 430, -E);
            b.Mat = MeshBuilder.MatMatte;
            PaveStrip(b, -400, 410, -400, 640, 0.5, PaveH + 0.03f, yellow);                               // taxi line along the apron

            // ---- Between the runways: a small apron with two hangars; south of them a fire station
            Plaza(-650, 380, -490, 470, 10);
            SmallHangar(-618, 402, 24f, 26f, 11f, E);
            SmallHangar(-618, 448, 24f, 26f, 11f, E);
            Plaza(-650, -560, -490, -462, 10);
            {
                b.Mat = MeshBuilder.MatFacade(MeshBuilder.Cladding);
                Box(-570, -522, PaveH + 4.5f, new Vector3(44f, 9f, 18f), new Color(0.62f, 0.32f, 0.26f));
                b.Mat = MeshBuilder.MatPaint;
                Box(-570, -522, PaveH + 9.3f, new Vector3(44.6f, 0.6f, 18.6f), teal);
                for (int k = 0; k < 4; k++) Box(-586 + k * 10.6, -512.8, PaveH + 2.8f, new Vector3(7.6f, 5.6f, 0.3f), new Color(0.62f, 0.12f, 0.1f));
                b.Mat = MeshBuilder.MatMatte;
            }

            // ---- Helipads south-east of mission control: three pads, a hangar for the helicopters, one parked
            void Helipad(double e, double n)
            {
                b.Mat = MeshBuilder.MatConcrete;
                PaveRect(b, e - 13, n - 13, e + 13, n + 13, 13, PaveH + 0.02f, new Color(0.46f, 0.47f, 0.47f));
                b.Mat = MeshBuilder.MatMatte;
                Annulus(b, P(e, n, PaveH + 0.05f), 10.4f, 11f, 40, Color.white);
                Annulus(b, P(e, n, PaveH + 0.05f), 12.2f, 12.8f, 40, yellow);
                foreach (int sgn in new[] { -1, 1 }) Box(e + sgn * 2.4, n, PaveH + 0.06f, new Vector3(1f, 0.02f, 7f), Color.white);
                Box(e, n, PaveH + 0.06f, new Vector3(4f, 0.02f, 1f), Color.white);
            }
            void Ellipsoid(Vector3 c0, Vector3 r, Quaternion rot, Color col)
            {
                int s0 = b.v.Count;
                b.AddSphere(Vector3.zero, 1f, col, 16, 10);
                for (int i = s0; i < b.v.Count; i++)
                {
                    Vector3 v = b.v[i], nn = b.n[i];
                    b.v[i] = c0 + rot * Vector3.Scale(v, r);
                    b.n[i] = rot * new Vector3(nn.x / r.x, nn.y / r.y, nn.z / r.z).normalized;
                }
            }
            void Helicopter(double e, double n)
            {
                Vector3 at = P(e, n, PaveH + 0.05f), side = Vector3.Cross(U, N).normalized;
                b.Mat = MeshBuilder.MatGloss;
                Ellipsoid(at + U * 1.9f, new Vector3(1.25f, 1.15f, 2.5f), qN, white);
                b.AddBox(at + U * 2.2f - N * 4.6f, new Vector3(0.4f, 0.45f, 5.6f), white, qN);
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(at + U * 3.0f - N * 7.2f, new Vector3(0.15f, 1.7f, 1.1f), teal, qN);
                b.AddBox(at + U * 1.9f, new Vector3(2.56f, 0.35f, 3.2f), teal, qN);
                b.Mat = MeshBuilder.MatGlass;
                Ellipsoid(at + U * 2.15f + N * 1.25f, new Vector3(1.05f, 0.85f, 1.45f), qN, glass);
                b.Mat = MeshBuilder.MatDarkMetal;
                b.AddBox(at + U * 3.3f, new Vector3(0.25f, 0.6f, 0.25f), dark, qN);
                b.AddBox(at + U * 3.6f, new Vector3(11f, 0.06f, 0.36f), dark, qN * Quaternion.Euler(0, 30f, 0));
                b.AddBox(at + U * 3.6f, new Vector3(11f, 0.06f, 0.36f), dark, qN * Quaternion.Euler(0, 120f, 0));
                b.AddBox(at + U * 3.1f - N * 7.2f + side * 0.15f, new Vector3(0.05f, 1.7f, 0.2f), dark, qN);
                foreach (int sgn in new[] { -1, 1 })
                {
                    b.AddBox(at + U * 0.12f + side * (sgn * 1.15f), new Vector3(0.12f, 0.12f, 4.4f), dark, qN);
                    foreach (int f in new[] { -1, 1 }) b.AddBox(at + U * 0.5f + side * (sgn * 1.0f) + N * (f * 1.1f), new Vector3(0.1f, 0.8f, 0.1f), dark, qN);
                }
            }
            Plaza(236, 10, 436, 118, 30);
            Helipad(272, 54); Helipad(338, 66); Helipad(402, 48);
            Helicopter(338, 66);
            SmallHangar(338, 104, 34f, 12f, 9f, -N);
            Walk(206, 140, 252, 108, 6);

            // ---- A launch pad of its own: apron, scorched deck, keep-out ring, a red lattice service tower (on the side of
            // 'towerSide') with walkways and a swing arm, floodlight masts at the corners and a lightning mast
            void ExtraPad(double e, double n, Vector3 towerSide)
            {
                Round(e, n, 62);
                b.Mat = MeshBuilder.MatConcrete;
                PaveRect(b, e - 26, n - 26, e + 26, n + 26, 26, PaveH + 0.02f, new Color(0.5f, 0.5f, 0.49f));
                b.Mat = MeshBuilder.MatMatte;
                GroundRing(b, e, n, 40, 41.2, PaveH + 0.04f, 56, yellow);
                Vector3 c0 = P(e, n, PaveH);
                Quaternion q = Quaternion.LookRotation(towerSide, U);
                Vector3 fz = towerSide.normalized, fx = Vector3.Cross(U, fz).normalized;
                Color red = new Color(0.55f, 0.1f, 0.07f);
                float deck = (float)LaunchSite.PadHeight - PaveH;   // the deck's top, above the apron
                b.Mat = MeshBuilder.MatConcrete;
                b.AddBox(c0 + U * (deck - 1f), new Vector3(24f, 2f, 24f), new Color(0.62f, 0.62f, 0.6f), q);
                b.Mat = MeshBuilder.MatDarkMetal;
                b.AddBox(c0 + U * (deck + 0.03f), new Vector3(3.2f, 0.06f, 3.2f), dark * 2f, q);
                b.AddBox(c0 + U * (deck + 0.02f) - fz * 6.5f, new Vector3(3.6f, 0.03f, 10f), new Color(0.12f, 0.12f, 0.12f), q);
                b.Mat = MeshBuilder.MatMatte;
                foreach (int sgn in new[] { -1, 1 })
                {
                    b.AddBox(c0 + U * (deck + 0.02f) + fz * (sgn * 11.4f), new Vector3(23f, 0.02f, 0.25f), yellow, q);
                    b.AddBox(c0 + U * (deck + 0.02f) + fx * (sgn * 11.4f), new Vector3(0.25f, 0.02f, 23f), yellow, q);
                }
                b.Mat = MeshBuilder.MatPaint;
                Vector3 tb = c0 + U * deck + fz * 7.5f;
                const float tw = 2.4f, th = 30f;
                for (int cx = -1; cx <= 1; cx += 2)
                    for (int cz = -1; cz <= 1; cz += 2)
                        b.AddBox(tb + fx * (cx * tw * 0.5f) + fz * (cz * tw * 0.5f) + U * (th * 0.5f), new Vector3(0.28f, th, 0.28f), red, q);
                float diag = Mathf.Sqrt(tw * tw + 9f), ang = Mathf.Atan2(3f, tw) * Mathf.Rad2Deg;
                for (float y = 0; y <= th + 0.01f; y += 3f)
                    foreach (int sgn in new[] { -1, 1 })
                    {
                        b.AddBox(tb + U * y + fz * (sgn * tw * 0.5f), new Vector3(tw, 0.16f, 0.16f), red, q);
                        b.AddBox(tb + U * y + fx * (sgn * tw * 0.5f), new Vector3(0.16f, 0.16f, tw), red, q);
                        if (y + 3f <= th + 0.01f)
                        {
                            b.AddBox(tb + U * (y + 1.5f) + fz * (sgn * tw * 0.5f), new Vector3(diag, 0.1f, 0.1f), red, q * Quaternion.Euler(0, 0, sgn * ang));
                            b.AddBox(tb + U * (y + 1.5f) + fx * (sgn * tw * 0.5f), new Vector3(0.1f, 0.1f, diag), red, q * Quaternion.Euler(sgn * ang, 0, 0));
                        }
                    }
                b.Mat = MeshBuilder.MatMetal;
                for (int i = 0; i < 5; i++) b.AddBox(tb + U * (3f + i * 5.5f), new Vector3(2.9f, 0.18f, 2.9f), steel, q);
                b.AddBox(tb + U * (th + 4f), new Vector3(0.12f, 8f, 0.12f), steel, q);
                b.Mat = MeshBuilder.MatPaint;
                // (the swing arms are their own objects: BuildPadArms)
                b.Mat = MeshBuilder.MatLamp;
                b.AddSphere(tb + U * (th + 8.2f), 0.18f, new Color(1f, 0.15f, 0.1f), 10, 6);
                foreach (var (cx, cz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                {
                    Vector3 mp = c0 + fx * (cx * 24f) + fz * (cz * 24f);
                    Vector3 toward = (c0 - mp).normalized;
                    toward = (toward - U * Vector3.Dot(toward, U)).normalized;
                    b.Mat = MeshBuilder.MatMetal;
                    b.AddBox(mp + U * 11f, new Vector3(0.35f, 22f, 0.35f), steel, q);
                    b.AddBox(mp + U * 22.2f, new Vector3(2.4f, 0.8f, 0.5f), dark * 2f, Quaternion.LookRotation(toward, U));
                    b.Mat = MeshBuilder.MatLamp;
                    b.AddBox(mp + U * 22.2f + toward * 0.27f, new Vector3(2.1f, 0.55f, 0.05f), new Color(1f, 0.97f, 0.9f), Quaternion.LookRotation(toward, U));
                }
                b.Mat = MeshBuilder.MatMatte;
            }
            // Two lanes for the crawler, grass between, from (e0, n0) to (e1, n1)
            void RocketRoad(double e0, double n0, double e1, double n1)
            {
                double len = System.Math.Sqrt((e1 - e0) * (e1 - e0) + (n1 - n0) * (n1 - n0));
                double se = -(n1 - n0) / len, sn = (e1 - e0) / len;
                foreach (int lane in new[] { -1, 1 }) Walk(e0 + se * lane * 9, n0 + sn * lane * 9, e1 + se * lane * 9, n1 + sn * lane * 9, 11, false);
                b.Mat = MeshBuilder.MatLawn;
                PaveStrip(b, e0, n0, e1, n1, 6.2, WalkH - 0.02f, lawn);
            }
            void SphereTank(double e, double n, float r, Color col)
            {
                float hc = r + 3f;
                b.Mat = MeshBuilder.MatGloss;
                b.AddSphere(P(e, n, PaveH + hc), r, col, 24, 14);
                b.Mat = MeshBuilder.MatMetal;
                for (int k = 0; k < 6; k++)
                {
                    Quaternion qq = qN * Quaternion.Euler(0, 60 * k, 0);
                    b.AddBox(P(e, n, PaveH + hc * 0.5f) + qq * new Vector3(r * 0.8f, 0, 0), new Vector3(0.4f, hc, 0.4f), steel, qq);
                }
                b.Mat = MeshBuilder.MatMatte;
            }
            void LongTank(double e, double n, float len, float r, Color col, Vector3 along)
            {
                Quaternion qq = Quaternion.FromToRotation(Vector3.up, along.normalized);
                b.Mat = MeshBuilder.MatGloss;
                int s0 = b.v.Count;
                b.AddFrustum(len * 0.5f, -len * 0.5f, r, r, col, 20, capTop: true, capBottom: true);
                b.Transform(s0, qq, P(e, n, PaveH + r + 1.2f));
                b.Mat = MeshBuilder.MatMetal;
                foreach (float t in new[] { -0.32f, 0f, 0.32f })
                    b.AddBox(P(e, n, PaveH + 0.7f) + along.normalized * (t * len), new Vector3(r * 1.6f, 1.4f, 0.8f), steel, Quaternion.LookRotation(along, U));
                b.Mat = MeshBuilder.MatMatte;
            }
            // A second, smaller assembly hall: cladding with ribs, a tall door (facing 'door') in a teal frame, an office annexe
            void HighBay(double e, double n, float w, float h, float d, Vector3 door)
            {
                Quaternion q = Quaternion.LookRotation(door, U);
                Vector3 c0 = P(e, n, PaveH), fz = door.normalized, fx = Vector3.Cross(U, fz).normalized;
                b.Mat = MeshBuilder.MatFacade(MeshBuilder.Cladding);
                b.AddBox(c0 + U * (h * 0.5f), new Vector3(w, h, d), new Color(0.71f, 0.7f, 0.67f), q);
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(c0 + U * (h + 0.6f), new Vector3(w + 2f, 1.2f, d + 2f), teal, q);
                Color ribC = new Color(0.68f, 0.69f, 0.7f);
                for (float x = -w * 0.5f + 2f; x <= w * 0.5f - 2f; x += 4f)
                    foreach (int sgn in new[] { -1, 1 }) b.AddBox(c0 + U * (h * 0.5f) + fx * x + fz * (sgn * (d * 0.5f + 0.1f)), new Vector3(0.45f, h, 0.25f), ribC, q);
                float dw = w * 0.34f, dh = h * 0.82f;
                b.Mat = MeshBuilder.MatDarkMetal;
                b.AddBox(c0 + U * (dh * 0.5f) + fz * (d * 0.5f + 0.5f), new Vector3(dw, dh, 0.5f), new Color(0.22f, 0.24f, 0.27f), q);
                for (float y = 6f; y < dh; y += 6f) b.AddBox(c0 + U * y + fz * (d * 0.5f + 0.8f), new Vector3(dw, 0.25f, 0.2f), new Color(0.1f, 0.11f, 0.12f), q);
                b.Mat = MeshBuilder.MatPaint;
                b.AddBox(c0 + U * (dh + 0.8f) + fz * (d * 0.5f + 0.7f), new Vector3(dw + 3.2f, 1.6f, 0.6f), teal, q);
                foreach (int sgn in new[] { -1, 1 }) b.AddBox(c0 + U * (dh * 0.5f) + fx * (sgn * (dw * 0.5f + 0.8f)) + fz * (d * 0.5f + 0.7f), new Vector3(1.6f, dh + 1f, 0.6f), teal, q);
                b.AddBox(c0 + U * (h * 0.72f) + fx * (dw * 0.5f + 9f) + fz * (d * 0.5f + 0.3f), new Vector3(12f, 16f, 0.25f), teal, q);
                b.AddBox(c0 + U * (h * 0.72f) + fx * (dw * 0.5f + 9f) + fz * (d * 0.5f + 0.45f), new Vector3(4.5f, 4.5f, 0.3f), Color.white, q * Quaternion.Euler(0, 0, 45));
                b.Mat = MeshBuilder.MatFacade(MeshBuilder.Ribbon);
                b.AddBox(c0 + U * 8f + fx * (w * 0.5f + 13f) - fz * (d * 0.12f), new Vector3(26f, 16f, d * 0.72f), new Color(0.78f, 0.72f, 0.62f), q);
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(c0 + U * 16.4f + fx * (w * 0.5f + 13f) - fz * (d * 0.12f), new Vector3(26.6f, 0.8f, d * 0.72f + 0.6f), teal, q);
            }

            // ---- The east launch complex (the far left of captaincarl24's map): its own assembly hall, garages and a rollout shed
            // along the road out, and two long rocket roads to two pads near the coast with a tank farm between them
            Plaza(1200, 56, 1420, 262, 22);
            HighBay(1270, 150, 62f, 56f, 44f, E);
            Lawn(1206, 62, 1240, 92, 8, 2);
            Lawn(1360, 220, 1412, 254, 8, 3);
            Round(1228, -27, 30);
            Walk(1228, 3, 1238, 56, 7);
            Lab(1228, -27, 26, 18, 1, MeshBuilder.Punched);
            Round(1420, 253, 28);
            Lab(1420, 253, 22, 22, 2);
            foreach (var (pe, pn) in new[] { (2192.0, -206.0), (2037.0, -428.0) })
            {
                double de = pe - 1300, dn = pn - 150, dl = System.Math.Sqrt(de * de + dn * dn);
                RocketRoad(1300, 150, pe - de / dl * 56, pn - dn / dl * 56);
            }
            ExtraPad(2192, -206, -N);
            ExtraPad(2037, -428, E);
            Round(2066, -300, 34);
            SphereTank(2046, -292, 7f, white); SphereTank(2068, -292, 7f, white); SphereTank(2090, -292, 7f, white);
            Round(2345, -268, 28);
            LongTank(2338, -260, 18f, 2.6f, new Color(0.78f, 0.42f, 0.16f), E);
            LongTank(2338, -276, 18f, 2.6f, new Color(0.78f, 0.42f, 0.16f), E);
            Plaza(712, 288, 812, 322, 8);
            SmallHangar(762, 305, 90f, 16f, 12f, -N);                                                    // the rollout shed
            Plaza(1046, 276, 1146, 312, 8);
            for (int k = 0; k < 3; k++) SmallHangar(1066 + k * 30, 294, 22f, 16f, 7f, -N);              // garages

            // ---- North of the campus: warehouses and offices, then the north-west complex with its own hall and pad, a
            // dish on its own pad and a few small buildings by the far-side road
            Plaza(-180, 780, 130, 950, 16);
            SmallHangar(-110, 832, 60f, 36f, 12f, -N);
            SmallHangar(-30, 832, 44f, 36f, 10f, -N);
            Lab(40, 902, 50, 24, 3);
            Lab(-90, 906, 36, 22, 2, MeshBuilder.Punched);
            Lawn(76, 800, 120, 830, 8, 3);
            Lawn(-170, 880, -140, 940, 8, 3);
            Plaza(-1440, 1050, -1262, 1210, 18);
            HighBay(-1352, 1130, 54f, 48f, 40f, -E);
            RocketRoad(-1374, 1124, -1716, 1032);
            ExtraPad(-1770, 1020, N);
            Round(-1892, 982, 24);
            SphereTank(-1900, 982, 5.5f, white); SphereTank(-1882, 982, 5.5f, white);
            Round(-1110, 895, 22);
            BigDish(b, P(-1110, 895, PaveH), 9f, 205f, 42f, E, N, white, steel, dark);
            Plaza(-912, 846, -852, 972, 8);
            Lab(-882, 946, 26, 18, 1);
            Lab(-884, 872, 24, 20, 2, MeshBuilder.Punched);
            Round(-1158, 1011, 16);
            SmallHangar(-1158, 1011, 18f, 14f, 6f, E);
            b.Mat = MeshBuilder.MatMatte;

            // ================================================================ a busier space centre (milestone 24, captaincarl24: "it looks so
            // empty"): a staff town, an industrial park, a solar farm, a visitor centre with a rocket garden, a harbour with a
            // landing barge, a lighthouse, an engine test stand, more offices and car parks, roundabouts
            Color asphaltCol = new Color(0.45f, 0.46f, 0.47f);
            Color[] carCols = { new Color(0.85f, 0.85f, 0.83f), new Color(0.2f, 0.25f, 0.35f), new Color(0.7f, 0.15f, 0.12f), new Color(0.12f, 0.45f, 0.52f), new Color(0.15f, 0.15f, 0.16f), new Color(0.55f, 0.57f, 0.6f), new Color(0.9f, 0.6f, 0.15f) };
            void Keep(double e0, double n0, double e1, double n1) => keepClear.Add(new[] { e0, n0, e1, n1 });
            void ParkedCar(double e, double n, Vector3 dir, Color col, string kind = "car")
            {
                var car = Crowd.VehicleMesh(kind + "Low", col);
                var verts = car.vertices; var norms = car.normals; var cols = car.colors; var tris = car.triangles;
                var uv = new List<Vector4>(); car.GetUVs(0, uv);
                Quaternion q = Quaternion.LookRotation(dir, U);
                Vector3 at = P(e, n, PaveH + 0.02f);
                int s0 = b.v.Count;
                for (int i = 0; i < verts.Length; i++) { b.v.Add(at + q * verts[i]); b.n.Add(q * norms[i]); b.c.Add(cols[i]); }
                b.AddRawUv(uv);
                foreach (int t in tris) b.t.Add(s0 + t);
            }
            // A car park: asphalt with a kerb, rows of painted bays across the north-south direction, cars in some of them
            void CarPark(double e0, double n0, double e1, double n1, double fill)
            {
                b.Mat = MeshBuilder.MatConcrete;
                PaveRect(b, e0 - 1, n0 - 1, e1 + 1, n1 + 1, 4, KerbH, kerb);
                b.Mat = MeshBuilder.MatAsphalt;
                PaveRect(b, e0, n0, e1, n1, 3, PaveH, asphaltCol);
                Color line = new Color(0.88f, 0.88f, 0.86f);
                int row = 0;
                for (double rn = n0 + 1.5; rn + 5.2 <= n1 - 1; rn += 13, row++)
                    for (double re = e0 + 2; re + 2.6 <= e1 - 1; re += 2.8)
                    {
                        b.Mat = MeshBuilder.MatMatte;
                        PaveStrip(b, re, rn, re, rn + 5.2, 0.12, PaveH + 0.03f, line);
                        if (rng.NextDouble() < fill) ParkedCar(re + 1.4, rn + 2.6, row % 2 == 0 ? N : -N, carCols[rng.Next(carCols.Length)], rng.NextDouble() < 0.15 ? "van" : "car");
                    }
                b.Mat = MeshBuilder.MatMatte;
                Keep(e0 - 4, n0 - 4, e1 + 4, n1 + 4);
            }
            // A house: walls, a pitched roof with overhangs, gable ends, windows front and back, a door facing 'front'
            void House(double e, double n, float w, float d, float h, Color wall, Color roof, Vector3 front)
            {
                Vector3 fz = front.normalized, fx = Vector3.Cross(U, fz).normalized;
                Quaternion q = Quaternion.LookRotation(fz, U);
                Vector3 c0 = P(e, n, 0.03f);
                b.Mat = MeshBuilder.MatMatte;
                b.AddBox(c0 + U * (h * 0.5f), new Vector3(w, h, d), wall, q);
                float hw = w * 0.5f + 0.4f, hd = d * 0.5f + 0.5f, rh = d * 0.36f;
                Vector3 top = c0 + U * h;
                Vector3 A = top - fx * hw + fz * hd, B = top + fx * hw + fz * hd, C = top + fx * hw - fz * hd, D = top - fx * hw - fz * hd;
                Vector3 R1 = top - fx * hw + U * rh, R2 = top + fx * hw + U * rh;
                b.Mat = MeshBuilder.MatPaint;
                b.AddQuad(A, B, R2, R1, (fz * rh + U * hd).normalized, roof);
                b.AddQuad(C, D, R1, R2, (-fz * rh + U * hd).normalized, roof);
                b.Mat = MeshBuilder.MatMatte;
                float gh = rh * (d * 0.5f) / hd;
                foreach (int sgn in new[] { -1, 1 })
                    b.AddTri(top + fx * (sgn * w * 0.5f) + fz * (d * 0.5f), top + fx * (sgn * w * 0.5f) - fz * (d * 0.5f), top + fx * (sgn * w * 0.5f) + U * gh, fx * sgn, wall);
                void Pane(Vector3 c, float pw, float ph, Vector3 outward, Color col)
                {
                    b.AddQuad(c - fx * (pw * 0.5f) - U * (ph * 0.5f), c + fx * (pw * 0.5f) - U * (ph * 0.5f), c + fx * (pw * 0.5f) + U * (ph * 0.5f), c - fx * (pw * 0.5f) + U * (ph * 0.5f), outward, col);
                }
                Color pane = new Color(0.12f, 0.17f, 0.22f);
                Vector3 ff = c0 + fz * (d * 0.5f + 0.03f), bf = c0 - fz * (d * 0.5f + 0.03f);
                b.Mat = MeshBuilder.MatGlass;
                Pane(ff - fx * (w * 0.28f) + U * (h * 0.55f), 1.6f, 1.2f, fz, pane);
                Pane(ff + fx * (w * 0.28f) + U * (h * 0.55f), 1.6f, 1.2f, fz, pane);
                Pane(bf + U * (h * 0.55f), 2.4f, 1.2f, -fz, pane);
                b.Mat = MeshBuilder.MatPaint;
                Pane(ff + U * 1.05f, 1.0f, 2.1f, fz, roof * 0.8f);
                b.Mat = MeshBuilder.MatMatte;
            }
            // A street tree: a trunk and a round crown
            Color[] greens = { new Color(0.17f, 0.3f, 0.1f), new Color(0.22f, 0.35f, 0.12f), new Color(0.26f, 0.38f, 0.14f) };
            void StreetTree(double e, double n)
            {
                Vector3 at = P(e, n, 0);
                float h = 5.5f + (float)rng.NextDouble() * 2.5f;
                b.Mat = MeshBuilder.MatMatte;
                int s0 = b.v.Count;
                b.AddFrustum(h * 0.55f, 0f, 0.12f, 0.18f, new Color(0.33f, 0.25f, 0.17f), 5);
                b.Transform(s0, Quaternion.identity, at);
                b.AddSphere(at + U * (h * 0.72f), h * 0.3f, greens[rng.Next(greens.Length)], 8, 6);
            }
            // A roundabout: an asphalt ring and a planted middle with a little teal obelisk
            void Roundabout(double e, double n, double r)
            {
                b.Mat = MeshBuilder.MatAsphalt;
                GroundRing(b, e, n, r * 0.45, r, 0.165f, 32, asphaltCol);
                b.Mat = MeshBuilder.MatConcrete;
                GroundRing(b, e, n, r * 0.45 - 0.6, r * 0.45, 0.2f, 32, kerb);
                b.Mat = MeshBuilder.MatLawn;
                PaveRect(b, e - r * 0.45 + 0.6, n - r * 0.45 + 0.6, e + r * 0.45 - 0.6, n + r * 0.45 - 0.6, r * 0.45 - 0.6, 0.22f, lawn);
                b.Mat = MeshBuilder.MatPaint;
                int s0 = b.v.Count;
                b.AddFrustum(7f, 0f, 0.15f, 0.9f, teal, 4, capBottom: true);
                b.Transform(s0, qN, P(e, n, 0.22f));
                b.Mat = MeshBuilder.MatMatte;
            }
            // A rocket on display: a plinth, the body with a teal band, a nose cone and fins
            void Monument(double e, double n, float r, float hgt)
            {
                Vector3 at = P(e, n, PaveH);
                b.Mat = MeshBuilder.MatConcrete;
                int s0 = b.v.Count;
                b.AddFrustum(1.2f, 0f, r * 2.4f, r * 2.6f, new Color(0.6f, 0.6f, 0.58f), 16, capTop: true);
                b.Transform(s0, Quaternion.identity, at);
                b.Mat = MeshBuilder.MatGloss;
                s0 = b.v.Count;
                b.AddFrustum(hgt * 0.8f, 1.2f, r, r, white * 1.4f, 16, capTop: true);
                b.Transform(s0, Quaternion.identity, at);
                s0 = b.v.Count;
                b.AddFrustum(hgt, hgt * 0.8f, 0.05f, r, white * 1.4f, 16);
                b.Transform(s0, Quaternion.identity, at);
                b.Mat = MeshBuilder.MatPaint;
                s0 = b.v.Count;
                b.AddFrustum(hgt * 0.5f, hgt * 0.42f, r * 1.02f, r * 1.02f, teal, 16);
                b.Transform(s0, Quaternion.identity, at);
                for (int k = 0; k < 4; k++)
                {
                    Quaternion qq = qN * Quaternion.Euler(0, 45 + k * 90, 0);
                    b.AddBox(at + U * (1.2f + hgt * 0.07f) + qq * new Vector3(r * 1.5f, 0, 0), new Vector3(r * 1.2f, hgt * 0.14f, 0.15f), dark, qq);
                }
                b.Mat = MeshBuilder.MatMatte;
            }
            // A power pylon: a tapering lattice-like mast with two cross-arms
            Vector3 Pylon(double e, double n)
            {
                Vector3 at = P(e, n, 0);
                b.Mat = MeshBuilder.MatMetal;
                int s0 = b.v.Count;
                b.AddFrustum(24f, 0f, 0.4f, 2.4f, steel, 4);
                b.Transform(s0, qN * Quaternion.Euler(0, 45, 0), at);
                b.AddBox(at + U * 21f, new Vector3(9f, 0.4f, 0.5f), steel, qN);
                b.AddBox(at + U * 17.5f, new Vector3(7f, 0.4f, 0.5f), steel, qN);
                b.Mat = MeshBuilder.MatMatte;
                return at + U * 20.6f;
            }
            void Wire(Vector3 a, Vector3 c)
            {
                Vector3 d = c - a, mid = (a + c) * 0.5f - U * 1.2f;
                foreach (var (p0, p1) in new[] { (a, mid), (mid, c) })
                {
                    Vector3 dd = p1 - p0;
                    b.AddBox((p0 + p1) * 0.5f, new Vector3(0.06f, 0.06f, dd.magnitude), dark, Quaternion.LookRotation(dd.normalized, U));
                }
            }

            // ---- The staff town north of the campus: streets of houses, apartments, a school with a sports field, shops
            // and a supermarket, a park with a pond and a water tower, street trees and lamps
            Keep(150, 330, 590, 800);
            {
                Color[] walls = { new Color(0.93f, 0.9f, 0.82f), new Color(0.78f, 0.86f, 0.9f), new Color(0.8f, 0.86f, 0.76f), new Color(0.95f, 0.84f, 0.74f), new Color(0.94f, 0.94f, 0.92f), new Color(0.86f, 0.8f, 0.86f) };
                Color[] roofs = { new Color(0.55f, 0.26f, 0.18f), new Color(0.3f, 0.32f, 0.36f), new Color(0.36f, 0.26f, 0.21f), new Color(0.45f, 0.3f, 0.22f) };
                void HouseRow(double e0, double e1, double n, Vector3 front)
                {
                    for (double e = e0 + 14; e <= e1 - 12; e += 24)
                    {
                        bool two = rng.NextDouble() < 0.4;
                        House(e, n, 11f + (float)rng.NextDouble() * 3f, 9f + (float)rng.NextDouble() * 2f, two ? 5.8f : 3.2f, walls[rng.Next(walls.Length)], roofs[rng.Next(roofs.Length)], front);
                        if (rng.NextDouble() < 0.45) ParkedCar(e + 9, n + (front == N ? 4 : -4), front, carCols[rng.Next(carCols.Length)]);
                    }
                }
                foreach (var (e0, e1) in new[] { (165.0, 300.0), (300.0, 430.0), (430.0, 575.0) })
                {
                    HouseRow(e0, e1, 574, -N); HouseRow(e0, e1, 636, N);
                    if (e0 > 200) { HouseRow(e0, e1, 664, -N); HouseRow(e0, e1, 726, N); }
                }
                HouseRow(165, 575, 754, -N);
                // the park, its pond and the town's water tower
                Lawn(170, 655, 296, 736, 14, 6);
                b.Mat = MeshBuilder.MatGlass;
                PaveRect(b, 208, 682, 246, 712, 14, LawnH + 0.02f, new Color(0.1f, 0.22f, 0.3f));
                Walk(170, 695, 206, 695, 3, false); Walk(248, 695, 296, 695, 3, false);
                {
                    Vector3 wt = P(276, 722, LawnH);
                    b.Mat = MeshBuilder.MatGloss;
                    b.AddSphere(wt + U * 26f, 5.5f, white * 1.3f, 20, 12);
                    b.Mat = MeshBuilder.MatPaint;
                    b.AddBox(wt + U * 26f + N * 5.4f, new Vector3(4f, 1.4f, 0.1f), teal, qN);
                    b.Mat = MeshBuilder.MatMetal;
                    for (int k = 0; k < 4; k++) { Quaternion qq = qN * Quaternion.Euler(0, 45 + 90 * k, 0); b.AddBox(wt + U * 12f + qq * new Vector3(3f, 0, 0), new Vector3(0.45f, 24f, 0.45f), steel, qq); }
                }
                // apartments
                Plaza(178, 450, 292, 540, 10);
                Lab(205, 470, 30, 18, 4); Lab(262, 470, 30, 18, 4, MeshBuilder.Punched); Lab(234, 522, 60, 16, 3);
                CarPark(184, 404, 288, 438, 0.6);
                // the school and its sports field
                Plaza(318, 484, 414, 520, 8);
                Lab(366, 502, 80, 24, 2, MeshBuilder.Punched);
                b.Mat = MeshBuilder.MatLawn;
                PaveRect(b, 318, 396, 416, 466, 6, LawnH, lawn);
                b.Mat = MeshBuilder.MatMatte;
                foreach (double fe in new[] { 324.0, 367.0, 410.0 }) PaveStrip(b, fe, 400, fe, 462, 0.25, LawnH + 0.02f, Color.white);
                PaveStrip(b, 324, 400, 410, 400, 0.25, LawnH + 0.02f, Color.white); PaveStrip(b, 324, 462, 410, 462, 0.25, LawnH + 0.02f, Color.white);
                // shops with an awning, a supermarket, a car park
                Plaza(436, 500, 570, 546, 8);
                Lab(503, 524, 120, 16, 1, MeshBuilder.Curtain);
                b.Mat = MeshBuilder.MatPaint;
                Box(503, 514.5, PaveH + 3.4f, new Vector3(120f, 0.3f, 3.2f), new Color(0.5f, 0.18f, 0.14f));
                Plaza(442, 420, 500, 470, 6);
                Lab(471, 446, 52, 36, 1, MeshBuilder.Punched);
                CarPark(508, 360, 568, 476, 0.55);
                // street trees and lamps
                foreach (double sn in new[] { 560.0, 650.0, 740.0 })
                    for (double e = 180; e < 570; e += 36) { StreetTree(e, sn + ((int)(e / 36) % 2 == 0 ? 7 : -7)); if ((int)(e / 36) % 3 == 0) Lamp(e + 18, sn + 5); }
                foreach (double se in new[] { 300.0, 430.0, 575.0 })
                    for (double n = 380; n < 735; n += 40) StreetTree(se + ((int)(n / 40) % 2 == 0 ? 7 : -7), n);
            }

            // ---- The visitor centre by the north roundabout: a glass hall, a rocket garden, a car park with coaches
            Plaza(570, 1110, 740, 1250, 24);
            Lab(655, 1218, 84, 26, 2, MeshBuilder.Curtain);
            Monument(600, 1150, 1.6f, 26f); Monument(632, 1138, 2.2f, 38f); Monument(668, 1152, 1.2f, 18f); Monument(704, 1140, 3.0f, 54f);
            Lawn(580, 1166, 600, 1196, 6, 2); Lawn(712, 1166, 732, 1196, 6, 2);
            CarPark(752, 1118, 840, 1230, 0.5);
            ParkedCar(770, 1222, E, white * 1.4f, "bus"); ParkedCar(800, 1222, E, white * 1.4f, "bus");
            Roundabout(490, 1316, 18); Roundabout(180, 985, 15); Roundabout(1030, 200, 15);
            Keep(560, 1100, 850, 1260);

            // ---- The industrial park along the east road: warehouses, workshops, a propellant plant with a pipe rack and a
            // vent stack, a car park; a substation, and a solar farm to the south feeding it along a line of pylons
            Plaza(652, 126, 708, 228, 8);
            SmallHangar(680, 205, 46f, 28f, 11f, N);
            SmallHangar(680, 150, 46f, 28f, 11f, E);
            CarPark(716, 176, 790, 216, 0.6);
            SmallHangar(880, 290, 40f, 22f, 9f, -N);
            SmallHangar(958, 268, 34f, 20f, 8f, -N);
            Plaza(790, 125, 960, 195, 10);
            SphereTank(815, 150, 8f, white); SphereTank(845, 150, 8f, white); SphereTank(815, 178, 6f, white);
            LongTank(900, 140, 26f, 3f, white, E); LongTank(900, 156, 26f, 3f, white, E);
            {
                b.Mat = MeshBuilder.MatMetal;
                for (double pe = 800; pe <= 955; pe += 10) Box(pe, 175, PaveH + 2.5f, new Vector3(0.4f, 5f, 0.4f), steel);
                foreach (float ph in new[] { 4.4f, 5.0f }) Box(878, 175, PaveH + ph, new Vector3(156f, 0.5f, 1.6f), new Color(0.6f, 0.62f, 0.64f));
                int s0 = b.v.Count;
                b.AddFrustum(32f, 0f, 0.8f, 1.2f, steel, 12);
                b.Transform(s0, Quaternion.identity, P(945, 140, PaveH));
                b.Mat = MeshBuilder.MatPaint;
                s0 = b.v.Count;
                b.AddFrustum(32f, 30f, 0.85f, 0.85f, new Color(0.7f, 0.15f, 0.12f), 12);
                b.Transform(s0, Quaternion.identity, P(945, 140, PaveH));
                b.Mat = MeshBuilder.MatMatte;
            }
            Keep(640, 110, 1010, 300);
            // the substation and the pylons out to the solar farm
            Plaza(496, 232, 546, 280, 4);
            {
                b.Mat = MeshBuilder.MatDarkMetal;
                for (int k = 0; k < 3; k++) Box(508 + k * 14, 252, PaveH + 2.4f, new Vector3(5f, 4.8f, 6f), new Color(0.38f, 0.4f, 0.43f));
                b.Mat = MeshBuilder.MatMetal;
                foreach (int sgn in new[] { -1, 1 }) { Box(521, 256 + sgn * 24, PaveH + 1.2f, new Vector3(50f, 2.4f, 0.1f), steel); Box(521 + sgn * 25, 256, PaveH + 1.2f, new Vector3(0.1f, 2.4f, 48f), steel); }
                b.Mat = MeshBuilder.MatMatte;
                Vector3 prev = Pylon(540, 200);
                foreach (var (pe, pn) in new[] { (552.0, 140.0), (564.0, 80.0) }) { Vector3 nx = Pylon(pe, pn); Wire(prev, nx); prev = nx; }
            }
            Keep(490, 60, 560, 290);
            {
                // the solar farm: rows of dark panels tilted to the sun on low frames
                Quaternion tilt = qN * Quaternion.Euler(16f, 0, 0);
                for (double rn = -284; rn <= 44; rn += 14)
                    for (double re = 562; re + 30 <= 1100; re += 31)
                    {
                        b.Mat = MeshBuilder.MatGlass;
                        b.AddBox(P(re + 15, rn, 1.7f), new Vector3(30f, 0.12f, 4.2f), new Color(0.05f, 0.08f, 0.18f), tilt);
                        b.Mat = MeshBuilder.MatMetal;
                        b.AddBox(P(re + 15, rn, 0.8f), new Vector3(29f, 1.4f, 0.15f), steel, qN);
                    }
                b.Mat = MeshBuilder.MatMatte;
            }
            Keep(555, -295, 1105, 52);

            // ---- More at the east launch complex: an office block, a car park; at the north-west complex a workshop, an
            // office and a car park
            Plaza(1438, 196, 1506, 262, 8);
            Lab(1472, 229, 40, 24, 3);
            CarPark(1442, 110, 1520, 186, 0.55);
            SmallHangar(-1320, 1240, 52f, 24f, 10f, -N);
            Lab(-1222, 1240, 30, 20, 2, MeshBuilder.Punched);
            CarPark(-1252, 1055, -1190, 1110, 0.5);
            Keep(-1350, 1220, -1200, 1262);
            // and by the airport terminal
            CarPark(-240, 404, -170, 470, 0.6);
            // and the campus: by headquarters' back yard and by mission control
            CarPark(-188, 316, -112, 372, 0.65);
            CarPark(214, 148, 276, 190, 0.6);

            // ---- An engine test stand beyond the research campus: a tall concrete stand with a steel frame on top, the
            // flame bucket, an engine in place, propellant spheres and a half-buried bunker
            Round(-1180, 520, 42);
            {
                b.Mat = MeshBuilder.MatConcrete;
                Box(-1180, 520, PaveH + 14f, new Vector3(16f, 28f, 16f), new Color(0.66f, 0.66f, 0.63f));
                b.Mat = MeshBuilder.MatMetal;
                for (int cx = -1; cx <= 1; cx += 2) for (int cz = -1; cz <= 1; cz += 2) Box(-1180 + cx * 7, 520 + cz * 7, PaveH + 34f, new Vector3(0.6f, 12f, 0.6f), steel);
                Box(-1180, 520, PaveH + 40.2f, new Vector3(16f, 0.6f, 16f), steel);
                b.Mat = MeshBuilder.MatDarkMetal;
                Box(-1180, 500, PaveH + 3f, new Vector3(14f, 6f, 24f), new Color(0.18f, 0.18f, 0.19f));   // flame bucket
                int s0 = b.v.Count;
                b.AddFrustum(7.5f, 4.5f, 0.7f, 1.6f, dark, 16);
                b.Transform(s0, Quaternion.identity, P(-1180, 512, PaveH));
                b.Mat = MeshBuilder.MatMatte;
            }
            SphereTank(-1150, 556, 5f, white); SphereTank(-1210, 556, 5f, white);
            {
                b.Mat = MeshBuilder.MatLawn;
                PaveRect(b, -1272, 578, -1232, 606, 10, LawnH + 1.4f, lawn);
                b.Mat = MeshBuilder.MatConcrete;
                Box(-1252, 577, PaveH + 1.5f, new Vector3(16f, 3f, 2f), new Color(0.6f, 0.6f, 0.58f));
                b.Mat = MeshBuilder.MatGlass;
                Box(-1252, 575.9, PaveH + 2f, new Vector3(10f, 0.6f, 0.1f), glass);
                b.Mat = MeshBuilder.MatMatte;
            }
            Keep(-1280, 470, -1130, 615);

            // ---- The harbour on the east coast: a yard with a warehouse and an office, a long pier on pilings out over the
            // beach into the sea, two cranes, and the landing barge tied up with a booster standing on its deck
            Plaza(2520, 70, 2682, 220, 14);
            SmallHangar(2560, 176, 50f, 30f, 12f, -N);
            Lab(2644, 96, 26, 18, 2, MeshBuilder.Punched);
            CarPark(2600, 150, 2676, 214, 0.4);
            {
                const double pn = 125;
                b.Mat = MeshBuilder.MatConcrete;
                for (double pe = 2682; pe < 3420; pe += 40)
                    Box(pe + 20, pn, 3f, new Vector3(40.2f, 1f, 16f), new Color(0.6f, 0.6f, 0.58f));
                for (double pe = 2690; pe <= 3420; pe += 20)
                    foreach (int sgn in new[] { -1, 1 }) Box(pe, pn + sgn * 6.5, 0.5f, new Vector3(0.9f, 5f, 0.9f), new Color(0.5f, 0.5f, 0.48f));
                b.Mat = MeshBuilder.MatMatte;
                PaveStrip(b, 2682, pn - 7.6, 3422, pn - 7.6, 0.3, 3.52f, yellow);
                PaveStrip(b, 2682, pn + 7.6, 3422, pn + 7.6, 0.3, 3.52f, yellow);
                // cranes on the pier
                foreach (double ce in new[] { 3250.0, 3360.0 })
                {
                    b.Mat = MeshBuilder.MatPaint;
                    foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
                        Box(ce + sx * 5, pn + sz * 6, 3.5f + 8f, new Vector3(0.8f, 16f, 0.8f), new Color(0.95f, 0.72f, 0.1f));
                    Box(ce, pn + 8, 19.8f, new Vector3(1.6f, 1.6f, 40f), new Color(0.95f, 0.72f, 0.1f));
                    Box(ce, pn, 21.2f, new Vector3(5f, 3f, 4f), teal);
                    b.Mat = MeshBuilder.MatMetal;
                    Box(ce, pn + 22, 13f, new Vector3(0.08f, 13f, 0.08f), dark);
                }
                // the landing barge with a returned booster on it
                b.Mat = MeshBuilder.MatDarkMetal;
                Box(3340, 162, 0.5f, new Vector3(92f, 5f, 34f), new Color(0.24f, 0.25f, 0.27f));
                b.Mat = MeshBuilder.MatPaint;
                foreach (int sgn in new[] { -1, 1 }) Box(3340 + sgn * 44, 162, 4.6f, new Vector3(4f, 3.2f, 34f), new Color(0.3f, 0.32f, 0.35f));
                b.Mat = MeshBuilder.MatMatte;
                Annulus(b, P(3340, 162, 3.02f), 12f, 13f, 48, yellow);
                Box(3340, 162, 3.03f, new Vector3(18f, 0.02f, 1.2f), yellow);
                Box(3340, 162, 3.03f, new Vector3(1.2f, 0.02f, 18f), yellow);
                Vector3 bb = P(3340, 162, 3f);
                b.Mat = MeshBuilder.MatGloss;
                int s0 = b.v.Count;
                b.AddFrustum(31f, 2.5f, 1.85f, 1.85f, new Color(0.82f, 0.82f, 0.8f), 20, capTop: true, capBottom: true);
                b.Transform(s0, Quaternion.identity, bb);
                b.Mat = MeshBuilder.MatPaint;
                s0 = b.v.Count;
                b.AddFrustum(12f, 2.5f, 1.87f, 1.87f, new Color(0.3f, 0.29f, 0.28f), 20);   // soot from coming home
                b.Transform(s0, Quaternion.identity, bb);
                b.Mat = MeshBuilder.MatDarkMetal;
                for (int k = 0; k < 4; k++)
                {
                    Quaternion qq = qN * Quaternion.Euler(0, 45 + k * 90, 0);
                    Vector3 o = qq * Vector3.right;
                    b.AddBox(bb + U * 3.2f + o * 3.1f, new Vector3(0.35f, 0.35f, 5.6f), dark, Quaternion.LookRotation((o * 2.4f - U * 4.2f).normalized, U));
                    b.AddBox(bb + U * 29.5f + o * 2.3f, new Vector3(1.0f, 1.2f, 0.12f), dark, Quaternion.LookRotation(o, U));
                }
                b.Mat = MeshBuilder.MatMatte;
            }
            Keep(2510, 60, 2700, 230);

            // ---- The lighthouse on the south-east point, with the keeper's cottage
            Round(1300, -1415, 22);
            {
                Vector3 lh = P(1300, -1415, PaveH);
                for (int k = 0; k < 6; k++)
                {
                    b.Mat = MeshBuilder.MatGloss;
                    int s0 = b.v.Count;
                    float y0 = k * 4.6f, y1 = y0 + 4.6f;
                    b.AddFrustum(y1, y0, 3.2f - y1 * 0.035f, 3.2f - y0 * 0.035f, k % 2 == 1 ? new Color(0.7f, 0.14f, 0.11f) : white * 1.45f, 20, capTop: k == 5);
                    b.Transform(s0, Quaternion.identity, lh);
                }
                b.Mat = MeshBuilder.MatMetal;
                int s1 = b.v.Count;
                b.AddFrustum(28.2f, 27.6f, 3.0f, 3.0f, dark, 20, capTop: true);
                b.Transform(s1, Quaternion.identity, lh);
                b.Mat = MeshBuilder.MatLamp;
                b.AddSphere(lh + U * 30f, 1.3f, new Color(1f, 0.92f, 0.6f), 12, 8);
                b.Mat = MeshBuilder.MatGlass;
                s1 = b.v.Count;
                b.AddFrustum(31.6f, 28.2f, 1.9f, 1.9f, glass, 12);
                b.Transform(s1, Quaternion.identity, lh);
                b.Mat = MeshBuilder.MatPaint;
                s1 = b.v.Count;
                b.AddFrustum(34.5f, 31.6f, 0.1f, 2.3f, new Color(0.7f, 0.14f, 0.11f), 12, capBottom: true);
                b.Transform(s1, Quaternion.identity, lh);
                b.Mat = MeshBuilder.MatMatte;
            }
            House(1322, -1404, 10f, 8f, 3.4f, white * 1.45f, new Color(0.7f, 0.14f, 0.11f), N);
            Keep(1270, -1445, 1340, -1385);
        }

        /// <summary>A crawler with a rocket on it (milestone 24): a wide platform on four track units, a launch mount and a white
        /// rocket with a teal band, facing +Z. It trundles between the east complex's hall and its pads.</summary>
        /// <summary>Height of the crawler's deck above the road, where a rocket stands on it.</summary>
        public const float CrawlerDeck = 6.8f;

        public static Mesh CrawlerMesh(bool withRocket = true)
        {
            var b = new MeshBuilder();
            Color grey = new Color(0.55f, 0.56f, 0.58f), dark = new Color(0.18f, 0.19f, 0.2f), white = new Color(0.9f, 0.9f, 0.89f), teal = new Color(0.62f, 0.2f, 0.16f), red = new Color(0.55f, 0.1f, 0.07f);
            b.Mat = MeshBuilder.MatDarkMetal;
            foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
                b.AddBox(new Vector3(sx * 12f, 1.6f, sz * 11f), new Vector3(5f, 3.2f, 9f), dark, Quaternion.identity);
            b.Mat = MeshBuilder.MatPaint;
            b.AddBox(new Vector3(0, 4.6f, 0), new Vector3(30f, 2.8f, 30f), grey, Quaternion.identity);
            b.AddBox(new Vector3(0, 6.4f, 0), new Vector3(16f, 0.8f, 16f), grey * 0.8f, Quaternion.identity);
            for (int k = 0; k < 4; k++)
            {
                Quaternion q = Quaternion.Euler(0, 45 + 90 * k, 0);
                b.AddBox(new Vector3(0, 6.8f, 0) + q * new Vector3(4f, 0, 0), new Vector3(1.2f, 2.4f, 1.2f), red, q);
            }
            // a lattice umbilical tower on one side
            for (int cx = -1; cx <= 1; cx += 2) for (int cz = -1; cz <= 1; cz += 2)
                b.AddBox(new Vector3(-10f + cx, 22f, cz), new Vector3(0.25f, 32f, 0.25f), red, Quaternion.identity);
            for (float y = 8f; y < 38f; y += 3f) b.AddBox(new Vector3(-10f, y, 0), new Vector3(2.2f, 0.18f, 2.2f), red, Quaternion.identity);
            if (!withRocket) return b.ToMesh("Crawler");
            b.Mat = MeshBuilder.MatGloss;
            int s0 = b.v.Count;
            b.AddFrustum(38f, 8f, 1.9f, 1.9f, white, 20, capBottom: true);
            b.AddFrustum(44f, 38f, 0.1f, 1.9f, white, 20);
            b.Transform(s0, Quaternion.identity, Vector3.zero);
            b.Mat = MeshBuilder.MatPaint;
            s0 = b.v.Count;
            b.AddFrustum(30f, 27f, 1.93f, 1.93f, teal, 20);
            b.Transform(s0, Quaternion.identity, Vector3.zero);
            return b.ToMesh("Crawler");
        }

        /// <summary>Ordinary building colours (milestone 24, captaincarl24: "not white and cyan"): buff and sand stone, warm grey concrete,
        /// brick and darker stone, picked by place so neighbours differ.</summary>
        static Color WallTint(double e, double n)
        {
            Color[] walls = { new Color(0.8f, 0.74f, 0.64f), new Color(0.76f, 0.68f, 0.55f), new Color(0.68f, 0.67f, 0.64f), new Color(0.62f, 0.4f, 0.32f), new Color(0.73f, 0.69f, 0.62f), new Color(0.58f, 0.54f, 0.5f) };
            return walls[PlaceHash(e, n) % walls.Length];
        }

        /// <summary>Colours for metal-clad sheds: galvanised grey, sand, green-grey, brown and slate.</summary>
        static Color CladTint(double e, double n)
        {
            Color[] clad = { new Color(0.62f, 0.63f, 0.62f), new Color(0.7f, 0.65f, 0.56f), new Color(0.52f, 0.57f, 0.52f), new Color(0.54f, 0.4f, 0.31f), new Color(0.46f, 0.5f, 0.55f) };
            return clad[PlaceHash(e, n) % clad.Length];
        }

        static int PlaceHash(double e, double n)
        {
            unchecked
            {
                int h = (int)System.Math.Round(e) * 73856093 ^ (int)System.Math.Round(n) * 19349663;
                h ^= h >> 13;
                return (h & 0x7fffffff);
            }
        }

        /// <summary>
        /// A conifer (milestone 25, nicer): a tapering trunk and four tiers of foliage, each a short skirt that droops at
        /// the edge, getting smaller up the tree, in slightly different greens (darker low down), leaning a touch.
        /// </summary>
        public static void Conifer(MeshBuilder b, Vector3 at, float h, System.Random rng)
        {
            Color trunk = new Color(0.32f, 0.22f, 0.14f);
            Color[] greens = { new Color(0.11f, 0.22f, 0.09f), new Color(0.14f, 0.27f, 0.1f), new Color(0.1f, 0.2f, 0.11f), new Color(0.16f, 0.29f, 0.12f) };
            Color g = greens[rng.Next(greens.Length)];
            float w = h * (0.28f + 0.08f * (float)rng.NextDouble());
            b.Mat = MeshBuilder.MatMatte;
            int s0 = b.v.Count;
            b.AddFrustum(h * 0.4f, 0f, 0.16f, 0.3f, trunk, 6);
            for (int k = 0; k < 4; k++)
            {
                float t = k / 4f, y0 = h * (0.18f + 0.2f * t), y1 = y0 + h * (0.34f - 0.04f * k), r = w * (1f - 0.22f * k);
                float shade = 0.8f + 0.12f * k + 0.08f * (float)rng.NextDouble();
                var c = new Color(g.r * shade, g.g * shade, g.b * shade, 1f);
                b.AddFrustum(y0 + h * 0.03f, y0 - h * 0.02f, r * 0.92f, r, Color.Lerp(c, Color.black, 0.15f), 9, capBottom: true);   // the drooping edge
                b.AddFrustum(y1, y0 + h * 0.03f, 0.04f, r * 0.92f, c, 9);
            }
            b.Transform(s0, Quaternion.Euler((float)rng.NextDouble() * 5f - 2.5f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 5f - 2.5f), at);
        }

        /// <summary>A rounded rectangle of ground cover (east/north bounds, corner radius c), as a fan that follows the planet.</summary>
        void PaveRect(MeshBuilder b, double e0, double n0, double e1, double n1, double c, float h, Color col)
        {
            c = System.Math.Min(c, System.Math.Min(e1 - e0, n1 - n0) * 0.5);
            double ce = (e0 + e1) * 0.5, cn = (n0 + n1) * 0.5;
            var pts = new List<Vector3>();
            void Corner(double oe, double on, double a0)
            {
                for (int i = 0; i <= 8; i++)
                {
                    double a = a0 + i * System.Math.PI / 16;
                    pts.Add(At(oe + System.Math.Cos(a) * c, on + System.Math.Sin(a) * c) + Vector3.up * h);
                }
            }
            Corner(e1 - c, n1 - c, 0);
            Corner(e0 + c, n1 - c, System.Math.PI / 2);
            Corner(e0 + c, n0 + c, System.Math.PI);
            Corner(e1 - c, n0 + c, System.Math.PI * 1.5);
            Vector3 mid = At(ce, cn) + Vector3.up * h;
            for (int i = 0; i < pts.Count; i++) b.AddTri(mid, pts[i], pts[(i + 1) % pts.Count], Vector3.up, col);
        }

        /// <summary>A ring of paint (radii r0..r1) on the ground round (e, n), following the planet.</summary>
        void GroundRing(MeshBuilder b, double e, double n, double r0, double r1, float h, int seg, Color col)
        {
            for (int i = 0; i < seg; i++)
            {
                double a0 = i * 2 * System.Math.PI / seg, a1 = (i + 1) * 2 * System.Math.PI / seg;
                Vector3 In(double a) => At(e + System.Math.Cos(a) * r0, n + System.Math.Sin(a) * r0) + Vector3.up * h;
                Vector3 Out(double a) => At(e + System.Math.Cos(a) * r1, n + System.Math.Sin(a) * r1) + Vector3.up * h;
                b.AddQuad(In(a0), Out(a0), Out(a1), In(a1), Vector3.up, col);
            }
        }

        /// <summary>A straight strip of ground cover w wide, cut into lengths that follow the planet.</summary>
        void PaveStrip(MeshBuilder b, double e0, double n0, double e1, double n1, double w, float h, Color col)
        {
            double len = System.Math.Sqrt((e1 - e0) * (e1 - e0) + (n1 - n0) * (n1 - n0));
            if (len < 0.01) return;
            double se = -(n1 - n0) / len * w * 0.5, sn = (e1 - e0) / len * w * 0.5;
            int segs = System.Math.Max(1, (int)(len / 40));
            for (int i = 0; i < segs; i++)
            {
                double f0 = (double)i / segs, f1 = (double)(i + 1) / segs;
                double ea = e0 + (e1 - e0) * f0, na = n0 + (n1 - n0) * f0, eb = e0 + (e1 - e0) * f1, nb = n0 + (n1 - n0) * f1;
                b.AddQuad(At(ea - se, na - sn) + Vector3.up * h, At(eb - se, nb - sn) + Vector3.up * h, At(eb + se, nb + sn) + Vector3.up * h, At(ea + se, na + sn) + Vector3.up * h, Vector3.up, col);
            }
        }

        /// <summary>
        /// A park tree (milestone 24, more natural): a trunk with a couple of limbs and a crown of lumpy leaf clumps, each
        /// clump's surface pushed in and out by noise, darker underneath and lighter on top, in a few greens.
        /// </summary>
        static void RoundTree(MeshBuilder b, Vector3 at, float h, System.Random rng)
        {
            Color trunk = new Color(0.33f, 0.25f, 0.17f);
            Color[] greens = { new Color(0.16f, 0.28f, 0.09f), new Color(0.2f, 0.33f, 0.11f), new Color(0.24f, 0.36f, 0.12f), new Color(0.18f, 0.3f, 0.12f) };
            Color leaf = greens[rng.Next(greens.Length)];
            b.Mat = MeshBuilder.MatMatte;
            int s0 = b.v.Count;
            b.AddFrustum(h * 0.5f, 0f, 0.16f, 0.27f, trunk, 7);
            b.Transform(s0, Quaternion.Euler((float)rng.NextDouble() * 6f - 3f, 0, (float)rng.NextDouble() * 6f - 3f), at);
            float r = h * 0.27f;
            Vector3 crown = at + Vector3.up * (h * 0.66f);
            for (int k = 0; k < 2; k++)   // limbs reaching into the crown
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 1.1f, Mathf.Sin(a)).normalized;
                s0 = b.v.Count;
                b.AddFrustum(r * 1.1f, 0f, 0.05f, 0.12f, trunk, 5);
                b.Transform(s0, Quaternion.FromToRotation(Vector3.up, dir), at + Vector3.up * (h * 0.4f));
            }
            int clumps = 5 + rng.Next(3);
            float seed = (float)rng.NextDouble() * 100f;
            for (int k = 0; k < clumps; k++)
            {
                float a = k * Mathf.PI * 2f / clumps + (float)rng.NextDouble() * 0.6f;
                float out_ = k == 0 ? 0f : r * (0.45f + 0.25f * (float)rng.NextDouble());
                Vector3 c = crown + new Vector3(Mathf.Cos(a) * out_, (k == 0 ? r * 0.35f : (float)rng.NextDouble() * r * 0.4f - r * 0.1f), Mathf.Sin(a) * out_);
                float cr = r * (k == 0 ? 0.85f : 0.55f + 0.2f * (float)rng.NextDouble());
                s0 = b.v.Count;
                b.AddSphere(Vector3.zero, 1f, leaf, 12, 8);
                for (int i = s0; i < b.v.Count; i++)
                {
                    Vector3 d = b.v[i];
                    float bump = 0.82f + 0.36f * Mathf.PerlinNoise(d.x * 2.1f + seed + k * 3.7f, d.z * 2.1f + d.y * 1.3f + seed);
                    b.v[i] = c + d * (cr * bump);
                    float shade = Mathf.Lerp(0.62f, 1.12f, d.y * 0.5f + 0.5f) * (0.9f + 0.2f * Mathf.PerlinNoise(d.x * 5f + seed, d.y * 5f + k));
                    var col = leaf * shade; col.a = 1f;
                    b.c[i] = col;
                }
            }
        }

        /// <summary>A big tracking dish: concrete base, a turning pedestal, a yoke, the bowl (aperture radius r) with its feed on three struts.</summary>
        static void BigDish(MeshBuilder b, Vector3 at, float r, float azimuthDeg, float elevationDeg, Vector3 E, Vector3 N, Color white, Color steel, Color dark)
        {
            Vector3 U = Vector3.up;
            float scale = r / 13f;
            b.Mat = MeshBuilder.MatConcrete;
            int s0 = b.v.Count;
            b.AddFrustum(3f * scale, 0f, 5.5f * scale, 6f * scale, new Color(0.62f, 0.62f, 0.6f), 16, capTop: true);
            b.Transform(s0, Quaternion.identity, at);
            b.Mat = MeshBuilder.MatPaint;
            s0 = b.v.Count;
            b.AddFrustum(14f * scale, 3f * scale, 2.2f * scale, 3.4f * scale, white, 16, capTop: true);
            b.Transform(s0, Quaternion.identity, at);
            Vector3 hub = at + U * (16f * scale);
            Vector3 az = E * Mathf.Cos(azimuthDeg * Mathf.Deg2Rad) + N * Mathf.Sin(azimuthDeg * Mathf.Deg2Rad);
            Vector3 side = Vector3.Cross(U, az).normalized;
            b.Mat = MeshBuilder.MatMetal;
            foreach (int sgn in new[] { -1, 1 })
                b.AddBox(at + U * (15f * scale) + side * (sgn * 3.2f * scale), new Vector3(0.9f, 4f, 2f) * scale, steel, Quaternion.LookRotation(az, U));
            b.AddBox(at + U * (13.2f * scale), new Vector3(7.4f, 1f, 2.4f) * scale, steel, Quaternion.LookRotation(az, U));
            // the bowl points along 'look'
            Vector3 look = (az * Mathf.Cos(elevationDeg * Mathf.Deg2Rad) + U * Mathf.Sin(elevationDeg * Mathf.Deg2Rad)).normalized;
            float R = r / Mathf.Sin(Mathf.PI * 0.32f);
            b.Mat = MeshBuilder.MatPaint;
            s0 = b.v.Count;
            b.AddSphere(Vector3.zero, R, white, 36, 10, Mathf.PI * 0.32f, doubleSided: true);
            float depth = R * (1 - Mathf.Cos(Mathf.PI * 0.32f));
            for (int i = s0; i < b.v.Count; i++) b.v[i] = new Vector3(b.v[i].x, -b.v[i].y + R, b.v[i].z);   // a bowl opening upward, its bottom at 0
            b.Transform(s0, Quaternion.FromToRotation(U, look), hub);
            Vector3 feed = hub + look * (depth + r * 0.75f);
            b.Mat = MeshBuilder.MatMetal;
            Vector3 perp = Vector3.Cross(look, side).normalized;
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI * 2f / 3f;
                Vector3 rim = hub + look * depth + (side * Mathf.Cos(a) + perp * Mathf.Sin(a)) * (r * 0.92f);
                Vector3 d = feed - rim;
                b.AddBox((rim + feed) * 0.5f, new Vector3(0.18f * scale, 0.18f * scale, d.magnitude), steel, Quaternion.LookRotation(d.normalized, U));
            }
            b.Mat = MeshBuilder.MatPaint;
            b.AddBox(feed, new Vector3(1.2f, 1.2f, 1.8f) * scale, dark, Quaternion.LookRotation(look, Mathf.Abs(look.y) > 0.99f ? N : U));
        }

        /// <summary>
        /// A big arched hangar: a barrel-vault roof (w wide, d deep, h high) with ribs, a glazed north gable over a huge
        /// sliding door facing the runway, and a solid south gable.
        /// </summary>
        static void ArchHangar(MeshBuilder b, Vector3 at, float w, float d, float h, Vector3 E, Vector3 N, Color panel, Color glass, Color steel, Color dark, Color teal)
        {
            Vector3 U = Vector3.up;
            const int seg = 28;
            Vector3 Arch(float t, float grow) => E * (Mathf.Cos(t) * (w * 0.5f + grow)) + U * (Mathf.Sin(t) * (h + grow));
            Vector3 ArchN(float t) => (E * (Mathf.Cos(t) / (w * 0.5f)) + U * (Mathf.Sin(t) / h)).normalized;
            void Vault(float n0, float n1, float grow, Color col)
            {
                int s = b.v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float t = Mathf.PI * i / seg;
                    Vector3 p = Arch(t, grow), nrm = ArchN(t);
                    b.v.Add(at + p + N * n0); b.n.Add(nrm); b.c.Add(col);
                    b.v.Add(at + p + N * n1); b.n.Add(nrm); b.c.Add(col);
                }
                for (int i = 0; i < seg; i++) { int a = s + i * 2; Tri(b, a, a + 1, a + 2); Tri(b, a + 2, a + 1, a + 3); }
            }
            b.Mat = MeshBuilder.MatMetal;
            Vault(-d * 0.5f, d * 0.5f, 0f, new Color(0.72f, 0.74f, 0.77f));
            b.Mat = MeshBuilder.MatPaint;
            for (float n = -d * 0.5f; n <= d * 0.5f + 0.01f; n += 7f) Vault(n - 0.35f, n + 0.35f, 0.35f, steel);
            // gables: fans from the floor centre to the arch
            void Gable(float n, Vector3 facing, Color col)
            {
                for (int i = 0; i < seg; i++)
                {
                    float t0 = Mathf.PI * i / seg, t1 = Mathf.PI * (i + 1) / seg;
                    b.AddTri(at + N * n, at + N * n + Arch(t0, 0f), at + N * n + Arch(t1, 0f), facing, col);
                }
            }
            b.Mat = MeshBuilder.MatMatte;
            Gable(-d * 0.5f, -N, panel);
            b.Mat = MeshBuilder.MatGlass;
            Gable(d * 0.5f, N, glass);
            // the north door: tall panels under the glazing, a teal header, hazard stripes along the bottom
            b.Mat = MeshBuilder.MatDarkMetal;
            float doorW = w * 0.8f, doorH = h * 0.62f;
            for (int k = 0; k < 6; k++)
            {
                float x = -doorW * 0.5f + doorW * (k + 0.5f) / 6f;
                b.AddBox(at + N * (d * 0.5f + 0.4f) + E * x + U * (doorH * 0.5f), new Vector3(doorW / 6f - 0.3f, doorH, 0.4f), new Color(0.6f, 0.62f, 0.65f), Quaternion.LookRotation(N, U));
            }
            b.Mat = MeshBuilder.MatPaint;
            b.AddBox(at + N * (d * 0.5f + 0.6f) + U * (doorH + 0.8f), new Vector3(doorW + 2f, 1.6f, 0.5f), teal, Quaternion.LookRotation(N, U));
            for (int k = 0; k < 20; k++)
                b.AddBox(at + N * (d * 0.5f + 0.65f) + E * (-doorW * 0.5f + doorW * (k + 0.5f) / 20f) + U * 0.6f, new Vector3(doorW / 20f, 1.2f, 0.1f), k % 2 == 0 ? new Color(0.9f, 0.7f, 0.12f) : dark, Quaternion.LookRotation(N, U));
            // mullions on the glazing
            b.Mat = MeshBuilder.MatPaint;
            for (int k = -4; k <= 4; k++)
            {
                float x = k * w * 0.1f;
                float top = h * Mathf.Sqrt(Mathf.Max(0f, 1f - (2f * x / w) * (2f * x / w)));
                if (top < doorH + 2f) continue;
                b.AddBox(at + N * (d * 0.5f + 0.2f) + E * x + U * ((doorH + 1.6f + top) * 0.5f), new Vector3(0.3f, top - doorH - 1.6f, 0.3f), panel, Quaternion.LookRotation(N, U));
            }
        }

        // ---------------------------------------------------------------- headquarters (milestone 24)

        /// <summary>
        /// Headquarters: a five-storey white block banded with green glass and sun louvers, a curved all-glass wing at its
        /// east end, a glass lobby under a canopy, a busy roof (penthouse, solar arrays, cooling plant, an antenna mast),
        /// and in front a round plaza with a fountain, flags, palms, hedges, lamps and benches.
        /// </summary>
        void BuildHeadquarters(MeshBuilder b, Vector3 E, Vector3 N, Color white, Color teal, Color steel, Color dark)
        {
            Color green = new Color(0.09f, 0.27f, 0.25f), greenLight = new Color(0.16f, 0.38f, 0.36f), panel = new Color(0.82f, 0.78f, 0.7f);
            Color solar = new Color(0.05f, 0.08f, 0.18f), concrete = new Color(0.7f, 0.7f, 0.68f), path = new Color(0.78f, 0.77f, 0.73f);
            Color hedge = new Color(0.1f, 0.22f, 0.08f), lawn = new Color(0.82f, 0.86f, 0.76f), water = new Color(0.25f, 0.45f, 0.55f);
            Vector3 U = Vector3.up;
            Quaternion qN = Quaternion.LookRotation(N, U);
            Vector3 P(double e, double n, double h = 0) => At(e, n) + U * (float)h;
            void Box(double e, double n, double h, Vector3 size, Color c) => b.AddBox(P(e, n, h), size, c, qN);   // size: (east-west, up, north-south)

            // Main block: e -212..-150, n 248..278, five floors of 4.2 m
            const float floorH = 4.2f;
            const int floors = 5;
            float top = floorH * floors + 0.6f;
            b.Mat = MeshBuilder.MatMatte;
            Box(-181, 263, top * 0.5f, new Vector3(62, top, 30), panel);
            for (int f = 0; f < floors; f++)
            {
                float y = 1.2f + f * floorH;
                // glass bands round all four faces, set a little proud of the panels
                b.Mat = MeshBuilder.MatGlass;
                Box(-181, 247.9, y + 1.25f, new Vector3(60, 2.5f, 0.3f), green);
                Box(-181, 278.1, y + 1.25f, new Vector3(60, 2.5f, 0.3f), green);
                Box(-212.1, 263, y + 1.25f, new Vector3(0.3f, 2.5f, 28), green);
                // a sun louver over each south-facing band
                b.Mat = MeshBuilder.MatPaint;
                Box(-181, 247.2, y + 2.75f, new Vector3(61, 0.18f, 1.6f), white);
                // floor slab edges
                Box(-181, 247.85, y - 0.25f, new Vector3(62.2f, 0.35f, 0.5f), white);
                Box(-181, 278.15, y - 0.25f, new Vector3(62.2f, 0.35f, 0.5f), white);
            }
            // vertical fins up the south face, and corner piers
            b.Mat = MeshBuilder.MatPaint;
            for (double e = -210.5; e <= -151; e += 2.5) Box(e, 247.6, top * 0.5f, new Vector3(0.22f, top - 0.6f, 0.7f), white);
            foreach (double e in new[] { -212.0, -150.6 }) foreach (double n in new[] { 248.0, 278.0 }) Box(e, n, top * 0.5f, new Vector3(1.6f, top + 0.2f, 1.6f), panel);
            Box(-181, 263, top + 0.45f, new Vector3(62.6f, 0.9f, 30.6f), white);                         // parapet
            b.Mat = MeshBuilder.MatConcrete;
            Box(-181, 263, top + 0.05f, new Vector3(61, 0.2f, 29), concrete);                            // roof deck

            // Curved glass wing on the east end: a half drum, four floors, all glass between white slab rings
            Vector3 c = P(-150, 263);
            const float R = 17f;
            float wingTop = floorH * 4 + 0.5f;
            b.Mat = MeshBuilder.MatGlass;
            Drum(b, c, R, 0.3f, wingTop - 0.4f, -Mathf.PI / 2, Mathf.PI / 2, 40, green, E, N);
            b.Mat = MeshBuilder.MatPaint;
            for (int f = 0; f <= 4; f++)
            {
                float y = 0.3f + f * floorH;
                Drum(b, c, R + 0.35f, y - 0.3f, y + 0.25f, -Mathf.PI / 2, Mathf.PI / 2, 40, white, E, N);
            }
            for (int k = 0; k <= 24; k++)
            {
                float th = -Mathf.PI / 2 + Mathf.PI * k / 24f;
                Vector3 d = E * Mathf.Cos(th) + N * Mathf.Sin(th);
                b.AddBox(c + d * (R + 0.15f) + U * (wingTop * 0.5f), new Vector3(0.18f, wingTop, 0.45f), panel, Quaternion.LookRotation(d, U));   // mullions
            }
            // its roof, a parapet ring and a rooftop solar field
            b.Mat = MeshBuilder.MatConcrete;
            for (int k = 0; k < 40; k++)
            {
                float a0 = -Mathf.PI / 2 + Mathf.PI * k / 40f, a1 = -Mathf.PI / 2 + Mathf.PI * (k + 1) / 40f;
                b.AddTri(c + U * wingTop, c + (E * Mathf.Cos(a0) + N * Mathf.Sin(a0)) * R + U * wingTop, c + (E * Mathf.Cos(a1) + N * Mathf.Sin(a1)) * R + U * wingTop, U, concrete);
            }
            b.Mat = MeshBuilder.MatPaint;
            Drum(b, c, R + 0.2f, wingTop, wingTop + 0.9f, -Mathf.PI / 2, Mathf.PI / 2, 40, white, E, N);
            b.Mat = MeshBuilder.MatGlass;
            for (int row = 0; row < 5; row++)
                for (int k = 0; k < 6 - (row == 0 || row == 4 ? 2 : 0); k++)
                {
                    double e = -146 + k * 2.2 - (row == 0 || row == 4 ? -2.2 : 0), n = 255 + row * 3.6;
                    b.AddBox(P(e, n, wingTop + 0.75f), new Vector3(2f, 0.08f, 1.5f), solar, qN * Quaternion.Euler(-22f, 0, 0));
                }

            // Main roof: a glass-banded penthouse, solar rows, cooling plant, ducts and an antenna mast with a dish
            b.Mat = MeshBuilder.MatMatte;
            Box(-190, 266, top + 2.3f, new Vector3(26, 4.2f, 16), panel);
            b.Mat = MeshBuilder.MatGlass;
            Box(-190, 257.9, top + 2.4f, new Vector3(24, 2.2f, 0.25f), greenLight);
            b.Mat = MeshBuilder.MatPaint;
            Box(-190, 266, top + 4.5f, new Vector3(27, 0.4f, 17), white);
            b.Mat = MeshBuilder.MatGlass;
            for (int row = 0; row < 2; row++)
                for (int k = 0; k < 8; k++)
                    b.AddBox(P(-209 + k * 2.4, 251 + row * 3.6, top + 0.9f), new Vector3(2.2f, 0.08f, 1.7f), solar, qN * Quaternion.Euler(-24f, 0, 0));
            b.Mat = MeshBuilder.MatMetal;
            for (int k = 0; k < 3; k++)
            {
                Box(-170 + k * 5.5, 271, top + 1.3f, new Vector3(4.2f, 2.2f, 3.4f), new Color(0.72f, 0.74f, 0.76f));     // cooling units
                int s0 = b.v.Count;
                b.AddFrustum(0.5f, 0f, 1.3f, 1.3f, new Color(0.3f, 0.32f, 0.34f), 16, capTop: true);
                b.Transform(s0, Quaternion.identity, P(-170 + k * 5.5, 271, top + 2.4f));                                     // fan housings
            }
            Box(-166, 262, top + 0.6f, new Vector3(16, 0.9f, 0.9f), steel);                                                    // duct run
            Box(-158.5, 258, top + 0.6f, new Vector3(0.9f, 0.9f, 8f), steel);
            {
                Vector3 m = P(-208, 273, top);
                b.AddBox(m + U * 6f, new Vector3(0.25f, 12f, 0.25f), steel, Quaternion.identity);
                for (int k = 0; k < 4; k++) b.AddBox(m + U * (2.5f + k * 2.6f), new Vector3(1.4f, 0.08f, 0.08f), steel, Quaternion.LookRotation(N, U));
                int s0 = b.v.Count;
                b.Mat = MeshBuilder.MatPaint;
                b.AddSphere(Vector3.zero, 1.6f, white, 16, 6, Mathf.PI * 0.32f, true);
                b.Transform(s0, Quaternion.LookRotation(U, -N) * Quaternion.Euler(-60f, 0, 0), m + U * 8.5f - N * 0.6f);
                b.Mat = MeshBuilder.MatLamp;
                b.AddSphere(m + U * 12.1f, 0.2f, new Color(1f, 0.25f, 0.2f), 8, 5);                                            // aircraft warning light
            }

            // Lobby: a one-storey glass pavilion on the south side under a deep white canopy on thin columns
            b.Mat = MeshBuilder.MatGlass;
            Box(-185, 243, 2.6f, new Vector3(34, 5.2f, 10), greenLight);
            b.Mat = MeshBuilder.MatPaint;
            Box(-185, 241, 5.55f, new Vector3(40, 0.5f, 16), white);
            Box(-185, 241, 5.15f, new Vector3(40.4f, 0.5f, 16.4f), teal);                                                     // a teal edge under the white slab
            b.Mat = MeshBuilder.MatMetal;
            for (int k = 0; k < 6; k++) Box(-203 + k * 7.2, 233.6, 2.7f, new Vector3(0.3f, 5.4f, 0.3f), steel);
            b.Mat = MeshBuilder.MatDarkMetal;
            Box(-185, 237.9, 1.3f, new Vector3(6, 2.6f, 0.2f), new Color(0.15f, 0.17f, 0.2f));                                   // revolving doors
            b.Mat = MeshBuilder.MatPaint;
            Box(-185, 237.85, 4.4f, new Vector3(16, 0.9f, 0.15f), teal);                                                           // name band
            for (int k = 0; k < 7; k++) Box(-191 + k * 2, 237.75, 4.4f, new Vector3(1.2f, 0.5f, 0.05f), white);                      // the letters, as blocks

            // The plaza: a lawn with a ring path, spokes, a fountain, three flags, palms, hedges, lamps and benches
            Vector3 pc = P(-168, 220);
            const float ring = 12.5f;
            b.Mat = MeshBuilder.MatLawn;
            Disc(b, pc + U * 0.04f, ring + 9f, 48, lawn);
            b.Mat = MeshBuilder.MatConcrete;
            Annulus(b, pc + U * 0.07f, ring - 1.3f, ring + 1.3f, 48, path);
            foreach (var (de, dn, len) in new[] { (0.0, 1.0, 9.0), (0.0, -1.0, 10.0), (1.0, 0.0, 30.0), (-1.0, 0.0, 9.0) })
            {
                Vector3 d = (E * (float)de + N * (float)dn).normalized;
                Vector3 mid = pc + d * (ring + (float)len * 0.5f) + U * 0.07f;
                b.AddBox(mid, new Vector3(2.4f, 0.04f, (float)len + 1f), path, Quaternion.LookRotation(d, U));
            }
            Disc(b, pc + U * 0.08f, 6.2f, 32, path);
            b.Mat = MeshBuilder.MatConcrete;
            {
                int s0 = b.v.Count;
                b.AddFrustum(0.6f, 0f, 4.2f, 4.4f, concrete, 32, capTop: false);
                b.Transform(s0, Quaternion.identity, pc);
                s0 = b.v.Count;
                b.AddFrustum(0.6f, 0.05f, 3.9f, 3.9f, concrete, 32, doubleSided: true);
                b.Transform(s0, Quaternion.identity, pc);
                b.Mat = MeshBuilder.MatGlass;
                Disc(b, pc + U * 0.48f, 3.9f, 32, water);
                b.Mat = MeshBuilder.MatConcrete;
                s0 = b.v.Count;
                b.AddFrustum(1.6f, 0f, 0.5f, 0.8f, concrete, 16, capTop: true);
                b.Transform(s0, Quaternion.identity, pc);
            }
            b.Mat = MeshBuilder.MatMetal;
            for (int k = -1; k <= 1; k++)
            {
                Vector3 fp = pc - E * (ring + 6f) + N * (k * 3.2f);
                b.AddBox(fp + U * 6f, new Vector3(0.16f, 12f, 0.16f), steel, Quaternion.identity);
                b.Mat = MeshBuilder.MatPaint;
                Color fc = k == 0 ? teal : k < 0 ? white : new Color(0.95f, 0.75f, 0.2f);
                b.AddBox(fp + U * 11f + E * 1.2f, new Vector3(2.3f, 1.4f, 0.04f), fc, qN * Quaternion.Euler(0, 90f, 0));
                if (k == 0) b.AddBox(fp + U * 11f + E * 1.2f + N * 0.03f, new Vector3(0.6f, 0.6f, 0.06f), white, qN * Quaternion.Euler(0, 90f, 45f));
                b.Mat = MeshBuilder.MatMetal;
            }
            // hedges along the building and round the lawn
            b.Mat = MeshBuilder.MatMatte;
            for (double e = -208; e < -150; e += 4.6) if (e < -203 || e > -167) Shrub(b, P(e, 236.8), new Vector3(4.4f, 1.2f, 1.6f), qN, hedge, (float)e);
            for (int k = 0; k < 28; k++)
            {
                float th = Mathf.PI * 2f * k / 28f;
                if (Mathf.Abs(Mathf.Cos(th)) > 0.97f || Mathf.Abs(Mathf.Sin(th)) > 0.97f) continue;   // gaps for the spokes
                Vector3 d = E * Mathf.Cos(th) + N * Mathf.Sin(th);
                Shrub(b, pc + d * (ring + 3.2f), new Vector3(2.7f, 1.0f, 1.2f), Quaternion.LookRotation(Vector3.Cross(U, d), U), hedge, k * 1.7f);
            }
            // palms round the ring and along the drive
            var rngP = new System.Random(77);
            for (int k = 0; k < 8; k++)
            {
                float th = Mathf.PI * 2f * (k + 0.5f) / 8f;
                Palm(b, pc + (E * Mathf.Cos(th) + N * Mathf.Sin(th)) * (ring + 6.5f), 7.5f + (float)rngP.NextDouble() * 3f, rngP);
            }
            for (int k = 0; k < 5; k++) Palm(b, P(-146 + k * 8, 241.5), 8f + (float)rngP.NextDouble() * 2.5f, rngP);
            for (int k = 0; k < 4; k++) Palm(b, P(-128, 252 + k * 8), 7f + (float)rngP.NextDouble() * 3f, rngP);
            // lamp posts and benches along the paths
            for (int k = 0; k < 8; k++)
            {
                float th = Mathf.PI * 2f * k / 8f;
                Vector3 lp = pc + (E * Mathf.Cos(th) + N * Mathf.Sin(th)) * (ring + 1.9f);
                b.Mat = MeshBuilder.MatDarkMetal;
                b.AddBox(lp + U * 2.2f, new Vector3(0.12f, 4.4f, 0.12f), dark, Quaternion.identity);
                b.Mat = MeshBuilder.MatLamp;
                b.AddBox(lp + U * 4.45f, new Vector3(0.45f, 0.18f, 0.45f), new Color(1f, 0.93f, 0.75f), Quaternion.identity);
                b.Mat = MeshBuilder.MatPaint;
                Vector3 bp = pc + (E * Mathf.Cos(th + 0.2f) + N * Mathf.Sin(th + 0.2f)) * (ring - 2f);
                Quaternion bq = Quaternion.LookRotation(Vector3.Cross(U, E * Mathf.Cos(th) + N * Mathf.Sin(th)), U);
                b.AddBox(bp + U * 0.45f, new Vector3(1.8f, 0.08f, 0.5f), new Color(0.55f, 0.4f, 0.25f), bq);
                b.AddBox(bp + U * 0.22f, new Vector3(1.6f, 0.44f, 0.1f), dark, bq);
            }
            // a low entrance sign by the campus road
            b.Mat = MeshBuilder.MatConcrete;
            Box(-182, 206.5, 0.7f, new Vector3(9f, 1.4f, 0.6f), panel);
            b.Mat = MeshBuilder.MatPaint;
            Box(-182, 206.15, 0.85f, new Vector3(7f, 0.5f, 0.08f), teal);
            b.Mat = MeshBuilder.MatMatte;
        }

        /// <summary>A smooth curved wall: a band of a vertical cylinder of radius r round c, from angle th0 to th1 (east = 0, north = 90 deg).</summary>
        static void Drum(MeshBuilder b, Vector3 c, float r, float y0, float y1, float th0, float th1, int seg, Color col, Vector3 E, Vector3 N)
        {
            int s = b.v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float th = Mathf.Lerp(th0, th1, i / (float)seg);
                Vector3 d = E * Mathf.Cos(th) + N * Mathf.Sin(th);
                b.v.Add(c + d * r + Vector3.up * y0); b.n.Add(d); b.c.Add(col);
                b.v.Add(c + d * r + Vector3.up * y1); b.n.Add(d); b.c.Add(col);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = s + i * 2;
                Tri(b, a, a + 1, a + 2);
                Tri(b, a + 2, a + 1, a + 3);
            }
        }

        /// <summary>A triangle wound to face along its vertices' normals (the builder's convention).</summary>
        static void Tri(MeshBuilder b, int i0, int i1, int i2)
        {
            Vector3 nrm = b.n[i0] + b.n[i1] + b.n[i2];
            if (Vector3.Dot(Vector3.Cross(b.v[i1] - b.v[i0], b.v[i2] - b.v[i0]), nrm) < 0) { b.t.Add(i0); b.t.Add(i2); b.t.Add(i1); }
            else { b.t.Add(i0); b.t.Add(i1); b.t.Add(i2); }
        }

        static void Disc(MeshBuilder b, Vector3 c, float r, int seg, Color col)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.PI * 2f * i / seg, a1 = Mathf.PI * 2f * (i + 1) / seg;
                b.AddTri(c, c + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r, c + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r, Vector3.up, col);
            }
        }

        static void Annulus(MeshBuilder b, Vector3 c, float r0, float r1, int seg, Color col)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.PI * 2f * i / seg, a1 = Mathf.PI * 2f * (i + 1) / seg;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                b.AddQuad(c + d0 * r0, c + d0 * r1, c + d1 * r1, c + d1 * r0, Vector3.up, col);
            }
        }

        /// <summary>A rounded clipped shrub (size = width, height, depth), lumpy and darker underneath.</summary>
        static void Shrub(MeshBuilder b, Vector3 c, Vector3 size, Quaternion rot, Color col, float seed)
        {
            b.Mat = MeshBuilder.MatMatte;
            int s0 = b.v.Count;
            b.AddSphere(Vector3.zero, 1f, col, 12, 7);
            for (int i = s0; i < b.v.Count; i++)
            {
                Vector3 d = b.v[i];
                float bump = 0.88f + 0.24f * Mathf.PerlinNoise(d.x * 2.5f + seed, d.z * 2.5f + d.y * 1.7f);
                Vector3 q = new Vector3(d.x * size.x, Mathf.Max(d.y, -0.6f) * size.y, d.z * size.z) * 0.5f * bump;
                b.v[i] = c + rot * (q + Vector3.up * size.y * 0.3f);
                b.n[i] = rot * d;
                var k = col * Mathf.Lerp(0.6f, 1.1f, d.y * 0.5f + 0.5f); k.a = 1f;
                b.c[i] = k;
            }
        }

        /// <summary>A palm: a gently curving ringed trunk and a crown of drooping fronds.</summary>
        static void Palm(MeshBuilder b, Vector3 at, float h, System.Random rng)
        {
            Color bark = new Color(0.47f, 0.38f, 0.27f), bark2 = new Color(0.4f, 0.32f, 0.22f), frond = new Color(0.2f, 0.42f, 0.15f), frond2 = new Color(0.27f, 0.5f, 0.19f);
            float lean = (float)rng.NextDouble() * 360f, bend = 0.5f + (float)rng.NextDouble() * 0.8f;
            Quaternion yaw = Quaternion.Euler(0, lean, 0);
            Vector3 Spine(float t) => at + yaw * new Vector3(bend * t * t * h * 0.12f, t * h, 0);
            b.Mat = MeshBuilder.MatMatte;
            const int segs = 7;
            for (int i = 0; i < segs; i++)
            {
                Vector3 p0 = Spine(i / (float)segs), p1 = Spine((i + 1) / (float)segs);
                int s0 = b.v.Count;
                b.AddFrustum((p1 - p0).magnitude, 0, 0.2f - i * 0.008f, 0.24f - i * 0.008f, i % 2 == 0 ? bark : bark2, 8);
                b.Transform(s0, Quaternion.FromToRotation(Vector3.up, (p1 - p0).normalized), p0);
            }
            Vector3 crown = Spine(1f);
            for (int k = 0; k < 14; k++)
            {
                // each frond: a narrow blade in segments that arch out and droop, with a lighter midrib side
                float yaw2 = k * (360f / 14f) + (float)rng.NextDouble() * 12f;
                float rise = 25f + (float)rng.NextDouble() * 20f, len = 2.6f + (float)rng.NextDouble() * 1.2f;
                Quaternion qy = Quaternion.Euler(0, yaw2, 0);
                Vector3 pos = crown;
                float pitch = -rise;
                for (int j = 0; j < 6; j++)
                {
                    Vector3 dir = qy * (Quaternion.Euler(pitch, 0, 0) * Vector3.forward);
                    float seg = len / 6f, wdt = 0.55f * Mathf.Sin((j + 0.6f) / 6.6f * Mathf.PI);
                    b.AddBox(pos + dir * (seg * 0.5f), new Vector3(wdt, 0.025f, seg * 1.05f), (k + j) % 3 == 0 ? frond2 : frond, Quaternion.LookRotation(dir, qy * Vector3.up));
                    pos += dir * seg;
                    pitch += 14f + j * 3f;
                }
            }
            b.AddSphere(crown, 0.35f, bark2, 8, 5);
        }

        /// <summary>The space center's workers and vehicles.</summary>
        Crowd BuildCrowd(Transform site)
        {
            var c = new Crowd(site, 77) { Role = Ardlings.RoleOffice };   // the campus: office staff (others below by place)
            Vector3 P(double e, double n) => At(e, n);
            // Walkers between headquarters, mission control and the assembly building
            for (int k = 0; k < 5; k++) c.AddWalker(new[] { P(-150 + k * 3, 232), P(-60, 212 + k), P(120, 208 + k), P(165, 180), P(120, 214 - k), P(-60, 218 - k) });
            for (int k = 0; k < 4; k++) c.AddWalker(new[] { P(-25 + k * 4, 290), P(25, 292), P(30, 282 - k), P(-28, 280) });
            // round the headquarters plaza and in through the lobby
            for (int k = 0; k < 4; k++)
            {
                var ring = new Vector3[12];
                for (int i = 0; i < 12; i++) { double th = (i + k * 3) * System.Math.PI * 2 / 12; ring[i] = P(-168 + 12.3 * System.Math.Cos(th), 220 + 12.3 * System.Math.Sin(th)); }
                c.AddWalker(ring, 1.1f);
            }
            for (int k = 0; k < 3; k++) c.AddWalker(new[] { P(-168 + k, 233), P(-185 + k * 2, 236), P(-168 + k, 233), P(-125 + k, 220), P(-105, 226 + k * 2), P(-125 + k, 220) });
            for (int k = 0; k < 3; k++) c.AddWalker(new[] { P(130 + k * 4, 296), P(150, 290), P(175, 300), P(160, 300) });
            for (int k = 0; k < 3; k++) c.AddWalker(new[] { P(-1290 + k * 3, 182), P(-1278, 208), P(-1250, 210), P(-1265, 185) });
            c.Role = Ardlings.RoleWork;
            // the airport: round the operations building and along the apron
            for (int k = 0; k < 3; k++) c.AddWalker(new[] { P(-300 + k * 4, 334), P(-258, 338), P(-258, 396), P(-312, 396) });
            for (int k = 0; k < 2; k++) c.AddWalker(new[] { P(-394 + k * 3, 420), P(-394, 630), P(-350, 630), P(-350, 420) });
            for (int k = 0; k < 4; k++)
            {
                var ring = new Vector3[10];
                for (int i = 0; i < 10; i++) { double th = (i + k * 2.5) * System.Math.PI * 2 / 10; ring[i] = P(-200 + 20 * System.Math.Cos(th), 1100 + 20 * System.Math.Sin(th)); }
                c.AddWalker(ring, 1.0f);
            }
            for (int k = 0; k < 3; k++) c.AddWalker(new[] { P(-200 + k, 1130), P(-200, 1240), P(-198 + k, 1240), P(-198, 1130) });
            for (int k = 0; k < 3; k++) c.AddWalker(new[] { P(-1290 + k * 3, 218), P(-1190, 220), P(-1186, 256), P(-1272, 254) });
            for (int k = 0; k < 2; k++) c.AddWalker(new[] { P(150 + k * 3, 112), P(70, 64), P(150 + k * 3, 112), P(205, 140), P(250, 106), P(205, 140) });
            for (int k = 0; k < 2; k++) c.AddWalker(new[] { P(326 + k * 3, 94), P(338, 80), P(354, 94) });                 // the helipads
            for (int k = 0; k < 3; k++) c.AddWalker(new[] { P(1306 + k * 3, 100), P(1380, 110), P(1390, 200), P(1312, 196) }); // the east complex
            // Vehicles on the roads and the apron
            c.AddVehicle("car", new[] { P(-305, 198), P(325, 198), P(325, 202), P(-305, 202) }, 9f);
            c.AddVehicle("car", new[] { P(320, 202), P(-300, 202), P(-300, 198), P(320, 198) }, 8f);
            c.AddVehicle("truck", new[] { P(-9, 96), P(-9, 198), P(-88, 198), P(-88, 523), P(140, 523), P(140, 527), P(-92, 527), P(-92, 202), P(9, 202), P(9, 96) }, 11f);
            c.AddVehicle("truck", new[] { P(-9, 210), P(-9, 280), P(9, 280), P(9, 210) }, 4f);
            c.AddVehicle("tug", new[] { P(-405, 420), P(-405, 630), P(-398, 630), P(-398, 420) }, 5f);
            c.AddVehicle("car", new[] { P(198, 378), P(1028, 198), P(1196, 166), P(1196, 170), P(1032, 202), P(200, 386) }, 10f);
            c.AddVehicle("truck", new[] { P(148, 528), P(178, 985), P(486, 1312), P(490, 1320), P(182, 989), P(152, 528) }, 9f);
            c.AddVehicle("car", new[] { P(-88, 300), P(-88, 520), P(-92, 520), P(-92, 300) }, 7f);
            c.AddVehicle("forklift", new[] { P(-24, 284), P(24, 284), P(24, 290), P(-24, 290) }, 2.5f);

            // ---- Milestone 24: traffic on every road, out along one side and back along the other (points every 100 m or
            // so on the road surface, so they follow the planet's curve), and people about everywhere
            Vector3[] Lift(float h, params Vector3[] pts) { for (int i = 0; i < pts.Length; i++) pts[i] += Vector3.up * h; return pts; }
            Vector3[] Drive(double lane, params double[] en)
            {
                var pts = new List<Vector3>();
                void Leg(int i0, int i1, int step, double side)
                {
                    for (int i = i0; i != i1; i += step)
                    {
                        double ea = en[i * 2], na = en[i * 2 + 1], eb = en[(i + step) * 2], nb = en[(i + step) * 2 + 1];
                        double len = System.Math.Sqrt((eb - ea) * (eb - ea) + (nb - na) * (nb - na));
                        double se = (nb - na) / len * side, sn = -(eb - ea) / len * side;
                        int segs = System.Math.Max(1, (int)(len / 100));
                        for (int k = 0; k < segs; k++)
                        {
                            double f = (double)k / segs;
                            pts.Add(At(ea + (eb - ea) * f + se, na + (nb - na) * f + sn) + Vector3.up * 0.17f);
                        }
                    }
                }
                int count = en.Length / 2;
                Leg(0, count - 1, 1, lane);
                Leg(count - 1, 0, -1, lane);
                return pts.ToArray();
            }
            var traffic = new (string kind, float speed, double[] route)[]
            {
                ("bus", 9f, new double[] { -305, 200, 330, 200 }), ("car", 12f, new double[] { -305, 200, 330, 200, 470, 322 }),
                ("car", 13f, new double[] { 195, 382, 1030, 200, 1200, 168 }), ("tanker", 10f, new double[] { 195, 382, 1030, 200, 1200, 168 }),
                ("truck", 10f, new double[] { 195, 382, 1030, 200, 1200, 168 }), ("van", 12f, new double[] { 470, 322, 1030, 200 }),
                ("car", 14f, new double[] { 150, 525, 180, 985, 490, 1316, 600, 1255, 790, 1240 }), ("bus", 10f, new double[] { 150, 525, 180, 985, 490, 1316, 600, 1255, 790, 1240 }),
                ("car", 13f, new double[] { 490, 1316, -740, 1204, -1262, 1143 }), ("truck", 10f, new double[] { 490, 1316, -740, 1204, -1262, 1143 }),
                ("car", 15f, new double[] { 490, 1316, 857, 1724, 1574, 2102, 2700, 2800 }), ("bus", 12f, new double[] { 490, 1316, 857, 1724, 1574, 2102, 2700, 2800 }),
                ("truck", 12f, new double[] { 490, 1316, 857, 1724, 1574, 2102, 2700, 2800 }), ("car", 15f, new double[] { 2700, 2800, 1574, 2102, 857, 1724 }),
                ("car", 13f, new double[] { -905, 130, -913, 182, -956, 598, -997, 998, -1085, 1508, -1444, 2300 }),
                ("van", 12f, new double[] { -905, 130, -913, 182, -956, 598, -997, 998, -1085, 1508 }),
                ("car", 11f, new double[] { -940, 332, -1538, 298 }), ("van", 9f, new double[] { -956, 598, -1140, 540 }),
                ("car", 9f, new double[] { 152, 560, 575, 560 }), ("car", 9f, new double[] { 575, 650, 158, 650 }), ("van", 8f, new double[] { 164, 740, 575, 740 }),
                ("car", 9f, new double[] { 300, 359, 300, 740 }), ("car", 9f, new double[] { 430, 331, 430, 740 }), ("car", 9f, new double[] { 575, 300, 575, 740 }),
                ("van", 9f, new double[] { -94, 525, -340, 525 }), ("car", 10f, new double[] { -90, 200, -90, 780 }),
                ("truck", 8f, new double[] { 640, 285, 640, 110, 1000, 110, 1000, 207 }), ("van", 11f, new double[] { 1000, 110, 1130, 110, 1160, -300, 1300, -1393 }),
                ("truck", 11f, new double[] { 1420, 262, 1750, 330, 2500, 300, 2600, 222 }), ("car", 12f, new double[] { 1420, 262, 1750, 330, 2500, 300, 2600, 222 }),
            };
            foreach (var (kind, speed, route) in traffic) c.AddVehicle(kind, Drive(kind == "bus" || kind == "truck" || kind == "tanker" ? 2.4 : 2.0, route), speed);
            // planes taxiing up and down beside runway 1, a fuel truck round the apron, a forklift on the pier and in the yard
            c.AddVehicle("plane", Drive(0, -415, -280, -415, 1030), 7f);
            c.AddVehicle("tanker", new[] { P(-395, 420), P(-345, 420), P(-345, 640), P(-395, 640) }, 4f);
            c.AddVehicle("forklift", Lift(3.5f, P(2700, 121), P(3380, 121), P(3380, 129), P(2700, 129)), 3f);
            c.AddVehicle("forklift", new[] { P(660, 175), P(705, 175), P(705, 180), P(660, 180) }, 2f);
            // crawlers taking rockets out to the east pads
            var crawler = CrawlerMesh();
            foreach (var (pe, pn) in new[] { (2192.0, -206.0), (2037.0, -428.0) })
            {
                double de = pe - 1300, dn = pn - 150, dl = System.Math.Sqrt(de * de + dn * dn);
                c.AddCrawler(crawler, P(1300 + de / dl * 70, 150 + dn / dl * 70) + Vector3.up * 0.12f, P(pe - de / dl * 70, pn - dn / dl * 70) + Vector3.up * 0.12f, 1.6f);
            }
            c.Role = Ardlings.RoleNone;   // off duty in town
            // people: round the town's blocks, the shops, the school field, the visitor centre's rocket garden, the
            // harbour, the industrial park, the north-west complex and the test stand
            foreach (var (e0, n0, e1, n1) in new[] { (170.0, 566.0, 296.0, 644.0), (306.0, 566.0, 424.0, 644.0), (436.0, 656.0, 569.0, 734.0), (306.0, 656.0, 424.0, 734.0) })
                for (int k = 0; k < 3; k++) c.AddWalker(new[] { P(e0 + k * 9, n0), P(e1, n0 + k), P(e1 - k, n1), P(e0, n1 - k * 4) });
            for (int k = 0; k < 6; k++) c.AddWalker(new[] { P(440 + k * 14, 506), P(565, 506 + k * 0.6), P(560, 496), P(442, 496) });
            for (int k = 0; k < 4; k++) c.AddWalker(new[] { P(322 + k * 4, 402), P(412, 404), P(412, 462), P(324, 460) }, 2.2f);
            c.Role = Ardlings.RoleVisitor;
            for (int k = 0; k < 8; k++)
            {
                var ring = new Vector3[12];
                for (int i = 0; i < 12; i++) { double th = (i + k * 1.5) * System.Math.PI * 2 / 12; ring[i] = P(652 + 64 * System.Math.Cos(th), 1160 + 30 * System.Math.Sin(th)); }
                c.AddWalker(ring, 1.0f);
            }
            c.Role = Ardlings.RoleWork;
            for (int k = 0; k < 4; k++) c.AddWalker(new[] { P(2540 + k * 6, 110), P(2660, 112), P(2676, 125), P(2560, 128) });
            for (int k = 0; k < 3; k++) c.AddWalker(Lift(3.5f, P(2720 + k * 8, 125), P(3400, 125)), 1.2f);
            for (int k = 0; k < 4; k++) c.AddWalker(new[] { P(660 + k * 5, 230), P(700, 232), P(705, 130), P(662, 128) });
            for (int k = 0; k < 4; k++) c.AddWalker(new[] { P(-1430 + k * 6, 1060), P(-1270, 1062), P(-1272, 1200), P(-1428, 1198) });
            for (int k = 0; k < 2; k++) c.AddWalker(new[] { P(-1160 + k * 4, 560), P(-1200, 560), P(-1250, 590) });
            for (int k = 0; k < 4; k++) c.AddWalker(new[] { P(1446 + k * 6, 200), P(1500, 204), P(1504, 258), P(1446, 256) });
            for (int k = 0; k < 3; k++) c.AddWalker(new[] { P(-236 + k * 5, 474), P(-176, 474), P(-176, 400), P(-236, 400) });
            return c;
        }

        // ---------------------------------------------------------------- rocks (milestone 25)

        /// <summary>
        /// Rocks and boulders round the close-up ground (milestone 25, captaincarl24: other planets "with like terrain and stuff"):
        /// small stones thick near the middle, bigger boulders further out. Where each one lies comes from a hash of a cell of
        /// space fixed to the world, so they stay put as the ground is rebuilt and are the same every visit. Each takes the
        /// colour of the ground it sits on (grey on Tal, rust on Ochre, pale on the ice worlds, black on Pyre), part buried.
        /// Arden only gets them on high, rocky ground away from the space centre and the sea. Seen, not felt: the physics
        /// still uses the smooth ground.
        /// </summary>
        void BuildScatter(Vector3d centerDir, double half)
        {
            scatterMesh.Clear();
            if (body.style == BodyStyle.Gas || body.style == BodyStyle.Star || half > 9000) return;
            float density = body.style switch { BodyStyle.Barren => 1f, BodyStyle.Desert => 0.8f, BodyStyle.Volcanic => 0.9f, BodyStyle.Icy => 0.55f, _ => 0.35f };
            var b = new MeshBuilder();
            double R = body.radius;
            Vector3d c0 = centerDir.normalized * R;
            Vector3d e1 = Vector3d.Cross(Vector3d.up, centerDir);
            if (e1.sqrMagnitude < 1e-8) e1 = Vector3d.Cross(Vector3d.right, centerDir);
            e1 = e1.normalized;
            Vector3d e2 = Vector3d.Cross(e1, centerDir.normalized).normalized;
            var seen = new HashSet<long>();
            // two layers: stones out to 260 m on a 7 m lattice, boulders out to 900 m on a 26 m lattice
            foreach (var (cell, reach, minR, maxR, chance) in new[] { (5.0, 170.0, 0.14f, 0.55f, 0.5f * density), (12.0, 450.0, 0.45f, 1.5f, 0.36f * density), (40.0, 1200.0, 1.4f, 4.8f, 0.26f * density) })
            {
                int steps = (int)(reach / cell);
                for (int j = -steps; j <= steps; j++)
                    for (int i = -steps; i <= steps; i++)
                    {
                        double x = i * cell, y = j * cell;
                        if (x * x + y * y > reach * reach) continue;
                        Vector3d pf = (c0 + e1 * x + e2 * y).normalized * R;
                        long kx = (long)System.Math.Floor(pf.x / cell), ky = (long)System.Math.Floor(pf.y / cell), kz = (long)System.Math.Floor(pf.z / cell);
                        long key = ((kx * 73856093L) ^ (ky * 19349663L) ^ (kz * 83492791L)) * 31 + (long)cell;
                        if (!seen.Add(key)) continue;
                        int hx = (int)(kx & 0x7fffffff), hy = (int)(ky & 0x7fffffff), hz = (int)(kz & 0x7fffffff);
                        if (Noise.Hash(hx, hy, hz, 501 + (int)cell) > chance) continue;
                        // where in the cell, how big, which way up
                        Vector3d jitter = new Vector3d(Noise.Hash(hx, hy, hz, 502), Noise.Hash(hx, hy, hz, 503), Noise.Hash(hx, hy, hz, 504)) * cell;
                        Vector3d at = new Vector3d(kx * cell, ky * cell, kz * cell) + jitter;
                        Vector3d dir = at.normalized;
                        double fromMiddle = Vector3d.Angle(dir, centerDir) * R;
                        if (fromMiddle > reach || fromMiddle < 18) continue;          // (none right where the ship is: it lands on smooth ground)
                        if (body.IsHomeWorld)
                        {
                            if (Vector3d.Angle(dir, LaunchSite.Direction) * R < 6000) continue;                       // not at the space centre
                            if (Terrain.ArdenField(body, dir.ToFloat()) < 0.2f) continue;                          // only on high, rocky ground
                        }
                        float size = Mathf.Lerp(minR, maxR, Mathf.Pow(Noise.Hash(hx, hy, hz, 505), 2.2f));
                        double h = Terrain.Height(body, dir);
                        Vector3d up = dir;
                        Vector3 pos = ((dir * (R + h - size * 0.35)) - c0).ToFloat();
                        Color ground = MeshFactory.BodyColor(body, dir.ToFloat());
                        float tone = 0.48f + 0.36f * Noise.Hash(hx, hy, hz, 506);
                        Color col = new Color(ground.r * tone, ground.g * tone, ground.b * tone, 1f);
                        if (body.style == BodyStyle.Icy) col = Color.Lerp(col, new Color(0.78f, 0.84f, 0.9f), 0.4f);
                        Quaternion rot = Quaternion.FromToRotation(Vector3.up, up.ToFloat()) * Quaternion.Euler(Noise.Hash(hx, hy, hz, 507) * 30f - 15f, Noise.Hash(hx, hy, hz, 508) * 360f, 0);
                        Rock(b, pos, size, rot, col, hx * 7 + hy * 13 + hz * 29);
                    }
            }
            if (b.v.Count == 0) return;
            b.Mat = MeshBuilder.MatConcrete;
            var m = b.ToMesh("Rocks");
            scatterMesh.SetVertices(m.vertices);
            scatterMesh.SetNormals(m.normals);
            scatterMesh.SetColors(m.colors);
            var uv = new List<Vector4>(); m.GetUVs(0, uv);
            scatterMesh.SetUVs(0, uv);
            scatterMesh.SetTriangles(m.triangles, 0);
            scatterMesh.RecalculateBounds();
            UnityEngine.Object.Destroy(m);
        }

        /// <summary>One rock: a low-poly ball pushed in and out by noise, flattened a little and shaded darker underneath.</summary>
        /// <summary>
        /// Milestone 27: the height of the ground as drawn (the close-up ground is a grid of flat triangles, which can be a
        /// little above or below the true height between its corners), so prints and tracks lie right on it.
        /// </summary>
        public double DrawnHeight(Vector3d fixedDir)
        {
            if (patchHalf <= 0 || patchResUsed <= 0 || !patch.activeSelf) return Terrain.Height(body, fixedDir);
            Vector3d c = patchCenter.normalized;
            Vector3d e1 = Vector3d.Cross(Vector3d.up, c);
            if (e1.sqrMagnitude < 1e-8) e1 = Vector3d.Cross(Vector3d.right, c);
            e1 = e1.normalized;
            Vector3d e2 = Vector3d.Cross(e1, c).normalized;
            double R = body.radius, half = patchHalf;
            int res = patchResUsed;
            Vector3d d = fixedDir.normalized;
            double dc = Vector3d.Dot(d, c);
            if (dc <= 0.5) return Terrain.Height(body, d);
            Vector3d p = d * (R / dc);
            double fi = (Vector3d.Dot(p, e1) + half) / (2 * half) * res, fj = (Vector3d.Dot(p, e2) + half) / (2 * half) * res;
            if (fi < 0 || fj < 0 || fi >= res || fj >= res) return Terrain.Height(body, d);
            int i = (int)System.Math.Floor(fi), j = (int)System.Math.Floor(fj);
            double u = fi - i, v = fj - j;
            Vector3d c0 = c * R;
            double Hc(int ii, int jj) => Terrain.Height(body, (c0 + e1 * (-half + 2 * half * ii / res) + e2 * (-half + 2 * half * jj / res)).normalized);
            double hbl = Hc(i, j), htr = Hc(i + 1, j + 1);
            // the same split as the mesh: corner to corner from bottom-left to top-right
            if (v >= u) { double htl = Hc(i, j + 1); return hbl + u * (htr - htl) + v * (htl - hbl); }
            double hbr = Hc(i + 1, j);
            return hbl + u * (hbr - hbl) + v * (htr - hbr);
        }

        /// <summary>
        /// Milestone 27: the places to discover near the close-up ground stand on it (children of the patch, so they turn
        /// with the world); built the first time they're needed.
        /// </summary>
        void PlaceLandmarks(Vector3d centerDir, double half)
        {
            if (body.style == BodyStyle.Gas || body.style == BodyStyle.Star) return;
            double R = body.radius;
            Vector3d c0 = centerDir.normalized * R;
            double reach = System.Math.Min(System.Math.Max(half * 0.95, 8000), 70_000);
            foreach (var l in Landmarks.On(body).Concat(Outposts.On(body)))   // milestone 34: outposts and wrecks too
            {
                bool show = Vector3d.Angle(l.dir, centerDir) * R < reach;
                landmarkObjs.TryGetValue(l.id, out var go);
                if (!show) { if (go != null) go.SetActive(false); continue; }
                if (go == null)
                {
                    go = LandmarkMeshes.Create(l, body);
                    go.transform.SetParent(patch.transform, false);
                    landmarkObjs[l.id] = go;
                }
                double h = Terrain.Height(body, l.dir);
                go.transform.localPosition = (l.dir * (R + h) - c0).ToFloat();
                go.transform.localRotation = Landmarks.Frame(l);
                go.SetActive(true);
            }
        }

        static void Rock(MeshBuilder b, Vector3 at, float size, Quaternion rot, Color col, int seed)
        {
            b.Mat = MeshBuilder.MatConcrete;
            int s0 = b.v.Count;
            b.AddSphere(Vector3.zero, 1f, col, 8, 6);
            float sq = 0.55f + 0.3f * Mathf.PerlinNoise(seed * 0.013f, 0.7f);
            for (int i = s0; i < b.v.Count; i++)
            {
                Vector3 d = b.v[i];
                float bump = 0.75f + 0.5f * Mathf.PerlinNoise(d.x * 1.7f + seed * 0.37f, d.z * 1.7f + d.y * 1.3f + seed * 0.11f);
                Vector3 p = new Vector3(d.x * bump, d.y * bump * sq, d.z * bump * (0.8f + 0.3f * Mathf.PerlinNoise(seed * 0.05f, 2.1f)));
                b.v[i] = at + rot * (p * size);
                b.n[i] = rot * new Vector3(d.x, d.y / sq, d.z).normalized;
                float shade = Mathf.Lerp(0.6f, 1.05f, d.y * 0.5f + 0.5f);
                b.c[i] = new Color(col.r * shade, col.g * shade, col.b * shade, 1f);
            }
        }

        /// <summary>For the autotest log: where the close-up ground is and how big.</summary>
        public string PatchInfo()
        {
            var b = patchMesh.bounds;
            return $"{body.name} patch active={patch.activeSelf} half={patchHalf:0} verts={patchMesh.vertexCount} at {patch.transform.position} bounds {b.center}±{b.extents} layer {patch.layer}";
        }

        /// <summary>Where a spot at the space centre (metres east and north of the pad, h up) is in the world right now.</summary>
        public Vector3 SiteWorld(double e, double n, float h) => site != null ? site.transform.TransformPoint(At(e, n) + Vector3.up * h) : Vector3.zero;

        /// <summary>The launch site (Arden only): things placed on it follow the ground as the world turns.</summary>
        public Transform SiteTransform => site != null ? site.transform : null;

        public void SetVisible(bool v)
        {
            sphere.SetActive(v);
            if (!v) patch.SetActive(false);
            if (site != null) site.SetActive(v);
            if (clouds != null) clouds.SetActive(v);
            if (atmosphere != null) atmosphere.SetActive(v);
        }

        public void Sync(Vector3d originAbs, double ut, Vector3d vesselAbs)
        {
            Vector3d bodyAbs = body.AbsolutePosition(ut);
            Quaternion q = body.RotationAt(ut);
            Vector3 centre = (bodyAbs - originAbs).ToFloat();
            planetMat.SetVector(RenderEnv.PlanetCenter, centre);
            if (cloudMat != null) cloudMat.SetVector(RenderEnv.PlanetCenter, centre);
            if (airMat != null) airMat.SetVector(RenderEnv.PlanetCenter, centre);
            if (ringMat != null) ringMat.SetVector(RenderEnv.PlanetCenter, centre);
            sphere.transform.SetPositionAndRotation((bodyAbs - originAbs).ToFloat(), q);
            if (clouds != null) clouds.transform.SetPositionAndRotation((bodyAbs - originAbs).ToFloat(), q);
            if (atmosphere != null) atmosphere.transform.SetPositionAndRotation((bodyAbs - originAbs).ToFloat(), q);

            Vector3d rel = vesselAbs - bodyAbs;
            double alt = rel.magnitude - body.radius;
            bool showPatch = alt < 70_000;
            if (patch.activeSelf != showPatch) patch.SetActive(showPatch);
            if (showPatch)
            {
                Vector3d dir = body.InertialToFixed(rel, ut).normalized;
                double aboveGround = System.Math.Max(0, alt - Terrain.Height(body, dir)); // high ground needs the fine patch too
                // At the space centre the close-up ground reaches the coast all round the peninsula (milestone 24)
                bool atSite = site != null && Vector3d.Angle(dir, LaunchSite.Direction) * body.radius < 1500;   // (Angle is in radians)
                double groundSpeed = ut > lastSyncUt && lastSyncDir.sqrMagnitude > 0 ? Vector3d.Angle(dir, lastSyncDir) * body.radius / (ut - lastSyncUt) : 0;
                if (ut != lastSyncUt) { lastSyncDir = dir; lastSyncUt = ut; }
                bool standing = atSite && aboveGround < 400 && groundSpeed < 2;
                double half = System.Math.Max(atSite ? 4300 : 3000, System.Math.Min(90_000, aboveGround * 2 + 3000));
                // Above the cloud layer the close-up ground shows the clouds too (they'd otherwise vanish over the patch).
                float cloudPaint = clouds != null ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(MeshFactory.CloudAltitude, MeshFactory.CloudAltitude * 2.4f, (float)alt)) : 0f;
                if (mapsReady) cloudPaint = Mathf.Round(cloudPaint * 20f) / 20f;
                bool rebuild = patchHalf <= 0
                               || Vector3d.Angle(dir, patchCenter) * body.radius > patchHalf * 0.2
                               || half / patchHalf > 1.35 || half / patchHalf < 0.7
                               || Mathf.Abs(cloudPaint - patchClouds) > 0.2f;
                if (rebuild)
                {
                    patchCenter = dir;
                    patchHalf = half;
                    patchClouds = cloudPaint;
                    int patchRes = half < 8000 ? (standing ? 150 : !body.IsHomeWorld && aboveGround < 800 ? 128 : half > 4000 ? 116 : 96) : 64;
                    MeshFactory.BuildPatch(patchMesh, body, dir, half, patchRes, mapsReady ? 0f : cloudPaint);
                    patchResUsed = patchRes;
                    PatchVersion++;
                    // Milestone 26: high up, the patch's vertices are far apart (2 km at 30 km up) and craters vanish between
                    // them; the baked relief is finer then, so the shader shades with it
                    double spacing = 2 * half / patchRes, texel = System.Math.PI * 0.5 * body.radius / PlanetBaker.ResFor(body);
                    patchMapBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 2.2f, (float)(spacing / texel)));
                    BuildScatter(dir, half);
                    PlaceLandmarks(dir, half);
                    // The patch's place on the world, for the ground photos (offset mod 1024 m so the pattern doesn't
                    // jump when the patch moves) and the baked maps
                    Vector3d c0 = patchCenter * body.radius;
                    double M(double x) { double m = x % 1024.0; return m < 0 ? m + 1024.0 : m; }
                    patchProps ??= new MaterialPropertyBlock();
                    patchProps.SetVector("_PatchCenterPF", new Vector4((float)c0.x, (float)c0.y, (float)c0.z, 1f));
                    patchProps.SetFloat("_PatchHalf", (float)half);
                    patchProps.SetFloat("_MapBlend", patchMapBlend);
                    patchProps.SetVector("_PatchOffset", new Vector4((float)M(c0.x), (float)M(c0.y), (float)M(c0.z), 0f));
                }
                if (patchProps != null)
                {
                    patchProps.SetFloat("_CloudPaint", mapsReady ? cloudPaint : 0f);
                    patchProps.SetFloat("_Detail", RenderEnv.Quality == 0 ? 0f : 1f);
                    patch.GetComponent<MeshRenderer>().SetPropertyBlock(patchProps);
                }
                patch.transform.SetPositionAndRotation((bodyAbs + body.FixedToInertial(patchCenter * body.radius, ut) - originAbs).ToFloat(), q);
            }

            if (site != null)
            {
                Vector3d sitePos = bodyAbs + body.FixedToInertial(siteFixedPos, ut);
                bool showSite = Vector3d.Distance(sitePos, vesselAbs) < 120_000;
                if (site.activeSelf != showSite) site.SetActive(showSite);
                if (showSite && siteCrowd != null) siteCrowd.Update(UnityEngine.Time.unscaledDeltaTime);
                // the lights come on after sunset (and only while the site is near enough to see)
                bool wantLights = showSite && siteLights != null && Vector3.Dot(site.transform.up, GameRoot.SunNow) < 0.04f;
                if (siteLights != null && wantLights != siteLightsOn)
                {
                    siteLightsOn = wantLights;
                    foreach (var l in siteLights) if (l != null) l.enabled = wantLights;
                }
                if (showSite) site.transform.SetPositionAndRotation((sitePos - originAbs).ToFloat(), q * siteLocalRot);
            }
        }
    }

    /// <summary>Stars and the sun, drawn far out around the camera; the stars fade in daylight under the air.</summary>
    public class SkyView
    {
        readonly GameObject stars, sun;
        readonly Material starMat, sunMat;
        /// <summary>The Milky Way's tilt against Arden's orbit (the map is in Earth's sky coordinates).</summary>
        static readonly Quaternion SkyTilt = Quaternion.Euler(-28f, 40f, 12f);
        /// <summary>Colour shift of the sun seen through air (set by flight; white in space).</summary>
        public static Color SunTint = Color.white;
        public static float StarExposure = 1f;

        public SkyView()
        {
            stars = new GameObject("Stars") { layer = GameRoot.LayerFar };
            stars.AddComponent<MeshFilter>().sharedMesh = MeshFactory.StarSphere();
            var sr = stars.AddComponent<MeshRenderer>();
            starMat = new Material(Mats.Stars);
            sr.sharedMaterial = starMat;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sun = new GameObject("Sun disc") { layer = GameRoot.LayerFar };
            sun.AddComponent<MeshFilter>().sharedMesh = MeshFactory.SunDisc();
            var su = sun.AddComponent<MeshRenderer>();
            sunMat = new Material(Mats.Sun);
            su.sharedMaterial = sunMat;
            su.shadowCastingMode = ShadowCastingMode.Off;
        }

        public void SetVisible(bool v) { stars.SetActive(v); sun.SetActive(v); }

        /// <param name="starAlpha">1 in space or at night, fading to 0 in a daylit sky.</param>
        public void Sync(Camera cam, float starAlpha)
        {
            float r = cam.farClipPlane * 0.45f;
            Vector3 c = cam.transform.position;
            stars.transform.SetPositionAndRotation(c, SkyTilt);
            stars.transform.localScale = Vector3.one * r;
            starMat.SetFloat("_Exposure", StarExposure * Mathf.Lerp(0.05f, 1f, Mathf.Clamp01(starAlpha)));
            sunMat.SetColor("_Color", new Color(SunTint.r * 30f, SunTint.g * 28f, SunTint.b * 25f, 1f));
            Vector3 s = GameRoot.SunNow;
            sun.transform.SetPositionAndRotation(c + s * r * 0.9f, Quaternion.LookRotation(-s, Mathf.Abs(s.y) < 0.95f ? Vector3.up : Vector3.right));
            sun.transform.localScale = Vector3.one * r * 0.9f * 0.07f;
        }
    }

    /// <summary>
    /// Map-view ore overlay for one world: a slightly larger sphere tinted by what the survey has mapped. Unmapped ground is
    /// veiled dark; mapped ground runs from faint blue (no ore) through yellow to red (rich).
    /// </summary>
    public class OreOverlay
    {
        static Material mat;
        readonly CelestialBody body;
        readonly GameObject go;
        readonly Mesh mesh;
        readonly float[] conc;
        readonly int[] cell;
        readonly Color[] cols;
        int shownVersion = -1;

        public OreOverlay(CelestialBody b, int res = 40)
        {
            body = b;
            if (mat == null) { mat = new Material(Mats.Unlit) { name = "OreOverlay", renderQueue = 3100 }; }
            var bld = new MeshBuilder();
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back, Vector3.up, Vector3.down };
            Vector3[] ups = { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.forward, Vector3.forward };
            float R = OverlayRadius(b); // above the highest mountains and the clouds
            for (int f = 0; f < 6; f++)
            {
                Vector3 N = normals[f], W = ups[f], Rt = Vector3.Cross(N, W);
                int start = bld.v.Count;
                for (int j = 0; j <= res; j++)
                    for (int i = 0; i <= res; i++)
                    {
                        Vector3 dir = (N + Rt * (2f * i / res - 1f) + W * (2f * j / res - 1f)).normalized;
                        bld.v.Add(dir * R); bld.n.Add(dir); bld.c.Add(Color.clear);
                    }
                int row = res + 1;
                for (int j = 0; j < res; j++)
                    for (int i = 0; i < res; i++)
                    {
                        int bl = start + j * row + i, br = bl + 1, tl = bl + row, tr = tl + 1;
                        bld.t.Add(tl); bld.t.Add(tr); bld.t.Add(bl);
                        bld.t.Add(tr); bld.t.Add(br); bld.t.Add(bl);
                    }
            }
            mesh = bld.ToMesh("OreOverlay " + b.name);
            mesh.MarkDynamic();
            int n = bld.v.Count;
            conc = new float[n];
            cell = new int[n];
            cols = new Color[n];
            for (int k = 0; k < n; k++)
            {
                var d = Vector3d.FromFloat(bld.n[k]);
                conc[k] = (float)Ore.Concentration(b, d);
                cell[k] = OreSurvey.CellOf(d);
            }
            go = new GameObject("OreOverlay " + b.name) { layer = GameRoot.LayerMap };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
        }

        /// <summary>Map layers float above the highest ground and, on worlds with air, above the clouds too.</summary>
        public static float OverlayRadius(CelestialBody b) => (float)(b.radius + System.Math.Max(Terrain.MaxHeight(b) + 600, b.HasAtmosphere ? MeshFactory.CloudAltitude + 1500 : 0));

        public static Color OreColor(double c, bool mapped)
        {
            if (!mapped) return new Color(0.03f, 0.03f, 0.05f, 0.62f);
            if (c < 0.004) return new Color(0.3f, 0.45f, 0.75f, 0.2f);
            float t = Mathf.Clamp01((float)(c / 0.16));
            Color a = new Color(1f, 0.93f, 0.3f), m = new Color(1f, 0.52f, 0.12f), z = new Color(0.95f, 0.12f, 0.32f);
            Color col = t < 0.5f ? Color.Lerp(a, m, t * 2f) : Color.Lerp(m, z, (t - 0.5f) * 2f);
            col.a = 0.5f + 0.4f * t;
            return col;
        }

        public void Show(OreSurvey survey, Vector3 pos, Quaternion rot)
        {
            if (survey != null && survey.version != shownVersion)
            {
                shownVersion = survey.version;
                for (int k = 0; k < cols.Length; k++) cols[k] = OreColor(conc[k], survey.CellScanned(body, cell[k]));
                mesh.colors = cols;
            }
            go.transform.SetPositionAndRotation(pos, rot);
            if (!go.activeSelf) go.SetActive(true);
        }

        public void Hide() { if (go.activeSelf) go.SetActive(false); }
    }

    /// <summary>The map's region view: each world's named regions in their own colours, just above the ground.</summary>
    public class RegionOverlay
    {
        static Material mat;
        readonly GameObject go;

        public RegionOverlay(CelestialBody b, int res = 48)
        {
            if (mat == null) { mat = new Material(Mats.Unlit) { name = "RegionOverlay", renderQueue = 3100 }; }
            var bld = new MeshBuilder();
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back, Vector3.up, Vector3.down };
            Vector3[] ups = { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.forward, Vector3.forward };
            float R = OreOverlay.OverlayRadius(b);
            for (int f = 0; f < 6; f++)
            {
                Vector3 N = normals[f], W = ups[f], Rt = Vector3.Cross(N, W);
                int start = bld.v.Count;
                for (int j = 0; j <= res; j++)
                    for (int i = 0; i <= res; i++)
                    {
                        Vector3 dir = (N + Rt * (2f * i / res - 1f) + W * (2f * j / res - 1f)).normalized;
                        Color c = Regions.ColorOf(b, Regions.IndexAt(b, Vector3d.FromFloat(dir)));
                        c.a = 0.62f;
                        bld.v.Add(dir * R); bld.n.Add(dir); bld.c.Add(c);
                    }
                int row = res + 1;
                for (int j = 0; j < res; j++)
                    for (int i = 0; i < res; i++)
                    {
                        int bl = start + j * row + i, br = bl + 1, tl = bl + row, tr = tl + 1;
                        bld.t.Add(tl); bld.t.Add(tr); bld.t.Add(bl);
                        bld.t.Add(tr); bld.t.Add(br); bld.t.Add(bl);
                    }
            }
            go = new GameObject("RegionOverlay " + b.name) { layer = GameRoot.LayerMap };
            go.AddComponent<MeshFilter>().sharedMesh = bld.ToMesh("RegionOverlay " + b.name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
        }

        public void Show(Vector3 pos, Quaternion rot)
        {
            go.transform.SetPositionAndRotation(pos, rot);
            if (!go.activeSelf) go.SetActive(true);
        }

        public void Hide() { if (go.activeSelf) go.SetActive(false); }
    }

    /// <summary>A polyline on the map layer (orbits, trajectory pieces, the moon's path).</summary>
    public class OrbitLine
    {
        readonly GameObject go;
        readonly LineRenderer lr;

        public OrbitLine(Color color)
        {
            go = new GameObject("OrbitLine") { layer = GameRoot.LayerMap };
            lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.TransformZ;
            lr.sharedMaterial = Mats.Unlit;
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            go.SetActive(false);
        }

        public void SetColor(Color c) { lr.startColor = lr.endColor = c; }

        public void Show(List<Vector3> pts, float width)
        {
            // Face the camera's view direction rather than its position: at millions of km the per-point "toward the
            // camera" maths loses the strip (whole orbits vanished when zoomed far out).
            var cam = Camera.main;
            if (cam != null) go.transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
            lr.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) lr.SetPosition(i, pts[i]);
            lr.widthMultiplier = width;
            if (!go.activeSelf) go.SetActive(true);
        }

        public void Hide() { if (go.activeSelf) go.SetActive(false); }
        public void Destroy() { Object.Destroy(go); }
    }
}
