using System.Collections.Generic;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 35 (captaincarl24: "the credits on the title screen are kinda bothering me ... maybe once you complete the
    /// game it shows the credits, Minecraft type"): the credits roll. The first time you finish the story, mission control
    /// says a last word and the credits scroll up the screen over the dark: who made it, the star map, textures, fonts,
    /// music and sounds. Hold SPACE to speed it up, ESC to skip. On the title screen there's only a small "Credits" link in
    /// the corner now, which plays it again.
    /// </summary>
    public static class EndCredits
    {
        public static bool Active;
        static float scroll;
        static bool finale;

        /// <summary>Rolls the credits (finale: after finishing the story, with mission control's last word first).</summary>
        public static void Start(bool afterStory = false) { Active = true; scroll = 0; finale = afterStory; }

        enum Kind { Title, Quote, Head, Name, Text, Gap, Big }

        static List<(Kind k, string s)> Lines()
        {
            var l = new List<(Kind, string)> { (Kind.Title, "ARDEN SPACE PROGRAM"), (Kind.Gap, "") };
            if (finale)
            {
                l.Add((Kind.Text, "Rosa Ardling, flight director, one last message:"));
                l.Add((Kind.Quote, "From a wobbly hop off the pad to the moons of Brume, you've flown further than anyone on Arden ever dreamed of. The whole town's still out on the lawn. They'll be looking up for years."));
                l.Add((Kind.Gap, ""));
            }
            l.Add((Kind.Head, "Made by"));
            l.Add((Kind.Name, "captaincarl24"));
            l.Add((Kind.Gap, ""));
            l.Add((Kind.Head, "Star map"));
            l.Add((Kind.Text, "NASA/Goddard Space Flight Center Scientific Visualization Studio, from Gaia DR2 (ESA/Gaia/DPAC)"));
            l.Add((Kind.Head, "Textures"));
            l.Add((Kind.Text, "ambientCG (CC0)"));
            l.Add((Kind.Head, "Fonts"));
            l.Add((Kind.Text, "Barlow and Exo 2 (SIL Open Font License)"));
            l.Add((Kind.Head, "Music"));
            foreach (var m in new[]
            {
                "\"Space\" by lasercheese (CC-BY 3.0)", "\"ArcLight\" by Matthew Pablo (CC-BY 3.0)",
                "\"Beyond the Stars\" and \"Drifting Beyond the Stars\" by Hitctrl (CC-BY 3.0)", "\"Space Atmosphere\" by Alexandr Zhelanov (CC-BY 3.0)",
                "\"Another August\" by cynicmusic (CC0)", "\"Exploration Theme\" by CleytonKauffman (CC0)", "\"At Home\" by Wolfgang_ (CC0)",
                "\"Out There\" and \"Galactic Temple\" by yd (CC0)", "\"First Light Particles\" by Yoiyami (CC0)",
            }) l.Add((Kind.Text, m));
            l.Add((Kind.Head, "Sounds"));
            foreach (var s in new[]
            {
                "Explosions by Michel Baradari and the Blender Foundation (CC-BY 3.0)", "Hangar hum by gryc (CC-BY 3.0)",
                "Rumble by theMinesAreShakin, engine by gmason, wind by aquinn and remaxim (CC0)",
                "Thrusters by EZduzziteh, metal by BMacZero, crickets by Wolfgang_, launch by qubodup (CC0)", "Interface sounds by Kenney (CC0)",
            }) l.Add((Kind.Text, s));
            l.Add((Kind.Text, "Music and sounds found on OpenGameArt.org"));
            l.Add((Kind.Head, "Made with"));
            l.Add((Kind.Text, "Unity"));
            l.Add((Kind.Gap, "")); l.Add((Kind.Gap, "")); l.Add((Kind.Gap, ""));
            l.Add((Kind.Big, "Thank you for playing!"));
            l.Add((Kind.Text, finale ? "The rest of space is yours." : ""));
            return l;
        }

        static GUIStyle title, quote, head, name, text, big;

        public static void Draw()
        {
            if (!Active) return;
            float W = Ui.W, H = Ui.H;
            Ui.Dim(new Rect(0, 0, W, H), 0.94f);
            Ui.Press(new Rect(0, 0, W, H), GUIContent.none, GUIStyle.none);   // eats clicks
            if (title == null)
            {
                title = new GUIStyle(Ui.Hero) { alignment = TextAnchor.MiddleCenter, fontSize = 46 };
                quote = new GUIStyle(Ui.Label) { alignment = TextAnchor.UpperCenter, wordWrap = true, fontStyle = FontStyle.Italic, fontSize = 17 };
                head = new GUIStyle(Ui.Header) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
                name = new GUIStyle(Ui.Title) { alignment = TextAnchor.MiddleCenter, fontSize = 26 };
                text = new GUIStyle(Ui.Label) { alignment = TextAnchor.UpperCenter, wordWrap = true };
                text.normal.textColor = new Color(0.82f, 0.86f, 0.9f);
                big = new GUIStyle(Ui.Title) { alignment = TextAnchor.MiddleCenter, fontSize = 30 };
            }
            if (Event.current.type == EventType.Repaint) scroll += Time.unscaledDeltaTime * 42f * (In.Held(K.Space) ? 5f : 1f);
            float cw = Mathf.Min(760, W - 80), x = W / 2 - cw / 2, y = H + 30 - scroll;
            foreach (var (k, s) in Lines())
            {
                var st = k == Kind.Title ? title : k == Kind.Quote ? quote : k == Kind.Head ? head : k == Kind.Name ? name : k == Kind.Big ? big : text;
                float h = k == Kind.Gap ? 40 : k == Kind.Title ? 70 : k == Kind.Head ? 34 : st.CalcHeight(new GUIContent(s), cw) + (k == Kind.Name ? 18 : 6);
                if (k == Kind.Head) y += 22;
                if (y + h > -20 && y < H + 20 && k != Kind.Gap) Ui.Text(new Rect(x, y, cw, h), s, st);
                y += h;
            }
            var hint = new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter };
            Ui.Text(new Rect(0, H - 30, W, 22), $"Hold {In.Name(K.Space)} to speed up", hint);
            bool done = y < -40;
            if (Ui.Press(new Rect(W - 130, 18, 112, 32), "Skip  (ESC)", Ui.ButtonSmall) || In.EscapeDown || done) Active = false;
        }
    }
}
