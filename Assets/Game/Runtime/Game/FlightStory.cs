using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 28 in flight: the story's mission control messages (a briefing for each mission, a word when it's done)
    /// and the goal line above the navball.
    /// </summary>
    public partial class FlightMode
    {
        public static bool ForceStory;
        public static bool StoryOn
        {
            get { if (Universe.IsAutotest && !ForceStory) return false; try { return PlayerPrefs.GetInt("story", 1) == 1; } catch { return true; } }
            set { try { PlayerPrefs.SetInt("story", value ? 1 : 0); } catch { } }
        }

        string storyWho, storyText, storyGoal;
        static GUIStyle wrapStyle;
        float storyUntil;
        bool storyIsBrief;

        /// <summary>Once a second (with the other career checks): finished missions and new briefings.</summary>
        void UpdateStory()
        {
            if (!StoryOn) return;
            var c = U.career;
            Story.Update(U);
            if (Time.unscaledTime < storyUntil) return;
            if (c.storyNews.Count > 0)
            {
                var (who, text) = c.storyNews[0];
                c.storyNews.RemoveAt(0);
                ShowStory(who, text, null, false);
                audio?.Clunk(0.12f);
                return;
            }
            var m = Story.Current(c);
            if (m != null && !c.storyBriefed && countdown < 0f && rollout == null)
            {
                c.storyBriefed = true;
                ShowStory(m.who, m.brief, m.goal, true);
            }
        }

        /// <summary>Mission control's message is on screen (the landing guides move down out of its way).</summary>
        public bool StoryShowing => StoryOn && storyText != null && Time.unscaledTime <= storyUntil && countdown < 0f && !mapView;
        float GuideY => StoryShowing ? 312 : 216;

        public void ShowStory(string who, string text, string goal, bool brief)
        {
            storyWho = who; storyText = text; storyGoal = goal; storyIsBrief = brief;
            storyUntil = Time.unscaledTime + (brief ? 14f : 9f);
        }

        void DrawStory(float W, float H)
        {
            if (!StoryOn) return;
            var c = U.career;
            // the goal line above the navball
            var m = Story.Current(c);
            if (m != null && !mapView)
            {
                var gr = new Rect(W * 0.5f - 210, H - 320, 420, 24);
                GUI.Box(gr, GUIContent.none, Ui.Panel);
                GUI.Label(new Rect(gr.x + 10, gr.y + 2, gr.width - 20, 20), $"<color=#ffd56a>Story</color>  {Story.Chapters[m.chapter]}: <b>{m.goal}</b>", new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter });
            }
            // mission control
            if (storyText == null || Time.unscaledTime > storyUntil || countdown >= 0f) return;
            float w = 600, h = storyGoal != null ? 128 : 104;
            var r = new Rect(W * 0.5f - w * 0.5f, 168, w, h);
            Ui.Box(r);
            // a little badge with their initial, in their colour
            Color col = storyWho == Story.Rosa ? new Color(0.95f, 0.55f, 0.25f) : storyWho == Story.Bram ? new Color(0.35f, 0.6f, 0.95f) : new Color(0.55f, 0.85f, 0.45f);
            var badge = new Rect(r.x + 14, r.y + 14, 44, 44);
            Ui.Line(badge, col);
            GUI.Label(badge, $"<b>{storyWho.Substring(storyWho.StartsWith("Dr") ? 3 : 0, 1)}</b>", Ui.BigCenter);
            GUI.Label(new Rect(r.x + 70, r.y + 8, w - 150, 22), $"<color=#9fc6e8>Mission control</color>  ·  {storyWho}", Ui.Small);
            GUI.Label(new Rect(r.x + 70, r.y + 28, w - 84, 60), Words.Say(storyText), wrapStyle ??= new GUIStyle(Ui.Label) { wordWrap = true, clipping = TextClipping.Clip });
            if (storyGoal != null) GUI.Label(new Rect(r.x + 70, r.y + h - 30, w - 160, 22), $"<color=#ffd56a>Next:</color> <b>{storyGoal}</b>", Ui.Small);
            if (Ui.Press(new Rect(r.xMax - 70, r.y + h - 34, 56, 26), "OK", Ui.ButtonSmall)) storyUntil = 0f;
        }
    }
}
