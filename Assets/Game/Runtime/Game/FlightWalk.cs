using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 34 (captaincarl24: "the ardlings can get out and it has first person, 3rd person, and you can move and
    /// jump around with it ... once you land or crash land but didn't explode you can exit the spacecraft"):
    /// - ENTER after any landing the crew lived through (a crash too, if the capsule's in one piece) sends someone out.
    /// - C switches between third person (the camera follows behind as you walk; right-drag to look round) and first
    ///   person (through the helmet: the mouse looks round, A/D step sideways; hold ALT for the pointer).
    /// - CTRL runs, SPACE jumps (higher than before: about half a metre on Arden, nearly 3 m on Tal).
    /// </summary>
    public partial class FlightMode
    {
        /// <summary>Looking through the astronaut's helmet (C on EVA).</summary>
        public bool firstPerson;
        float fpPitch;
        VesselView fpHiddenView;
        VesselSim landedPromptFor;
        const float EyeHeight = 0.42f;

        /// <summary>First person is showing right now (on EVA, not in the map, photo mode or a replay).</summary>
        public bool FirstPersonView => firstPerson && active != null && active.IsEva && !active.destroyed && !mapView && !photoMode && replay == null;

        /// <summary>The mouse is steering the view (first person, nothing in the way, ALT not held).</summary>
        bool MouseLooking => (FirstPersonView || (inside && !mapView)) && !paused && !Glossary.Open && !In.Held(K.LeftAlt) && Application.isFocused;

        /// <summary>Walking extras, after HandleEvaKeys has read W/S/A/D: the camera key, running, strafing, the mouse.</summary>
        void HandleWalkInput(VesselSim v)
        {
            if (In.Down(K.C))
            {
                firstPerson = !firstPerson;
                fpPitch = 0;
                Show(firstPerson ? $"First person: the mouse looks round, {In.Name(K.A)}/{In.Name(K.D)} step sideways ({In.Name(K.LeftAlt)} frees the pointer). {In.Name(K.C)}: third person."
                                 : $"Third person: the camera follows you; right-drag to look round. {In.Name(K.C)}: first person.", 4f);
            }
            v.evaStrafe = 0;
            v.evaRun = false;
            if (v.landed)
            {
                v.evaRun = In.Held(K.LeftCtrl);
                if (firstPerson && walkOverride == null) { v.evaStrafe = v.inputYaw; v.inputYaw = 0; }
            }
            if (MouseLooking)
            {
                var d = In.MouseDelta;
                fpPitch = Mathf.Clamp(fpPitch + d.y * 0.12f, -80f, 80f);
                if (v.landed) v.evaLookYaw += d.x * 0.12f;
                else v.inputYaw = Mathf.Clamp(d.x * 0.05f, -1f, 1f);
            }
            // third person: swing round behind the astronaut as they walk (unless you're looking round yourself)
            if (!firstPerson && v.landed && !In.RightMouse && (Mathf.Abs(v.inputPitch) > 0.01f || Mathf.Abs(v.inputYaw) > 0.01f || Mathf.Abs(v.evaStrafe) > 0.01f))
            {
                Vector3 up = renderRel.normalized.ToFloat();
                Vector3 north = Vector3.ProjectOnPlane(Vector3.up, up);
                if (north.sqrMagnitude < 1e-4f) north = Vector3.ProjectOnPlane(Vector3.right, up);
                north.Normalize();
                Vector3 east = Vector3.Cross(up, north);
                Vector3 f = Vector3.ProjectOnPlane(renderRot * Vector3.forward, up);
                if (f.sqrMagnitude > 1e-4f)
                {
                    float behind = Mathf.Atan2(Vector3.Dot(-f, east), Vector3.Dot(-f, north)) * Mathf.Rad2Deg;
                    camYaw = Mathf.MoveTowardsAngle(camYaw, behind, 140f * Time.unscaledDeltaTime);
                    camPitch = Mathf.MoveTowards(camPitch, 14f, 20f * Time.unscaledDeltaTime);
                }
            }
        }

        /// <summary>Locks the pointer while the mouse looks round in first person; frees it otherwise.</summary>
        void UpdateCursor()
        {
            var want = MouseLooking ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != want) { Cursor.lockState = want; Cursor.visible = want == CursorLockMode.None; }
        }

        static void FreeCursor()
        {
            if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        /// <summary>First person: the camera at the helmet, looking where the astronaut faces (and up or down with the mouse).</summary>
        void ApplyFirstPersonCamera(Camera cam)
        {
            if (!FirstPersonView)
            {
                if (fpHiddenView != null) { fpHiddenView.SetVisible(true); fpHiddenView = null; }
                return;
            }
            Vector3 up = renderRot * Vector3.up, fwd = renderRot * Vector3.forward, right = renderRot * Vector3.right;
            cam.transform.position = up * EyeHeight + fwd * 0.12f;
            cam.transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(-fpPitch, right) * fwd, up);
            // your own suit would fill the view: hide it while looking out of it
            if (views.TryGetValue(active, out var view) && view != fpHiddenView)
            {
                if (fpHiddenView != null) fpHiddenView.SetVisible(true);
                view.SetVisible(false);
                fpHiddenView = view;
            }
        }

        /// <summary>
        /// After a landing the crew lived through (soft, or a crash that left the capsule in one piece): a reminder that
        /// ENTER sends someone out, and the key itself.
        /// </summary>
        void HandleGetOut(VesselSim v)
        {
            if (v.IsEva || v.destroyed) { landedPromptFor = null; return; }
            bool down = v.landed && v.hasLaunched && v.SurfaceRelVelocity.magnitude < 1.0;
            PartState seat = null;
            if (down) foreach (var p in v.parts) if (p.crew.Count > 0) { seat = p; break; }
            if (!down || seat == null) { if (!v.landed) landedPromptFor = null; return; }
            if (landedPromptFor != v)
            {
                landedPromptFor = v;
                Show($"Down safely. {In.Name(K.Enter)}: get out and walk round ({In.Name(K.C)} changes the view once you're out)", 6f);
            }
            if (In.Down(K.Enter)) GoEva(seat);
        }
    }
}
