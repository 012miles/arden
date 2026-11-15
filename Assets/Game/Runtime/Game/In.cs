using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Rocket
{
    public enum K
    {
        W, A, S, D, Q, E, Space, LeftShift, LeftCtrl, Z, X, T, M, P, H, N, I, J, K, L, G, U, B, R, C, F,
        Period, Comma, Slash, Backspace, Escape, Delete, Tab, F1, F5, F9, LeftBracket, RightBracket, V, Y, O,
        Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9, Alpha0,
        F2,
        LeftMeta,   // Cmd on a Mac (the Windows key on a PC): Cmd+Z undoes, like Ctrl+Z
        Enter, LeftAlt,   // milestone 34: get out after landing; free the pointer in first person
        F10,              // milestone 41: the main menu, from anywhere
    }

    /// <summary>Thin input wrapper so game code doesn't care which Unity input backend is active.</summary>
    public static class In
    {
        public static readonly K[] Digits = { K.Alpha1, K.Alpha2, K.Alpha3, K.Alpha4, K.Alpha5, K.Alpha6, K.Alpha7, K.Alpha8, K.Alpha9, K.Alpha0 };

        /// <summary>
        /// Someone's typing in a text box (a name, the chat, the parts search): the game's keys stay quiet so letters don't
        /// stage the rocket or open the map. ESC still works (it leaves the text box).
        /// </summary>
        public static bool Typing { get { try { return GUIUtility.keyboardControl != 0; } catch { return false; } } }
        static bool Quiet(K k)
        {
            if (!Typing) return false;
            // ESC while typing just leaves the text box (it doesn't also pause the game)
            if (k == K.Escape && EscapeRaw) { try { GUIUtility.keyboardControl = 0; } catch { } }
            return true;
        }

        /// <summary>Ctrl on a PC, Cmd on a Mac.</summary>
        public static bool Cmd => Held(K.LeftCtrl) || Held(K.LeftMeta);

#if ENABLE_INPUT_SYSTEM
        static readonly Dictionary<K, Key> map = new Dictionary<K, Key>();

        static Key Map(K k)
        {
            if (map.TryGetValue(k, out var key)) return key;
            string s = Keybinds.Override(k) ?? Default(k);
            if (!Enum.TryParse(s, out key)) key = (Key)Enum.Parse(typeof(Key), Default(k));
            map[k] = key;
            return key;
        }

        static string Default(K k)
        {
            string s = k.ToString();
            return s.StartsWith("Alpha") ? "Digit" + s.Substring(5) : s;
        }

        /// <summary>Forget cached key lookups (after a rebind).</summary>
        public static void Remapped() => map.Clear();

        /// <summary>The key pressed this frame (for rebinding), by its saved name, or null.</summary>
        public static string AnyKeyDown()
        {
            var kb = Keyboard.current;
            if (kb == null) return null;
            foreach (var c in kb.allKeys) if (c != null && c.wasPressedThisFrame && c.keyCode != Key.Escape) return c.keyCode.ToString();
            return null;
        }

        public static bool EscapeDown => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        static bool EscapeRaw => EscapeDown;

        /// <summary>What to call the key a game key is on right now ("SPACE", "SHIFT", "["...).</summary>
        public static string Name(K k) => Pretty(Map(k).ToString());

        public static bool Held(K k) { if (Quiet(k)) return false; var kb = Keyboard.current; return kb != null && kb[Map(k)].isPressed; }
        public static bool Down(K k) { if (Quiet(k)) return false; var kb = Keyboard.current; return kb != null && kb[Map(k)].wasPressedThisFrame; }
        public static bool RightMouse => Mouse.current != null && Mouse.current.rightButton.isPressed;
        public static bool MiddleMouse => Mouse.current != null && Mouse.current.middleButton.isPressed;
        public static bool LeftDown => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool LeftMouse => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public static bool RightDown => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        public static bool RightUp => Mouse.current != null && Mouse.current.rightButton.wasReleasedThisFrame;
        public static Vector2 MouseDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        public static float Scroll => Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        static readonly Dictionary<K, KeyCode> map = new Dictionary<K, KeyCode>();

        static KeyCode Map(K k)
        {
            if (map.TryGetValue(k, out var key)) return key;
            string s = k == K.LeftCtrl ? "LeftControl" : k == K.Enter ? "Return" : k.ToString();
            key = (KeyCode)Enum.Parse(typeof(KeyCode), s);
            map[k] = key;
            return key;
        }

        public static void Remapped() => map.Clear();
        public static string AnyKeyDown() => null;
        public static bool EscapeDown => Input.GetKeyDown(KeyCode.Escape);
        static bool EscapeRaw => EscapeDown;
        public static string Name(K k) => Pretty(Map(k).ToString());

        public static bool Held(K k) => !Quiet(k) && Input.GetKey(Map(k));
        public static bool Down(K k) => !Quiet(k) && Input.GetKeyDown(Map(k));
        public static bool RightMouse => Input.GetMouseButton(1);
        public static bool MiddleMouse => Input.GetMouseButton(2);
        public static bool LeftDown => Input.GetMouseButtonDown(0);
        public static bool LeftMouse => Input.GetMouseButton(0);
        public static bool RightDown => Input.GetMouseButtonDown(1);
        public static bool RightUp => Input.GetMouseButtonUp(1);
        public static Vector2 MouseDelta => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
        public static float Scroll => Input.mouseScrollDelta.y;
        public static Vector2 MousePosition => Input.mousePosition;
#endif

        static string Pretty(string s)
        {
            switch (s)
            {
                case "Space": return "SPACE";
                case "LeftShift": case "RightShift": return "SHIFT";
                case "LeftCtrl": case "RightCtrl": case "LeftControl": case "RightControl": return "CTRL";
                case "LeftAlt": case "RightAlt": return "ALT";
                case "Backspace": return "BKSP";
                case "Period": return ".";
                case "Comma": return ",";
                case "Slash": return "/";
                case "Backslash": return "\\";
                case "LeftBracket": return "[";
                case "RightBracket": return "]";
                case "Semicolon": return ";";
                case "Quote": return "'";
                case "Minus": return "-";
                case "Equals": return "=";
                case "Enter": return "ENTER";
                case "Tab": return "TAB";
                case "UpArrow": return "UP";
                case "DownArrow": return "DOWN";
                case "LeftArrow": return "LEFT";
                case "RightArrow": return "RIGHT";
            }
            if (s.StartsWith("Digit")) return s.Substring(5);
            if (s.StartsWith("Alpha")) return s.Substring(5);
            if (s.StartsWith("Numpad")) return "NUM " + s.Substring(6);
            return s.ToUpperInvariant();
        }
    }

    /// <summary>
    /// Milestone 23: rebindable keys. Each game key (the K the code asks for) can be moved to another physical key;
    /// the choice is kept in PlayerPrefs. The help bars show whatever key is in use.
    /// </summary>
    public static class Keybinds
    {
        public static readonly (K key, string what)[] Actions =
        {
            (K.Space, "Stage / jump on EVA"), (K.LeftShift, "Throttle up / jetpack up"), (K.LeftCtrl, "Throttle down / jetpack down"),
            (K.Z, "Full throttle"), (K.X, "Cut throttle"), (K.W, "Pitch down / walk"), (K.S, "Pitch up / walk back"),
            (K.A, "Yaw left / turn left"), (K.D, "Yaw right / turn right"), (K.Q, "Roll left"), (K.E, "Roll right / use (in the cabin)"),
            (K.T, "SAS"), (K.R, "RCS"), (K.H, "RCS forward"), (K.N, "RCS back"), (K.I, "RCS down"), (K.K, "RCS up"),
            (K.J, "RCS left"), (K.L, "RCS right"), (K.G, "Gear / plant a flag"), (K.U, "Lights"), (K.B, "Brakes / climb in"),
            (K.Y, "Deploy"), (K.Backspace, "Abort"), (K.F, "Thermal view"), (K.P, "Autopilot"), (K.M, "Map"), (K.O, "Map layers"),
            (K.Tab, "Focus next world (map) / go inside the cabin"), (K.V, "Vessel list"), (K.LeftBracket, "Previous vessel"), (K.RightBracket, "Next vessel"),
            (K.Period, "Warp faster"), (K.Comma, "Warp slower"), (K.Slash, "Stop warp"), (K.F5, "Quicksave"), (K.F9, "Quickload"),
            (K.F1, "Hide / show the interface"), (K.C, "Angle snap (builder) / camera view (EVA)"), (K.Enter, "Get out after landing"), (K.LeftAlt, "Free the pointer (first person)"), (K.F10, "Main menu (press twice)"),
        };

        public static string Override(K k)
        {
            string s = PlayerPrefs.GetString("bind_" + k, "");
            return string.IsNullOrEmpty(s) ? null : s;
        }

        public static void Set(K k, string physicalKey)
        {
            PlayerPrefs.SetString("bind_" + k, physicalKey);
            PlayerPrefs.Save();
            In.Remapped();
        }

        public static void ResetAll()
        {
            foreach (var a in Actions) PlayerPrefs.DeleteKey("bind_" + a.key);
            PlayerPrefs.Save();
            In.Remapped();
        }

        /// <summary>Game keys that now share a physical key with another one (they'd both fire).</summary>
        public static List<string> Clashes()
        {
            var seen = new Dictionary<string, K>();
            var bad = new List<string>();
            foreach (var a in Actions)
            {
                string n = In.Name(a.key);
                if (seen.TryGetValue(n, out var other)) bad.Add($"{n}: {Describe(other)} and {Describe(a.key)}");
                else seen[n] = a.key;
            }
            return bad;
        }

        static string Describe(K k) { foreach (var a in Actions) if (a.key == k) return a.what; return k.ToString(); }
    }
}
