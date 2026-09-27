using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EchoShift
{
    /// <summary>Minimal JSON reader (objects, arrays, strings, numbers, bools, null). JsonUtility cannot read dictionaries.</summary>
    public static class Json
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int i = 0;
            try { return ParseValue(json, ref i); }
            catch { return null; }
        }

        public static Dictionary<string, object> ParseObject(string json) => Parse(json) as Dictionary<string, object>;

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return ParseObj(s, ref i);
            if (c == '[') return ParseArr(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (Match(s, i, "true")) { i += 4; return true; }
            if (Match(s, i, "false")) { i += 5; return false; }
            if (Match(s, i, "null")) { i += 4; return null; }
            return ParseNumber(s, ref i);
        }

        static bool Match(string s, int i, string word) => string.CompareOrdinal(s, i, word, 0, word.Length) == 0;

        static Dictionary<string, object> ParseObj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == '}') { i++; break; }
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                d[key] = ParseValue(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; break; }
            }
            return d;
        }

        static List<object> ParseArr(string s, ref int i)
        {
            var list = new List<object>();
            i++;
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == ']') { i++; break; }
                list.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; break; }
            }
            return list;
        }

        static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c != '\\' || i >= s.Length) { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 <= s.Length)
                        {
                            sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                            i += 4;
                        }
                        break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }

        static object ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
            return value;
        }

        public static string Quote(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        public static string Str(this Dictionary<string, object> d, string key, string fallback = "")
            => d != null && d.TryGetValue(key, out var v) && v is string s ? s : fallback;

        public static double Num(this Dictionary<string, object> d, string key, double fallback = 0)
            => d != null && d.TryGetValue(key, out var v) && v is double n ? n : fallback;

        public static bool Bool(this Dictionary<string, object> d, string key, bool fallback = false)
            => d != null && d.TryGetValue(key, out var v) && v is bool b ? b : fallback;

        public static Dictionary<string, object> Obj(this Dictionary<string, object> d, string key)
            => d != null && d.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        public static List<object> Arr(this Dictionary<string, object> d, string key)
            => d != null && d.TryGetValue(key, out var v) ? v as List<object> : null;
    }

    /// <summary>Tiny streaming JSON writer used to build the world state sent to Gemini.</summary>
    public sealed class JsonWriter
    {
        readonly StringBuilder sb = new StringBuilder(2048);
        readonly Stack<bool> first = new Stack<bool>();
        bool afterKey;

        public JsonWriter BeginObject() { Sep(); sb.Append('{'); first.Push(true); return this; }
        public JsonWriter EndObject() { sb.Append('}'); first.Pop(); return this; }
        public JsonWriter BeginArray() { Sep(); sb.Append('['); first.Push(true); return this; }
        public JsonWriter EndArray() { sb.Append(']'); first.Pop(); return this; }

        public JsonWriter Key(string key)
        {
            Sep();
            sb.Append(Json.Quote(key)).Append(':');
            afterKey = true;
            return this;
        }

        public JsonWriter Value(string v) { Sep(); sb.Append(Json.Quote(v)); return this; }
        public JsonWriter Value(bool v) { Sep(); sb.Append(v ? "true" : "false"); return this; }
        public JsonWriter Value(float v) { Sep(); sb.Append(v.ToString("0.###", CultureInfo.InvariantCulture)); return this; }
        public JsonWriter Value(int v) { Sep(); sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }

        public JsonWriter Prop(string key, string v) => Key(key).Value(v);
        public JsonWriter Prop(string key, bool v) => Key(key).Value(v);
        public JsonWriter Prop(string key, float v) => Key(key).Value(v);
        public JsonWriter Prop(string key, int v) => Key(key).Value(v);

        void Sep()
        {
            if (afterKey) { afterKey = false; return; }
            if (first.Count == 0) return;
            if (first.Peek()) { first.Pop(); first.Push(false); }
            else sb.Append(',');
        }

        public override string ToString() => sb.ToString();
    }
}
