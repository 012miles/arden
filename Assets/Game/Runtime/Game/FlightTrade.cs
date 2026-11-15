using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Rocket
{
    /// <summary>
    /// Milestone 40: traders. At each of the big outposts an Ardling trader has set up shop (their stock changes every
    /// day). Walk up (within 8 m) and press F to trade: buy from their stock (a little dearer than the store at home), or
    /// sell from your backpack (valuables fetch more out here). Free in a sandbox game.
    /// </summary>
    public partial class FlightMode
    {
        public bool tradeOpen;
        string tradePlace;
        readonly Dictionary<string, GameObject> traderViews = new Dictionary<string, GameObject>();
        static readonly string[] TraderNames = { "Pip", "Moss", "Juniper", "Basil", "Clover", "Tansy", "Rook", "Wren" };

        static string TraderName(string placeId) => TraderNames[Math.Abs(Landmarks.StableHash(placeId ?? "x")) % TraderNames.Length] + " Ardling";

        /// <summary>Today's stock at a trader: six things, the same all day.</summary>
        public static List<string> TraderStock(string placeId, int day)
        {
            var rng = new System.Random(day * 7919 ^ Landmarks.StableHash(placeId ?? "x"));
            return Items.All.Where(d => d.kind != ItemKind.Valuable).OrderBy(_ => rng.Next()).Take(6).Select(d => d.id).ToList();
        }

        public static int TraderPrice(ItemDef d) => (int)Math.Round(d.price * 1.25 / 5) * 5;
        public static int TraderBuys(ItemDef d) => d.kind == ItemKind.Valuable ? (int)Math.Round(d.price * 1.15 / 5) * 5 : Math.Max(5, (int)Math.Round(d.price * 0.6 / 5) * 5);

        /// <summary>The big outpost (not a random place) whose trader is within reach of this walker, or null.</summary>
        Landmark TraderNear(VesselSim v)
        {
            if (v == null || !v.IsEva || !v.landed) return null;
            Vector3d here = FixedPos(v).normalized;
            foreach (var l in Outposts.On(v.body))
            {
                if (l.id.StartsWith("site.")) continue;
                double m = Vector3d.Angle(here, l.dir) * v.body.radius;
                if (m < 12) return l;
            }
            return null;
        }

        /// <summary>The traders stand at the big outposts (drawn when you're near).</summary>
        void UpdateTraderViews()
        {
            if (active == null) return;
            var b = active.body;
            Vector3d here = FixedPos(active);
            var want = new HashSet<string>();
            foreach (var l in Outposts.On(b))
            {
                if (l.id.StartsWith("site.")) continue;
                Vector3d at = l.dir * (b.radius + LaunchSite.SurfaceHeight(b, l.dir));
                if ((at - here).magnitude > 400) continue;
                want.Add(l.id);
                if (!traderViews.TryGetValue(l.id, out var go) || go == null)
                {
                    go = MyArdling.Build(Ardlings.LookFor(TraderName(l.id)), 0);
                    go.name = "Trader " + TraderName(l.id);
                    traderViews[l.id] = go;
                }
                Vector3 up = b.FixedToInertial(l.dir, renderUt).ToFloat();
                go.transform.position = RenderPoint(b, at);
                Vector3 look = Vector3.ProjectOnPlane(RenderPoint(b, here) - go.transform.position, up);
                if (look.sqrMagnitude > 1e-4f) go.transform.rotation = Quaternion.LookRotation(look, up);
            }
            foreach (var k in traderViews.Keys.ToList()) if (!want.Contains(k)) { if (traderViews[k] != null) UnityEngine.Object.Destroy(traderViews[k]); traderViews.Remove(k); }
        }

        void ClearTraders()
        {
            foreach (var go in traderViews.Values) if (go != null) UnityEngine.Object.Destroy(go);
            traderViews.Clear();
            tradeOpen = false;
        }

        /// <summary>Buying and selling for a player (here, or on the host for a player in a room).</summary>
        void DoTrade(string who, string op, string arg)
        {
            var eva = EvaOf(who);
            var l = TraderNear(eva);
            if (l == null) return;
            var c = U.career;
            bool free = !c.IsCareer;
            var pack = PackOf(who, out int size);
            if (op == "buy")
            {
                var d = Items.Get(arg);
                if (d == null || !TraderStock(l.id, Quests.DayNumber(DateTime.UtcNow)).Contains(arg)) return;
                int price = TraderPrice(d);
                if (!free && c.funds < price) { TellPlayer(who, $"Not enough funds ({price:#,0}).", 2f); return; }
                if (!PackAdd(who, d.id, out string msg)) { TellPlayer(who, msg, 2f); return; }
                if (!free) c.funds -= price;
                TellPlayer(who, $"Bought: {d.name}" + (free ? "." : $" for {price:#,0}."), 2.5f);
            }
            else if (op == "sell")
            {
                int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n);
                if (n < 0 || n >= pack.Count) return;
                var d = Items.Get(pack[n]);
                if (d == null) return;
                int worth = TraderBuys(d);
                pack.RemoveAt(n);
                if (!free) c.funds += worth;
                Quest(QuestKind.Sell, worth);
                TellPlayer(who, free ? $"Traded away the {d.name.ToLowerInvariant()}." : $"Sold the {d.name.ToLowerInvariant()} for {worth:#,0}.", 2.5f);
            }
            if (Coop.Active) Coop.Current.room.Touch();
        }

        void DrawTrade(float W, float H)
        {
            if (!tradeOpen || active == null) return;
            var l = TraderNear(active);
            if (l == null) { tradeOpen = false; return; }
            string me = MyArdlingName;
            var c = U.career;
            bool free = !c.IsCareer;
            var stock = TraderStock(l.id, Quests.DayNumber(DateTime.UtcNow));
            var pack = Coop.Active && remote ? Coop.Current.room.State.packs.Find(p => p.name == me)?.items ?? new List<string>() : PackOf(me, out _);
            float h = 110 + Math.Max(stock.Count, Math.Max(1, pack.Count)) * 34;
            var r = new Rect(W / 2 - 380, H / 2 - h / 2, 760, h);
            Ui.Box(r); Ui.Box(r);
            Ui.Text(new Rect(r.x + 16, r.y + 10, 500, 26), $"<b>{TraderName(l.id)}</b>  <color=#8a929c>trading at {l.name}</color>", Ui.Bold);
            Ui.Text(new Rect(r.x + 420, r.y + 14, 220, 22), free ? "<color=#8a929c>Sandbox: free</color>" : $"Funds <b>{c.funds:#,0}</b>", Ui.Small);
            if (Ui.Press(new Rect(r.xMax - 92, r.y + 10, 80, 26), "Close (F)", Ui.ButtonSmall)) tradeOpen = false;
            float colW = r.width / 2 - 24;
            Ui.Text(new Rect(r.x + 16, r.y + 44, colW, 22), "<color=#f0c860><b>Today's stock</b></color>", Ui.Small);
            Ui.Text(new Rect(r.x + colW + 40, r.y + 44, colW, 22), "<color=#f0c860><b>Sell from your backpack</b></color>", Ui.Small);
            float y = r.y + 70;
            foreach (var id in stock)
            {
                var d = Items.Get(id);
                Ui.Text(new Rect(r.x + 18, y + 4, colW - 130, 22), d.name, Ui.Small);
                if (Ui.Press(new Rect(r.x + colW - 106, y, 120, 26), free ? "Buy (free)" : $"Buy  {TraderPrice(d):#,0}", Ui.ButtonGoSmall)) ItemOp(me, "buy", id);
                y += 34;
            }
            y = r.y + 70;
            for (int i = 0; i < pack.Count; i++)
            {
                var d = Items.Get(pack[i]);
                if (d == null) continue;
                Ui.Text(new Rect(r.x + colW + 42, y + 4, colW - 130, 22), d.name + (d.kind == ItemKind.Valuable ? "  <color=#f0c860>valuable</color>" : ""), Ui.Small);
                if (Ui.Press(new Rect(r.xMax - 136, y, 120, 26), free ? "Trade away" : $"Sell  {TraderBuys(d):#,0}", Ui.ButtonSmall)) ItemOp(me, "sell", i.ToString(CultureInfo.InvariantCulture));
                y += 34;
            }
            if (pack.Count == 0) Ui.Text(new Rect(r.x + colW + 42, y + 4, colW, 22), "<color=#5a626c>Nothing in your backpack</color>", Ui.Small);
            Ui.Text(new Rect(r.x + 16, r.yMax - 28, r.width - 32, 20), "<color=#8a929c>Valuables fetch more out here than at the store back home. The stock changes every day.</color>", Ui.Small);
        }
    }
}
