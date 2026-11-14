using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// The sound of flight (milestone 21: recorded sounds, CC0/CC-BY, over the old synthesized layers):
    /// a deep recorded rocket rumble plus a crackling roar in air, which thin out to the muffled engine you hear through
    /// the hull in vacuum; an ignition roar when engines light in air; jet whine; wind that builds with dynamic pressure
    /// and turns into the roar of reentry; the RCS and jetpack hiss with a puff as they start; metal thuds and clinks
    /// for staging, touchdown and the hatch, a deep clank for docking, recorded explosions and parachute whooshes.
    /// </summary>
    public class FlightAudio
    {
        public static float Volume
        {
            get => PlayerPrefs.GetFloat("volume", 0.8f);
            set { PlayerPrefs.SetFloat("volume", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        readonly AudioSource engine, roarLow, engineVac, jet, wind, plasma, hiss, shots;
        readonly AudioLowPassFilter engineLp, roarLp, windLp, plasmaLp;
        readonly AudioClip clunk, clank, whoosh, ignition, thud, clink, metalClank, puff;
        readonly AudioClip[] booms;
        readonly Dictionary<int, bool> chuteOpen = new Dictionary<int, bool>();
        VesselSim tracked;
        bool wasBurning, wasRcs;
        float lastIgnition = -10f;
        int boomIndex;

        public FlightAudio(GameObject host)
        {
            var ga = GameAudio.I;
            engine = Loop(host, "rumble", SoundSynth.Rumble(), out engineLp);
            roarLow = Loop(host, "roar", ga?.Clip("engine_low"), out roarLp);
            engineVac = Loop(host, "engine vac", ga?.Clip("engine_vac"), out _);
            jet = Loop(host, "jet", SoundSynth.Jet(), out _);
            wind = Loop(host, "wind", ga?.Clip("wind") ?? Clip("wind", SoundSynth.Wind()), out windLp);
            plasma = Loop(host, "plasma", ga?.Clip("wind") ?? Clip("wind", SoundSynth.Wind()), out plasmaLp);
            hiss = Loop(host, "hiss", ga?.Clip("rcs") ?? Clip("hiss", SoundSynth.Hiss()), out _);
            var sg = new GameObject("Audio shots");
            sg.transform.SetParent(host.transform, false);
            shots = sg.AddComponent<AudioSource>();
            shots.playOnAwake = false;
            shots.spatialBlend = 0;
            clunk = Clip("clunk", SoundSynth.Clunk());
            clank = Clip("clank", SoundSynth.Clank());
            whoosh = Clip("whoosh", SoundSynth.Whoosh());
            ignition = ga?.Clip("ignition");
            thud = ga?.Clip("thud");
            clink = ga?.Clip("clink");
            metalClank = ga?.Clip("clank");
            puff = ga?.Clip("rcs_puff");
            booms = new[] { ga?.Clip("explosion1"), ga?.Clip("explosion2"), Clip("boom", SoundSynth.Boom()) };
        }

        static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, SoundSynth.Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioSource Loop(GameObject host, string name, float[] data, out AudioLowPassFilter lp) => Loop(host, name, Clip(name, data), out lp);

        static AudioSource Loop(GameObject host, string name, AudioClip clip, out AudioLowPassFilter lp)
        {
            var go = new GameObject("Audio " + name);
            go.transform.SetParent(host.transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0;
            src.volume = 0;
            if (clip != null) src.Play();
            lp = go.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = 22000;
            return src;
        }

        static void Ease(AudioSource s, float target, float dt, float rate = 3f) => s.volume = Mathf.MoveTowards(s.volume, target, dt * rate);

        public void Update(VesselSim v, bool paused, bool map, bool warping, float dt)
        {
            AudioListener.volume = Volume;
            AudioListener.pause = paused;
            if (v != tracked) { tracked = v; chuteOpen.Clear(); wasBurning = false; }
            if (v == null || v.destroyed) { Silence(dt); return; }
            double pressure = v.body.PressureAtm(Math.Max(0, v.Altitude));
            float air = Mathf.Clamp01((float)Math.Sqrt(pressure));
            float mix = map ? 0.45f : 1f;

            double rocket = 0, jetSpool = 0, solid = 0;
            foreach (var p in v.parts)
            {
                if (p.def.IsJet) jetSpool = Math.Max(jetSpool, p.ignited ? p.spool : 0);
                else if (p.def.IsEngine || p.def.kind == PartKind.EscapeTower)
                {
                    double t = (p.def.IsEngine ? p.def.thrustVac : p.def.lesThrust) * p.thrustFrac;
                    rocket += t;
                    if (p.def.kind == PartKind.Solid) solid += t;
                }
                if (p.def.kind == PartKind.Parachute)
                {
                    bool open = p.chuteDeployed && !p.chuteFailed && p.chuteOpen > 0.02f;
                    chuteOpen.TryGetValue(p.index, out bool was);
                    if (open && !was && air > 0.05f) shots.PlayOneShot(whoosh, 0.7f * mix);
                    chuteOpen[p.index] = open;
                }
            }
            bool burning = rocket > 0 && !warping;
            float roar = burning ? Mathf.Clamp01(0.35f + (float)(rocket / 900)) : 0f;
            // Lighting up in air: the big roar of ignition (not every throttle blip)
            if (burning && !wasBurning && air > 0.25f && Time.unscaledTime - lastIgnition > 4f && ignition != null)
            {
                shots.PlayOneShot(ignition, Mathf.Clamp01(0.45f + (float)(rocket / 1500)) * mix);
                lastIgnition = Time.unscaledTime;
            }
            wasBurning = burning;

            // Air: the recorded low rumble and the synthesized crackle; vacuum: the hull-borne engine hum
            float solidShare = rocket > 0 ? (float)(solid / rocket) : 0f;
            Ease(roarLow, roar * Mathf.Lerp(0.12f, 1f, air) * mix, dt, 4f);
            roarLow.pitch = 0.85f + 0.25f * (float)v.throttle - 0.08f * solidShare;
            roarLp.cutoffFrequency = Mathf.Lerp(900f, 22000f, air);
            Ease(engine, roar * Mathf.Lerp(0.15f, 0.75f, air) * (1f + 0.4f * solidShare) * mix, dt, 4f);
            engine.pitch = 0.8f + 0.35f * (float)v.throttle;
            engineLp.cutoffFrequency = Mathf.Lerp(650f, 22000f, air);
            Ease(engineVac, roar * (1f - air) * 0.9f * mix, dt, 3f);
            engineVac.pitch = 0.9f + 0.2f * (float)v.throttle;

            Ease(jet, (float)jetSpool * 0.55f * air * mix, dt);
            jet.pitch = 0.55f + 0.9f * (float)jetSpool;

            float q = (float)v.dynPressure;
            Ease(wind, warping ? 0 : Mathf.Pow(Mathf.Clamp01(q / 25000f), 0.7f) * 0.8f * mix, dt, 2f);
            wind.pitch = 0.7f + 0.8f * Mathf.Clamp01((float)v.SurfaceRelVelocity.magnitude / 1200f);
            windLp.cutoffFrequency = Mathf.Lerp(1500f, 9000f, Mathf.Clamp01(q / 40000f));
            // Reentry: a deep, rising roar while the shield glows
            float heat = Mathf.Clamp01((float)(v.heatFlux / 5e5));
            Ease(plasma, warping ? 0 : heat * 0.9f * mix, dt, 1.5f);
            plasma.pitch = 0.45f + 0.35f * heat;
            plasmaLp.cutoffFrequency = Mathf.Lerp(600f, 2500f, heat);

            bool rcs = v.rcsFiring.sqrMagnitude > 0.01f && !warping;
            if (rcs && !wasRcs && puff != null) shots.PlayOneShot(puff, 0.35f * mix);
            wasRcs = rcs;
            Ease(hiss, rcs ? 0.3f * mix : 0f, dt, 8f);
        }

        void Silence(float dt)
        {
            Ease(engine, 0, dt, 6f); Ease(roarLow, 0, dt, 6f); Ease(engineVac, 0, dt, 6f); Ease(jet, 0, dt, 6f);
            Ease(wind, 0, dt, 6f); Ease(plasma, 0, dt, 6f); Ease(hiss, 0, dt, 6f);
        }

        public void Stop()
        {
            engine.volume = roarLow.volume = engineVac.volume = jet.volume = wind.volume = plasma.volume = hiss.volume = 0;
            AudioListener.pause = false;
        }

        /// <summary>Staging, touchdown, the hatch: a metal thud with a clink on top (the synth clunk if a sound is missing).</summary>
        public void Clunk(float vol = 1f)
        {
            if (thud == null) { shots.PlayOneShot(clunk, vol); return; }
            shots.PlayOneShot(thud, vol);
            if (clink != null && vol > 0.5f) shots.PlayOneShot(clink, vol * 0.45f);
        }

        /// <summary>Docking ports latching: a deep metal clank.</summary>
        public void Clank(float vol = 1f) => shots.PlayOneShot(metalClank != null ? metalClank : clank, vol * 0.8f);
        public void Whoosh(float vol = 1f) => shots.PlayOneShot(whoosh, vol);

        /// <summary>An explosion this far away (m): loud close by, a distant thud a few km off, nothing beyond.</summary>
        public void Boom(double distance)
        {
            float vol = Mathf.Clamp01(1.15f - (float)distance / 5000f);
            if (vol <= 0.03f) return;
            var c = booms[boomIndex++ % 2] ?? booms[2];
            shots.PlayOneShot(c, vol);
        }
    }
}
