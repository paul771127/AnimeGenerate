using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SpellDuel
{
    /// <summary>
    /// 很小的 JSON 讀取器（Unity 的 JsonUtility 不能讀巢狀陣列，例如多邊形 [[x,z],...]）。
    /// 回傳：物件＝Dictionary&lt;string, object&gt;、陣列＝List&lt;object&gt;、數字＝double、字串、bool、null。格式錯誤回傳 null。
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            int i = 0;
            try { var v = Value(s, ref i); return v; } catch { return null; }
        }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(); i++;
                Ws(s, ref i); if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i); string k = Str(s, ref i); Ws(s, ref i); i++;   // ':'
                    d[k] = Value(s, ref i); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return d;   // '}'
                }
            }
            if (c == '[')
            {
                var l = new List<object>(); i++;
                Ws(s, ref i); if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i)); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return l;   // ']'
                }
            }
            if (c == '"') return Str(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder(); i++;   // opening quote
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            i++;
            return sb.ToString();
        }

        // ---- 取值的小工具
        public static Dictionary<string, object> Obj(object o, string k) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) ? v as Dictionary<string, object> : null;
        public static List<object> Arr(object o, string k) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) ? v as List<object> : null;
        public static string Str(object o, string k) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) ? v as string : null;
        public static float Num(object o, string k, float def = 0f) => (o as Dictionary<string, object>) != null && ((Dictionary<string, object>)o).TryGetValue(k, out var v) && v is double d ? (float)d : def;
        public static bool Has(object o, string k) => o is Dictionary<string, object> d && d.ContainsKey(k) && d[k] != null;
    }
}
