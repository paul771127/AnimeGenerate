using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 對話的 2D 美術（立繪、表情、背景）：圖檔放在 Resources/Story2D/（Codex 生成，規格見 STORY_2D_BRIEF.md）。
    ///   立繪：Story2D/chars/{角色id}_{表情}.png（透明背景、大腿以上、臉朝畫面右邊）；沒有這個表情就用 _normal，再沒有就用程式畫的頭像。
    ///   背景：Story2D/bg/{背景id}.png／.jpg；沒有就照舊用半透明暗色蓋在 AR 畫面上。
    /// </summary>
    public static class Story2D
    {
        /// <summary>角色名稱 → 圖檔 id（城鎮居民的「職稱・名字」只看名字）</summary>
        public static readonly Dictionary<string, string> Cast = new Dictionary<string, string>
        {
            ["艾琳"] = "erin", ["老焰"] = "laoyan", ["小鐵"] = "xiaotie", ["米菈"] = "mila",
            ["翠"] = "cui", ["楓"] = "feng", ["小葉"] = "xiaoye",
            ["海斗"] = "haido", ["凪"] = "nagi", ["澪"] = "mio", ["阿潮"] = "achao",
            ["雷歐"] = "leo", ["小沙"] = "xiaosha", ["鐵娘"] = "tieniang",
            ["老駝"] = "laotuo", ["阿鈴"] = "aling", ["石先生"] = "shi",
            ["工頭老石"] = "foreman", ["阿岩"] = "ayan", ["黑衣鑄鐘匠"] = "bellsmith",
            ["公會長"] = "guildmaster", ["塔守"] = "towerkeeper",
            ["熔岩巨像"] = "golem", ["腐化樹王"] = "treeking", ["深海巨妖"] = "kraken",
            ["青銅鬥魂"] = "bronze", ["黃土岩魔"] = "rockfiend", ["霸主"] = "overlord",
            ["空賊船長"] = "aircaptain",
            ["炭姨"] = "alchemist_tanyi", ["青蘿"] = "alchemist_qingluo", ["汐"] = "alchemist_xi",
            ["賽琳"] = "alchemist_sailin", ["駝鈴"] = "alchemist_tuoling", ["老錘"] = "crafter_laochui",
            ["鈴音"] = "lingyin", ["疤面亞克"] = "ark", ["小麥"] = "xiaomai",
            ["老匠的殘影"] = "oldsmith_shade", ["齒輪商人"] = "gearmerchant",
            ["千機匠魂"] = "clockwork", ["萬象之心"] = "heartcore",
            ["熔岩殘影"] = "lavashade", ["樹心殘影"] = "treeshade", ["潮影"] = "tideshade",
        };
        /// <summary>表情（劇本裡寫在名字後面的［］中）：中文或英文都可以</summary>
        public static readonly string[] Exprs = { "normal", "smile", "angry", "sad", "surprised", "serious" };
        static readonly Dictionary<string, string> ExprAlias = new Dictionary<string, string>
        {
            ["普通"] = "normal", ["平常"] = "normal", ["笑"] = "smile", ["微笑"] = "smile", ["開心"] = "smile",
            ["怒"] = "angry", ["生氣"] = "angry", ["哀"] = "sad", ["難過"] = "sad", ["悲傷"] = "sad",
            ["驚"] = "surprised", ["驚訝"] = "surprised", ["認真"] = "serious", ["嚴肅"] = "serious",
        };
        public static string NormExpr(string e)
        {
            if (string.IsNullOrEmpty(e)) return null;
            e = e.Trim();
            if (ExprAlias.TryGetValue(e, out var x)) return x;
            e = e.ToLowerInvariant();
            return System.Array.IndexOf(Exprs, e) >= 0 ? e : null;
        }

        public static string IdOf(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return null;
            int dot = speaker.LastIndexOf('・'); if (dot >= 0) speaker = speaker.Substring(dot + 1);
            return Cast.TryGetValue(speaker.Trim(), out var id) ? id : null;
        }

        /// <summary>沒寫表情時：從台詞猜（……＝難過、！＝驚訝／認真、笑聲＝開心）</summary>
        public static string GuessExpr(string text)
        {
            if (string.IsNullOrEmpty(text)) return "normal";
            if (text.Contains("哈") || text.Contains("♪") || text.Contains("嘿嘿") || text.Contains("太好了")) return "smile";
            if (text.Contains("！？") || text.Contains("?!") || text.StartsWith("咦") || text.StartsWith("什麼")) return "surprised";
            if (text.StartsWith("……") || text.Contains("對不起")) return "sad";
            if (text.Contains("！")) return "serious";
            return "normal";
        }

        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static Texture2D Load(string path)
        {
            if (cache.TryGetValue(path, out var t)) return t;
            t = Resources.Load<Texture2D>(path);
            cache[path] = t;
            return t;
        }
        /// <summary>立繪（找不到回傳 null）</summary>
        public static Texture2D Portrait(string id, string expr)
        {
            if (id == null) return null;
            return Load($"Story2D/chars/{id}_{expr ?? "normal"}") ?? Load($"Story2D/chars/{id}_normal");
        }
        /// <summary>選角畫面的職業頭像：Story2D/classes/{進階職業 id 或職業 id}.png（沒有進階的圖就用基本職業的）</summary>
        public static Texture2D ClassPortrait(string cls, string adv)
            => (string.IsNullOrEmpty(adv) ? null : Load("Story2D/classes/" + adv)) ?? Load("Story2D/classes/" + cls);
        /// <summary>職業頭像的臉部（立繪上方中間）裁成方形，外面一圈顏色；沒有圖回傳 false</summary>
        public static bool DrawFace(Rect r, Texture2D tex, Color ring)
        {
            if (tex == null) return false;
            UiKit.Disc(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), ring);
            GUI.DrawTextureWithTexCoords(r, tex, FaceUV, true);
            return true;
        }
        public static readonly Rect FaceUV = new Rect(0.3f, 0.7f, 0.4f, 0.28f);
        public static Texture2D Background(string id) => string.IsNullOrEmpty(id) ? null : Load("Story2D/bg/" + id);

        public static readonly string[] ThemeKeys = { "volcano", "forest", "sea", "arena", "canyon" };
        public static readonly string[] TownKeys = { "village", "town_forest", "town_port", "town_arena", "town_canyon" };
    }
}
