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
        static string mysteryWon;

        /// <summary>Opens on a tab (3: the quest board).</summary>
        public static void OpenAt(int t) { Open = true; tab = t; scroll = Vector2.zero; }
        static Vector2 scroll;
        static string note;
        static float noteUntil;

        static void Say(string s) { note = s; noteUntil = Time.unscaledTime + 3f; }

        public static void Draw(Universe u)
        {
            if (!Open || u == null) return;
            var c = u.career;
            c.RefreshQuests(DateTime.UtcNow);   // milestone 40
            float W = Ui.W, H = Ui.H;
            Ui.Dim(new Rect(0, 0, W, H), 0.55f);
            var r = new Rect(W / 2 - 380, H / 2 - 310, 760, 620);
            Ui.ShieldAround(r, W, H);   // (nothing round it takes clicks; milestone 41: its own buttons do)
            Ui.Box(r); Ui.Box(r);
            Ui.Text(new Rect(r.x + 18, r.y + 14, 400, 28), "Supply store", Ui.Title);
            bool free = !c.IsCareer;
            Ui.Text(new Rect(r.x + 260, r.y + 20, 300, 22), free ? "<color=#8a929c>Sandbox: everything's free</color>" : $"Funds <b>{c.funds:#,0}</b>", Ui.Small);
            if (Ui.Press(new Rect(r.xMax - 44, r.y + 14, 30, 28), "×", Ui.ButtonSmall) || In.EscapeDown) { Open = false; u.Save(); return; }
            string[] tabs = { "Buy", "Sell", "Backpack & locker", "Quest board", "Mystery machine" };
            for (int i = 0; i < tabs.Length; i++)
                if (Ui.Press(new Rect(r.x + 18 + i * 145, r.y + 54, 139, 30), tabs[i], tab == i ? Ui.ButtonSmallSel : Ui.ButtonSmall)) { tab = i; scroll = Vector2.zero; }
            var body = new Rect(r.x + 14, r.y + 96, r.width - 28, r.height - 140);
            if (tab == 0) DrawBuy(u, body, free);
            else if (tab == 1) DrawSell(u, body, free);
            else if (tab == 2) DrawLocker(u, body, free);
            else if (tab == 3) DrawQuests(u, body);
            else DrawMystery(u, body, free);
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
                c.NoteQuest(QuestKind.Sell, worth);   // milestone 40
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

        // ---- milestone 40: the quest board and the mystery machine

        static void DrawQuests(Universe u, Rect body)
        {
            var c = u.career;
            var now = DateTime.UtcNow;
            var nextDay = now.Date.AddDays(1) - now;
            Ui.Text(new Rect(body.x + 4, body.y, body.width, 22), $"<b>Today's quests</b>  <color=#8a929c>new ones in {(int)nextDay.TotalHours} h {nextDay.Minutes} min</color>", Ui.Small);
            float y = body.y + 26;
            foreach (var q in c.quests.Where(x => !x.weekly)) { QuestRow(c, q, new Rect(body.x, y, body.width, 48)); y += 54; }
            y += 8;
            Ui.Text(new Rect(body.x + 4, y, body.width, 22), "<b>This week's quests</b>  <color=#8a929c>bigger, and worth twice as much</color>", Ui.Small);
            y += 26;
            foreach (var q in c.quests.Where(x => x.weekly)) { QuestRow(c, q, new Rect(body.x, y, body.width, 48)); y += 54; }
        }

        static void QuestRow(Career c, Quest q, Rect row)
        {
            Ui.Box(row);
            var item = Items.Get(q.item);
            Ui.Text(new Rect(row.x + 10, row.y + 4, row.width - 200, 22), (q.claimed ? "<color=#7dff8a>✓ </color>" : "") + q.text, Ui.Small);
            Ui.Bar(new Rect(row.x + 10, row.y + 30, row.width - 330, 9), q.need > 0 ? (float)q.have / q.need : 0, q.Done ? new Color(0.45f, 0.9f, 0.5f) : new Color(0.4f, 0.75f, 1f));
            Ui.Text(new Rect(row.xMax - 316, row.y + 24, 120, 20), $"{q.have}/{q.need}", Ui.Small);
            Ui.Text(new Rect(row.xMax - 190, row.y + 4, 180, 20), $"<color=#f0c860>{(c.IsCareer ? $"+{q.reward:#,0}" : "")}{(item != null ? (c.IsCareer ? " + " : "") + item.name : "")}</color>", new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperRight, wordWrap = false, clipping = TextClipping.Clip });
            if (q.Done && !q.claimed && Ui.Press(new Rect(row.xMax - 120, row.y + 22, 110, 24), "Claim", Ui.ButtonGoSmall)) Say(c.ClaimQuest(q));
            else if (q.claimed) Ui.Text(new Rect(row.xMax - 120, row.y + 24, 110, 20), "<color=#7dff8a>claimed</color>", new GUIStyle(Ui.Small) { alignment = TextAnchor.UpperRight });
        }

        static void DrawMystery(Universe u, Rect body, bool free)
        {
            var c = u.career;
            Ui.Text(new Rect(body.x + 4, body.y, body.width, 44), "A machine that sells mystery capsules. Rare things turn up far more often than out in the wild: something very rare one time in twenty.", new GUIStyle(Ui.Small) { wordWrap = true });
            var mc = new Rect(body.center.x - 120, body.y + 70, 240, 200);
            Ui.Box(mc);
            Ui.Text(new Rect(mc.x, mc.y + 20, mc.width, 30), "?", new GUIStyle(Ui.Title) { alignment = TextAnchor.MiddleCenter, fontSize = 48 });
            Ui.Text(new Rect(mc.x, mc.y + 80, mc.width, 50), mysteryWon != null ? $"You got:\n<b>{Items.Get(mysteryWon)?.name}</b>" : "Pull the lever!", new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter, wordWrap = true });
            if (Ui.Press(new Rect(mc.x + 20, mc.yMax - 50, mc.width - 40, 34), free ? "Pull the lever (free)" : $"Pull the lever  ({Items.MysteryPrice})", Ui.ButtonGo))
            {
                if (!free && c.funds < Items.MysteryPrice) { Say("Not enough funds."); return; }
                if (c.BackpackFull && c.LockerFull) { Say("Your backpack and the locker are both full."); return; }
                string id = Items.Mystery(Environment.TickCount);
                var d = Items.Get(id);
                if (!free) c.funds -= Items.MysteryPrice;
                if (d.kind == ItemKind.Upgrade) { if (!c.AddToBackpack(id, out string m)) { if (!c.LockerFull) c.locker.Add(id); } }
                else if (!c.BackpackFull) c.backpack.Add(id); else c.locker.Add(id);
                mysteryWon = id;
                Say($"The machine whirs, clunks, and drops a {d.name.ToLowerInvariant()}" + (d.rarity >= 2 ? "! A rare one!" : "."));
                GameAudio.I?.Confirm();
            }
        }

        static string KindName(ItemKind k) => k == ItemKind.Food ? "Food" : k == ItemKind.Medical ? "Medicine" : k == ItemKind.Supply ? "Ship and suit supplies" : k == ItemKind.Tool ? "Tools" : k == ItemKind.Valuable ? "Valuables (to sell)" : "Backpack upgrades";
    }
}
