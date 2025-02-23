using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using F23.StringSimilarity;
using F23.StringSimilarity.Interfaces;

namespace Shoko.Commons.Utils
{
    public static class Misc
    {
        private static readonly IStringDistance DiceSearch = new SorensenDice();

        private static bool IsAllowedSearchCharacter(this char a)
        {
            if (a == 32) return true;
            if (a == '!') return true;
            if (a == '.') return true;
            if (a == '?') return true;
            if (a == '*') return true;
            if (a == '&') return true;
            if (a > 47 && a < 58) return true;
            if (a > 64 && a < 91) return true;
            return a > 96 && a < 123;
        }

        public static string FilterCharacters(this string value, IEnumerable<char> allowed, bool blacklist = false)
        {
            if (!(allowed is HashSet<char> newSet)) newSet = new HashSet<char>(allowed);
            StringBuilder sb = new StringBuilder(value.Length);
            foreach (char a in value)
            {
                if (!(blacklist ^ newSet.Contains(a))) continue;
                sb.Append(a);
            }
            return sb.ToString();
        }

        public static string FilterSearchCharacters(this string value)
        {
            StringBuilder sb = new StringBuilder(value.Length);
            foreach (char a in value)
            {
                if (!a.IsAllowedSearchCharacter()) continue;
                sb.Append(a);
            }
            return sb.ToString();
        }

        public static String CompactWhitespaces(this string s)
        {
            StringBuilder sb = new StringBuilder(s);

            CompactWhitespaces(sb);

            return sb.ToString();
        }

        private static void CompactWhitespaces(StringBuilder sb)
        {
            if (sb.Length == 0)
                return;

            // set [start] to first not-whitespace char or to sb.Length

            int start = 0;

            while (start < sb.Length)
            {
                if (Char.IsWhiteSpace(sb[start]))
                    start++;
                else
                    break;
            }
            if (start == sb.Length)
            {
                sb.Length = 0;
                return;
            }
            int end = sb.Length - 1;

            while (end >= 0)
            {
                if (Char.IsWhiteSpace(sb[end]))
                    end--;
                else
                    break;
            }
            int dest = 0;
            bool previousIsWhitespace = false;

            for (int i = start; i <= end; i++)
            {
                if (Char.IsWhiteSpace(sb[i]))
                {
                    if (previousIsWhitespace) continue;
                    previousIsWhitespace = true;
                    sb[dest] = ' ';
                    dest++;
                }
                else
                {
                    previousIsWhitespace = false;
                    sb[dest] = sb[i];
                    dest++;
                }
            }

            sb.Length = dest;
        }

        public class SearchInfo<T>
        {
            public T Result { get; set; }
            public int Index { get; set; }
            public double Distance { get; set; }
            public bool ExactMatch { get; set; }

            protected bool Equals(SearchInfo<T> other)
            {
                return Index == other.Index && Math.Abs(Distance - other.Distance) < 0.0001D && ExactMatch == other.ExactMatch;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((SearchInfo<T>) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Index;
                    hashCode = (hashCode * 397) ^ Distance.GetHashCode();
                    hashCode = (hashCode * 397) ^ ExactMatch.GetHashCode();
                    return hashCode;
                }
            }

            public static bool operator ==(SearchInfo<T> left, SearchInfo<T> right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(SearchInfo<T> left, SearchInfo<T> right)
            {
                return !Equals(left, right);
            }
        }

        public static SearchInfo<T> DiceFuzzySearch<T>(string text, string pattern, int k, T value)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
                return new SearchInfo<T> {Index = -1, Distance = int.MaxValue};
            // This forces ASCII, because it's faster to stop caring if ss and ß are the same
            // No it's not perfect, but it works better for those who just want to do lazy searching
            string inputString = text.FilterSearchCharacters();
            string query = pattern.FilterSearchCharacters();
            inputString = inputString.Replace('_', ' ').Replace('-', ' ');
            query = query.Replace('_', ' ').Replace('-', ' ');
            query = query.CompactWhitespaces();
            inputString = inputString.CompactWhitespaces();
            // Case insensitive. We just removed the fancy characters, so latin alphabet lowercase is all we should have
            query = query.ToLowerInvariant();
            inputString = inputString.ToLowerInvariant();

            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(inputString))
                return new SearchInfo<T> {Index = -1, Distance = int.MaxValue};

            int index = inputString.IndexOf(query, StringComparison.Ordinal);
            // Shortcut
            if (index > -1)
            {
                return new SearchInfo<T> {Index = index, Distance = 0, ExactMatch = true, Result = value};
            }

            // always search the longer string for the shorter one
            if (query.Length > inputString.Length)
            {
                string temp = query;
                query = inputString;
                inputString = temp;
            }

            double result = DiceSearch.Distance(inputString, query);
            // Don't count an error as liberally when the title is short
            if (inputString.Length < 5 && result > 0.5) return new SearchInfo<T> {Index = -1, Distance = int.MaxValue};

            if (result >= 0.8) return new SearchInfo<T> {Index = -1, Distance = int.MaxValue};

            return new SearchInfo<T> {Index = 0, Distance = result, Result = value};
        }

        public static bool FuzzyMatches(this string text, string query)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query)) return false;
            int k = Math.Max(Math.Min((int)(text.Length / 6D), (int)(query.Length / 6D)), 1);
            SearchInfo<string> result = DiceFuzzySearch(text, query, k, text);
            if (result.ExactMatch) return true;
            if (text.Length <= 5 && result.Distance > 0.5D) return false;
            return result.Distance < 0.8D;
        }

        public static bool IsImageValid(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                var bytes = new byte[12];
                if (fs.Length < 12) return false;
                fs.Read(bytes, 0, 12);
                return GetImageFormat(bytes) != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsImageValid(byte[] bytes)
        {
            try
            {
                if (bytes.Length < 12) return false;
                return GetImageFormat(bytes) != null;
            }
            catch
            {
                return false;
            }
        }

        public static string? GetImageFormat(byte[] bytes)
        {
            // https://en.wikipedia.org/wiki/BMP_file_format#File_structure
            var bmp = new byte[] { 66, 77 };
            // https://en.wikipedia.org/wiki/GIF#File_format
            var gif = new byte[] { 71, 73, 70 };
            // https://en.wikipedia.org/wiki/JPEG#Syntax_and_structure
            var jpeg = new byte[] { 255, 216 };
            // https://en.wikipedia.org/wiki/Portable_Network_Graphics#File_header
            var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            // https://en.wikipedia.org/wiki/TIFF#Byte_order
            var tiff1 = new byte[] { 73, 73, 42, 0 };
            var tiff2 = new byte[] { 77, 77, 42, 0 };
            // https://developers.google.com/speed/webp/docs/riff_container#webp_file_header
            var webp1 = new byte[] { 82, 73, 70, 70 };
            var webp2 = new byte[] { 87, 69, 66, 80 };

            if (png.SequenceEqual(bytes.Take(png.Length)))
                return "png";

            if (jpeg.SequenceEqual(bytes.Take(jpeg.Length)))
                return "jpeg";

            if (webp1.SequenceEqual(bytes.Take(webp1.Length)) &&
                webp2.SequenceEqual(bytes.Skip(8).Take(webp2.Length)))
                return "webp";

            if (gif.SequenceEqual(bytes.Take(gif.Length)))
                return "gif";

            if (bmp.SequenceEqual(bytes.Take(bmp.Length)))
                return "bmp";

            if (tiff1.SequenceEqual(bytes.Take(tiff1.Length)) ||
                tiff2.SequenceEqual(bytes.Take(tiff2.Length)))
                return "tiff";

            return null;
        }
    }
}
