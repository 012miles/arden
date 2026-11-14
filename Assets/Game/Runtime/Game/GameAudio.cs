using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>What the music is doing right now.</summary>
    public enum Mood { Silent, Title, Builder, Launch, Space }

    /// <summary>Background sound for a place (under the music).</summary>
    public enum Ambience { None, Hangar, SiteDay, SiteNight }

    /// <summary>
    /// Milestone 21: the soundtrack and the places. A grand theme on the title screen; calm tracks in the assembly
    /// building; an orchestral piece from liftoff until you reach space; quiet ambient pieces in space with long
    /// silences between them (so each one is an event). Under the music: the hum of the assembly building, a breeze
    /// at the space centre by day and crickets by night. Also the interface clicks.
    /// Music and effects have their own volumes (the master volume is applied by the listener).
    /// </summary>
    public class GameAudio
    {
        public static GameAudio I { get; private set; }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat("musicVol", 0.55f);
            set { PlayerPrefs.SetFloat("musicVol", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        static readonly Dictionary<Mood, string[]> Playlists = new Dictionary<Mood, string[]>
        {
            { Mood.Title, new[] { "title_space" } },
            { Mood.Builder, new[] { "builder_august", "builder_exploration", "builder_home" } },
            { Mood.Launch, new[] { "launch_arclight" } },
            { Mood.Space, new[] { "space_outthere", "space_drifting", "space_firstlight", "space_temple", "space_beyond", "space_atmosphere" } },
        };

        readonly AudioSource[] music = new AudioSource[2];
        readonly AudioSource ambience, ui;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly System.Random rng = new System.Random();
        int current;                  // which of the two music sources is playing
        Mood mood = Mood.Silent;
        float silenceUntil;           // seconds (unscaled) before the next track starts
        int lastIndex = -1;
        Ambience amb = Ambience.None;
        float duck = 1f;
        public string Playing { get; private set; } = "";
        public Mood CurrentMood => mood;

        public GameAudio(GameObject host)
        {
            I = this;
            for (int k = 0; k < 2; k++)
            {
                var go = new GameObject("Music " + k);
                go.transform.SetParent(host.transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0; s.volume = 0; s.ignoreListenerPause = true; s.priority = 0;
                music[k] = s;
            }
            var ag = new GameObject("Ambience");
            ag.transform.SetParent(host.transform, false);
            ambience = ag.AddComponent<AudioSource>();
            ambience.playOnAwake = false; ambience.loop = true; ambience.spatialBlend = 0; ambience.volume = 0;
            var ug = new GameObject("UI sounds");
            ug.transform.SetParent(host.transform, false);
            ui = ug.AddComponent<AudioSource>();
            ui.playOnAwake = false; ui.spatialBlend = 0; ui.ignoreListenerPause = true;
        }

        public AudioClip Clip(string name, bool musicClip = false)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>((musicClip ? "Audio/Music/" : "Audio/Sfx/") + name);
            if (c == null) Debug.LogWarning("Missing sound " + name);
            clips[name] = c;
            return c;
        }

        /// <summary>Set every frame by the current screen: the music's mood, the place's ambience and a duck (0..1).</summary>
        public void Set(Mood m, Ambience a, float duckTo = 1f)
        {
            if (m != mood)
            {
                bool wasLaunch = mood == Mood.Launch;
                mood = m;
                lastIndex = -1;
                // Space after a launch starts quietly after a while; everything else starts soon
                silenceUntil = Time.unscaledTime + (m == Mood.Space ? (wasLaunch ? 25f : 8f) : m == Mood.Silent ? 0f : 1.5f);
            }
            duck = duckTo;
            if (a != amb)
            {
                amb = a;
                string name = a == Ambience.Hangar ? "hangar_amb" : a == Ambience.SiteDay ? "breeze" : a == Ambience.SiteNight ? "crickets" : null;
                if (name != null) { ambience.clip = Clip(name); ambience.Play(); }
            }
        }

        public void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float target = MusicVolume * duck * (mood == Mood.Builder ? 0.75f : mood == Mood.Space ? 0.85f : 1f);
            var cur = music[current];
            bool wanted = mood != Mood.Silent && cur.clip != null && Array.IndexOf(Playlists.TryGetValue(mood, out var list) ? list : Array.Empty<string>(), cur.clip.name) >= 0;
            // Fade the playing track in (or out when the mood changed), the other one out
            cur.volume = Mathf.MoveTowards(cur.volume, wanted && cur.isPlaying ? target : 0f, dt * (wanted ? 0.25f : 0.5f));
            var other = music[1 - current];
            other.volume = Mathf.MoveTowards(other.volume, 0f, dt * 0.5f);
            if (other.volume <= 0f && other.isPlaying) other.Stop();
            if (!wanted && cur.volume <= 0f && cur.isPlaying) cur.Stop();

            // A finished (or unwanted) track: wait out the silence, then the next
            if (mood != Mood.Silent && (!cur.isPlaying || !wanted))
            {
                if (cur.isPlaying && !wanted) { current = 1 - current; return; }   // crossfade: let it fade on the other source
                if (silenceUntil == float.MaxValue) silenceUntil = Time.unscaledTime + Gap();   // it ended between frames
                if (Time.unscaledTime >= silenceUntil)
                {
                    if (mood == Mood.Launch && lastIndex >= 0) return;            // the launch piece plays once
                    StartNext();
                }
            }
            else if (mood != Mood.Silent && cur.isPlaying && cur.clip != null && !cur.loop && cur.time > cur.clip.length - 0.25f)
            {
                silenceUntil = Time.unscaledTime + Gap();
            }

            // Ambience: quieter under music
            float ambTarget = amb == Ambience.None ? 0f : (amb == Ambience.Hangar ? 0.35f : amb == Ambience.SiteNight ? 0.45f : 0.3f) * (0.6f + 0.4f * (1f - MusicVolume * 0.5f));
            ambience.volume = Mathf.MoveTowards(ambience.volume, ambTarget, dt * 0.4f);
            if (ambience.volume <= 0f && ambience.isPlaying && amb == Ambience.None) ambience.Stop();
        }

        float Gap() => mood == Mood.Space ? 30f + (float)rng.NextDouble() * 50f : mood == Mood.Builder ? 4f : 1f;

        void StartNext()
        {
            var list = Playlists[mood];
            int i = list.Length == 1 ? 0 : rng.Next(list.Length);
            if (i == lastIndex && list.Length > 1) i = (i + 1) % list.Length;
            lastIndex = i;
            var clip = Clip(list[i], true);
            if (clip == null) { silenceUntil = Time.unscaledTime + 30f; return; }
            var s = music[current];
            if (s.isPlaying) { current = 1 - current; s = music[current]; }
            s.clip = clip;
            s.loop = mood == Mood.Title;
            s.volume = 0f;
            s.Play();
            Playing = clip.name;
            silenceUntil = float.MaxValue;
        }

        // ---------------------------------------------------------------- interface sounds

        public void Click() => PlayUi("ui_click", 0.5f);
        public void Drop() => PlayUi("ui_drop", 0.6f);
        public void Confirm() => PlayUi("ui_confirm", 0.6f);
        public void Error() => PlayUi("ui_error", 0.5f);

        void PlayUi(string name, float vol)
        {
            var c = Clip(name);
            if (c != null) ui.PlayOneShot(c, vol);
        }
    }
}
