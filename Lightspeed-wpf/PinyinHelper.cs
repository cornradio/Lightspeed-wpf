using System.Text;
using TinyPinyin;

namespace Lightspeed_wpf
{
    /// <summary>
    /// Chinese → pinyin-initial helper backed by TinyPinyin.
    /// </summary>
    public static class PinyinHelper
    {
        /// <summary>Returns lowercase initials, e.g. "汽水音乐" → "qsyy", "Chrome" → "chrome".</summary>
        public static string GetInitials(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    sb.Append(ch);
                }
                else if (ch >= 'A' && ch <= 'Z')
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
                else if (IsChinese(ch))
                {
                    // TinyPinyin returns e.g. "QI" for 汽
                    string py = PinyinHelperCompat.GetPinyin(ch);
                    if (!string.IsNullOrEmpty(py))
                        sb.Append(char.ToLowerInvariant(py[0]));
                }
            }
            return sb.ToString();
        }

        public static bool Matches(string name, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            if (string.IsNullOrEmpty(name)) return false;

            string nameLower = name.ToLowerInvariant();
            string queryLower = query.ToLowerInvariant();

            if (nameLower.Contains(queryLower))
                return true;

            string initials = GetInitials(name);
            if (initials.Contains(queryLower))
                return true;

            // Also match against full pinyin without spaces/tones: "qishuiyinyue" contains "qishui"
            string full = GetFullPinyin(name);
            if (full.Contains(queryLower))
                return true;

            return false;
        }

        private static string GetFullPinyin(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length * 4);
            foreach (char ch in text)
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                    sb.Append(ch);
                else if (ch >= 'A' && ch <= 'Z')
                    sb.Append(char.ToLowerInvariant(ch));
                else if (IsChinese(ch))
                {
                    string py = PinyinHelperCompat.GetPinyin(ch);
                    if (!string.IsNullOrEmpty(py))
                        sb.Append(py.ToLowerInvariant());
                }
            }
            return sb.ToString();
        }

        private static bool IsChinese(char ch) => ch >= 0x4E00 && ch <= 0x9FFF;
    }

    /// <summary>Thin wrapper so we don't clash with our own PinyinHelper type name.</summary>
    file static class PinyinHelperCompat
    {
        public static string GetPinyin(char ch)
        {
            try
            {
                // TinyPinyin.PinyinHelper.GetPinyin(char) → "QI" / "SHUI"
                return TinyPinyin.PinyinHelper.GetPinyin(ch) ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}
