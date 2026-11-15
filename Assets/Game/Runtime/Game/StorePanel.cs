using System;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 38: the supply store at the space centre (a building on the centre screen, and a button in the builder).
    /// Buy food, medicine, canisters, tools and backpack upgrades (into your backpack, or the locker when it's full); sell
    /// what you've found (valuables for their worth, the rest for half price); move things between your backpack and the
    /// locker (12 places, 24 once extended). In a sandbox game everything's free.
    /// </summary>
    public static class StorePanel
    {
        public static bool Open;
        static int tab;
        static Vector2 scroll;
        static string note;
        static float noteUntil;

        static void Say(string s) { note = s; noteUntil = Time.unscaledTime + 3f; }

        public static void Draw(Universe u)
        {
            if (!Open || u == null) return;
            var c = u.career;
            float W = Ui.W, H = Ui.H;
            Ui.Dim(new Rect(0, 0, W, H), 0.55f);
            Ui.Press(new Rect(0, 0, W, H), GUIContent.none, GUIStyle.none);   // (nothing behind takes clicks)
            var r = new Rect(W / 2 - 380, H / 2 - 310, 760, 620);
            Ui.Box(r); Ui.Box(r);
            Ui.Text(new Rect(r.x + 18, r.y + 14, 400, 28), "Supply store", Ui.Title);
            bool free = !c.IsCareer;
            Ui.Text(new Rect(r.x + 260, r.y + 20, 300, 22), free ? "<color=#8a929c>Sandbox: everything's free</color>" : $"Funds <b>{c.funds:#,0}</b>", Ui.Small);
            if (Ui.Press(new Rect(r.xMax - 44, r.y + 14, 30, 28), "×", Ui.ButtonSmall) || In.EscapeDown) { Open = false; u.Save(); return; }
            string[] tabs = { "Buy", "Sell", "Backpack and locker" };
            for (int i = 0; i < tabs.Length; i++)
                if (Ui.Press(new Rect(r.x + 18 + i * 186, r.y + 54, 178, 30), tabs[i], tab == i ? Ui.ButtonSmallSel : Ui.ButtonSmall)) { tab = i; scroll = Vector2.zero; }
            var body = new Rect(r.x + 14, r.y + 96, r.width - 28, r.height - 140);
            if (tab == 0) DrawBuy(u, body, free);
            else if (tab == 1) DrawSell(u, body, free);
            else DrawLocker(u, body, free);
            string foot = $"Backpack {c.backpack.Count}/{c.backpackSize}   ·   Locker {c.locker.Count}/{c.lockerSize}";
            if (Time.unscaledTime < noteUntil) foot = note;
            Ui.Text(new Rect(r.x + 18, r.yMax - 34, r.width - 36, 22), foot, Ui.Small);
        }

        static void DrawBuy(Universe u, Rect body, bool free)
        {
            var c = u.career;
            var view = new Rect(0, 0, body.width - 20, Items.All.Length * 40 + 30);
            scroll = GUI.BeginScrollView(body, scroll, view);
            float y = 0;
            ItemKind? last = null;
            foreach (var d in Items.All.OrderBy(x => x.kind))
            {
                if (last != d.kind) { Ui.Text(new Rect(4, y + 2, 300, 22), $"<color=#f0c860><b>{KindName(d.kind)}</b></color>", Ui.Small); y += 26; last = d.kind; }
                Ui.Text(new Rect(8, y + 6, 230, 22), d.name, Ui.Small);
                Ui.Text(new Rect(240, y + 6, 290, 22), $"<color=#8a929c>{d.about}</color>", new GUIStyle(Ui.Small) { wordWrap = false, clipping = TextClipping.Clip });
                string price = free ? "free" : $"{d.price:#,0}";
                if (Ui.Press(new Rect(view.width - 130, y + 3, 124, 28), $"Buy  {price}", Ui.ButtonGoSmall)) Buy(u, d, free);
                y += 36;
            }
            GUI.EndScrollView();
        }

        static void Buy(Universe u, ItemDef d, bool free)
        {
            var c = u.career;
            if (!free && c.funds < d.price) { Say($"Not enough funds for the {d.name.ToLowerInvariant()} ({d.price:#,0})."); return; }
            if (d.kind == ItemKind.Upgrade)
            {
                if (!c.AddToBackpack(d.id, out string m)) { Say(m); return; }
                if (!free) c.funds -= d.price;
                Say(m);
                return;
            }
            if (!c.BackpackFull) c.backpack.Add(d.id);
            else if (!c.LockerFull) c.locker.Add(d.id);
            else { Say("Your backpack and the locker are both full."); return; }
            if (!free) c.funds -= d.price;
            Say($"Bought: {d.name}" + (c.locker.LastOrDefault() == d.id && c.BackpackFull ? " (in the locker: your backpack's full)" : "") + ".");
        }

        static void DrawSell(Universe u, Rect body, bool free)
        {
            var c = u.career;
            float y = body.y;
            Ui.Text(new Rect(body.x + 4, y, 400, 22), "<b>In your backpack</b>", Ui.Small); y += 26;
            for (int i = 0; i < c.backpack.Count; i++) { if (SellRow(c, c.backpack, i, new Rect(body.x, y, body.width, 30), free)) return; y += 34; }
            if (c.backpack.Count == 0) { Ui.Text(new Rect(body.x + 8, y, 400, 22), "<color=#5a626c>Empty</color>", Ui.Small); y += 26; }
            y += 10;
            Ui.Text(new Rect(body.x + 4, y, 400, 22), "<b>In the locker</b>", Ui.Small); y += 26;
            for (int i = 0; i < c.locker.Count && y < body.yMax - 30; i++) { if (SellRow(c, c.locker, i, new Rect(body.x, y, body.width, 30), free)) return; y += 34; }
            if (c.locker.Count == 0) Ui.Text(new Rect(body.x + 8, y, 400, 22), "<color=#5a626c>Empty</color>", Ui.Small);
        }

        static bool SellRow(Career c, System.Collections.Generic.List<string> list, int i, Rect row, bool free)
        {
            var d = Items.Get(list[i]);
            Ui.Text(new Rect(row.x + 8, row.y + 5, 300, 22), d?.name ?? list[i], Ui.Small);
            if (d != null && d.kind == ItemKind.Valuable) Ui.Text(new Rect(row.x + 300, row.y + 5, 200, 22), "<color=#f0c860>valuable</color>", Ui.Small);
            int worth = d?.Sell ?? 0;
            if (Ui.Press(new Rect(row.xMax - 140, row.y + 1, 134, 28), free ? "Give away" : $"Sell  {worth:#,0}", Ui.ButtonSmall))
            {
                list.RemoveAt(i);
                if (!free) c.funds += worth;
                Say(free ? $"Gave away the {d?.name.ToLowerInvariant()}." : $"Sold the {d?.name.ToLowerInvariant()} for {worth:#,0}.");
                return true;
            }
            return false;
        }

        static void DrawLocker(Universe u, Rect body, bool free)
        {
            var c = u.career;
            float colW = body.width / 2 - 8;
            var left = new Rect(body.x, body.y, colW, body.height);
            var right = new Rect(body.x + colW + 16, body.y, colW, body.height);
            Ui.Text(new Rect(left.x + 4, left.y, colW, 22), $"<b>Backpack</b>  <color=#8a929c>{c.backpack.Count}/{c.backpackSize}, goes with you</color>", Ui.Small);
            Ui.Text(new Rect(right.x + 4, right.y, colW, 22), $"<b>Locker</b>  <color=#8a929c>{c.locker.Count}/{c.lockerSize}, stays here</color>", Ui.Small);
            float y = left.y + 28;
            for (int i = 0; i < c.backpack.Count; i++)
            {
                Ui.Text(new Rect(left.x + 8, y + 4, colW - 120, 22), Items.Get(c.backpack[i])?.name, Ui.Small);
                if (Ui.Press(new Rect(left.xMax - 110, y, 106, 26), "To locker →", Ui.ButtonSmall))
                {
                    if (c.LockerFull) Say("The locker's full."); else { c.locker.Add(c.backpack[i]); c.backpack.RemoveAt(i); }
                    return;
                }
                y += 30;
            }
            y = right.y + 28;
            for (int i = 0; i < c.locker.Count && y < right.yMax - 70; i++)
            {
                Ui.Text(new Rect(right.x + 8, y + 4, colW - 120, 22), Items.Get(c.locker[i])?.name, Ui.Small);
                if (Ui.Press(new Rect(right.xMax - 110, y, 106, 26), "← To pack", Ui.ButtonSmall))
                {
                    if (c.BackpackFull) Say("Your backpack's full."); else { c.backpack.Add(c.locker[i]); c.locker.RemoveAt(i); }
                    return;
                }
                y += 30;
            }
            if (c.lockerSize < Items.LockerBig && Ui.Press(new Rect(right.x, right.yMax - 34, colW, 30), free ? "Extend the locker to 24 (free)" : $"Extend the locker to 24  ({Items.LockerUpgradePrice:#,0})", Ui.ButtonSmall))
            {
                if (!free && c.funds < Items.LockerUpgradePrice) Say("Not enough funds to extend the locker.");
                else { if (!free) c.funds -= Items.LockerUpgradePrice; c.lockerSize = Items.LockerBig; Say("The locker now holds 24 things."); }
            }
        }

        static string KindName(ItemKind k) => k == ItemKind.Food ? "Food" : k == ItemKind.Medical ? "Medicine" : k == ItemKind.Supply ? "Ship and suit supplies" : k == ItemKind.Tool ? "Tools" : k == ItemKind.Valuable ? "Valuables (to sell)" : "Backpack upgrades";
    }
}
