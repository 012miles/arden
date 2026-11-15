using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 41 (captaincarl24: "when you crash while in first person its also super buggy, get a death screen too"): a
    /// crash used to leave you inside the cabin of a ship that wasn't there any more, with the pointer still locked so nothing
    /// could be clicked. Now the moment the ship is lost the cabin and first person are left, the pointer is freed, the camera
    /// pulls back to look at the wreck, and a death screen fades in: what happened, who was aboard, how far the flight got, a
    /// tip for next time, then the ways on (revert, quickload, replay, the builder, the main menu).
    /// </summary>
    public partial class FlightMode
    {
        VesselSim deathFor, statsFor;
        int deathId = int.MinValue, statsId = int.MinValue;   // milestone 43: by the ship's number (a room renews the ship object twice a second)
        static int IdOf(VesselSim v) => v.id != 0 ? v.id : v.GetHashCode();
        float deathAt;
        string deathWhy, deathTitle;
        List<string> deathCrew = new List<string>();
        double flightMaxAlt, flightMaxSpeed;

        /// <summary>Every frame: the flight's best height and speed, and the moment the ship is lost.</summary>
        void DeathTick()
        {
            var v = active;
            if (v == null) return;
            if (statsId != IdOf(v)) { statsId = IdOf(v); flightMaxAlt = 0; flightMaxSpeed = 0; }
            statsFor = v;
            if (!v.destroyed)
            {
                if (deathFor != null && deathId != IdOf(v)) { deathFor = null; deathId = int.MinValue; }   // flying something else now
                if (v.hasLaunched || v.IsEva)
                {
                    flightMaxAlt = System.Math.Max(flightMaxAlt, v.Altitude);
                    flightMaxSpeed = System.Math.Max(flightMaxSpeed, v.SurfaceRelVelocity.magnitude);
                }
                return;
            }
            if (deathId == IdOf(v)) { deathFor = v; return; }   // (the same ship: the screen carries on, it doesn't start again)
            // just lost
            deathFor = v;
            deathId = IdOf(v);
            deathAt = Time.unscaledTime;
            deathWhy = string.IsNullOrEmpty(v.destroyReason) ? "The ship was destroyed." : v.destroyReason;
            deathCrew = v.parts.SelectMany(p => p.crew).Distinct().ToList();
            if (deathCrew.Count == 0 && v.IsEva) deathCrew.Add(v.name);
            string me = Coop.Active ? Coop.Current.room.Me?.name : null;
            bool youWereThere = inside || firstPerson || v.IsEva || (me != null && deathCrew.Contains(me));
            deathTitle = deathCrew.Count == 0 ? "VESSEL DESTROYED" : youWereThere ? "YOU DIED" : deathCrew.Count == 1 ? "CREW LOST" : "CREW LOST";
            // out of the cabin and the helmet, the pointer back, the camera back to see what's left
            if (inside) LeaveInterior();
            firstPerson = false;
            packOpen = false; tradeOpen = false;
            FreeCursor();
            warpTargetUt = null; autoBurn = false;
            SetWarpOne();
            camDist = Mathf.Clamp(Mathf.Max(camDist, (float)v.Length * 3f + 30f), 25f, 400f);
            camPitch = Mathf.Max(camPitch, 18f);
            audio?.Clunk(1f);
        }

        /// <summary>For tours: whether the death screen's up, and its title.</summary>
        public string DeathScreenTitle => active != null && active.destroyed && deathId == IdOf(active) ? deathTitle : null;

        /// <summary>A tip that fits how it went wrong.</summary>
        public static string DeathTip(string why)
        {
            string w = (why ?? "").ToLowerInvariant();
            if (w.Contains("burned up on reentry")) return "Tip: come in with a heat shield facing forward, and a shallower path through the air (a lower periapsis bites harder).";
            if (w.Contains("crushed by the airflow")) return "Tip: thick air low down squeezes hard at speed. Throttle back until you're higher up, or add a nose cone.";
            if (w.Contains("crashed") || w.Contains("hit the ground") || w.Contains("hit the sea")) return "Tip: touch down slower than about 6 m/s. Open the parachutes lower down, or burn the engines to slow the fall; landing legs help.";
            if (w.Contains("knocked out")) return "Tip: carry a revive kit. If you're knocked out it's used by itself; a teammate with one can get you up.";
            if (w.Contains("sank")) return "Tip: engines and heavy parts sink. Drop them before you come down on the sea, so only the capsule splashes down.";
            if (w.Contains("supplies")) return "Tip: watch the supplies (food, water, air) on long trips, and resupply or bring the crew home in time.";
            if (w.Contains("clouds")) return "Tip: gas giants have no ground. Orbit them, or visit their moons.";
            if (w.Contains("too close")) return "Tip: keep well away from the sun.";
            return "Tip: the replay shows what happened, frame by frame.";
        }

        /// <summary>The death screen: a red fade, what happened, then the ways on underneath.</summary>
        void DrawDeathScreen(float W, float H)
        {
            if (deathId != IdOf(active)) { DrawEndPanel("Vessel destroyed", active.destroyReason); return; }
            float t = Time.unscaledTime - deathAt;
            float a = Mathf.Clamp01(t / 1.4f);
            var was = GUI.color;
            // the red fade, darker at the edges, and black bars sliding in
            GUI.color = new Color(0.22f, 0f, 0f, 0.5f * a);
            GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            for (int i = 0; i < 6; i++)
            {
                float e = (i + 1) * Mathf.Min(W, H) * 0.035f;
                GUI.color = new Color(0f, 0f, 0f, 0.09f * a);
                GUI.DrawTexture(new Rect(0, 0, e, H), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W - e, 0, e, H), Texture2D.whiteTexture);
            }
            float bar = H * 0.09f * Mathf.SmoothStep(0, 1, a);
            GUI.color = new Color(0, 0, 0, 0.92f);
            GUI.DrawTexture(new Rect(0, 0, W, bar), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, H - bar, W, bar), Texture2D.whiteTexture);
            GUI.color = was;

            // the words
            float tw = Mathf.Min(W - 40, 900), tx = W / 2 - tw / 2;
            float ty = H * 0.13f;
            int big = Mathf.RoundToInt(Mathf.Lerp(46, 64, Mathf.SmoothStep(0, 1, a)));
            var title = new GUIStyle(Ui.BigCenter) { fontSize = big, fontStyle = FontStyle.Bold, wordWrap = false };
            GUI.color = new Color(1, 1, 1, a);
            Ui.Shadowed(new Rect(tx, ty, tw, 76), $"<color=#ff4a3d>{deathTitle}</color>", title);
            ty += 80;
            var mid = new GUIStyle(Ui.BigCenter) { fontSize = 19, wordWrap = true };
            float wh = Mathf.Max(28, mid.CalcHeight(new GUIContent(Words.Say(deathWhy)), tw));
            Ui.Shadowed(new Rect(tx, ty, tw, wh), Words.Say(deathWhy), mid);
            ty += wh + 4;
            var small = new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 15 };
            if (deathCrew.Count > 0)
            {
                string names = deathCrew.Count == 1 ? deathCrew[0] : string.Join(", ", deathCrew.Take(deathCrew.Count - 1)) + " and " + deathCrew[deathCrew.Count - 1];
                Ui.Shadowed(new Rect(tx, ty, tw, 24), $"Aboard: {names}", small);
                ty += 24;
            }
            if (active.hasLaunched)
            {
                double flown = System.Math.Max(0, ut - active.launchUt);
                Ui.Shadowed(new Rect(tx, ty, tw, 24), $"Flight time {Ui.Time(flown)}   ·   highest {Ui.Dist(flightMaxAlt)}   ·   fastest {flightMaxSpeed:0} m/s", small);
                ty += 24;
            }
            var tip = new GUIStyle(small) { fontStyle = FontStyle.Italic };
            float tipH = Mathf.Max(24, tip.CalcHeight(new GUIContent(DeathTip(deathWhy)), tw));
            Ui.Shadowed(new Rect(tx, ty, tw, tipH), $"<color=#ffd9a0>{DeathTip(deathWhy)}</color>", tip);
            ty += tipH + 10;
            GUI.color = was;

            // the ways on, once the moment's sunk in (so a key still held from flying doesn't click one)
            if (t < 1.1f) return;
            if (Coop.Active) { DrawCoopPause(W, H, Mathf.Max(ty, H * 0.42f)); return; }
            DrawEndPanel(null, null, Mathf.Max(ty, H * 0.42f));
        }
    }
}
