using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Commons.Utils
{
    public static class Misc
    {
        [DllImport("Shlwapi.dll", CharSet = CharSet.Auto)]
        private static extern long StrFormatByteSize(long fileSize,
            [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer, int bufferSize);

        public static string FormatByteSize(long fileSize)
        {
            StringBuilder sbBuffer = new StringBuilder(20);
            StrFormatByteSize(fileSize, sbBuffer, 20);
            return sbBuffer.ToString();
        }

        public static string DownloadWebPage(string url)
        {
            return DownloadWebPage(url, null, false);
        }

        public static string DownloadWebPage(string url, string cookieHeader, bool setUserAgent)
        {
            try
            {
                HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(url);
                webReq.Timeout = 30000; // 30 seconds
                webReq.Proxy = null;
                webReq.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");

                if (!String.IsNullOrEmpty(cookieHeader))
                    webReq.Headers.Add("Cookie", cookieHeader);
                if (setUserAgent)
                    webReq.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)";

                webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                HttpWebResponse WebResponse = (HttpWebResponse)webReq.GetResponse();

                Stream responseStream = WebResponse.GetResponseStream();
                String enco = WebResponse.CharacterSet;
                Encoding encoding = null;
                if (!String.IsNullOrEmpty(enco))
                    encoding = Encoding.GetEncoding(WebResponse.CharacterSet);
                if (encoding == null)
                    encoding = Encoding.Default;
                StreamReader Reader = new StreamReader(responseStream, encoding);

                string output = Reader.ReadToEnd();

                WebResponse.Close();
                responseStream.Close();

                //logger.Trace("DownloadWebPage Response: {0}", output);

                return output;
            }
            catch (Exception ex)
            {
                string msg = "---------- ERROR IN DOWNLOAD WEB PAGE ---------" + Environment.NewLine +
                             url + Environment.NewLine +
                             ex.ToString() + Environment.NewLine + "------------------------------------";

                // if the error is a 404 error it may mean that there is a bad series association
                // so lets log it to the web cache so we can investigate
                if (ex.ToString().Contains("(404) Not Found"))
                {
                }

                return "";
            }
        }

        public static void DownloadFile(string url, string destFile, string cookieHeader, bool setUserAgent)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    if (!String.IsNullOrEmpty(cookieHeader))
                        client.Headers.Add("Cookie", cookieHeader);
                    if (setUserAgent)
                        client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 1.1.4322; .NET CLR 2.0.50727)");

                    client.DownloadFile(url, destFile);
                }

            }
            catch (Exception ex)
            {
                string msg = "---------- ERROR IN DOWNLOAD WEB PAGE ---------" + Environment.NewLine +
                             url + Environment.NewLine +
                             ex.ToString() + Environment.NewLine + "------------------------------------";

                // if the error is a 404 error it may mean that there is a bad series association
                // so lets log it to the web cache so we can investigate
                if (ex.ToString().Contains("(404) Not Found"))
                {
                }
            }
        }

        // A char array of the allowed characters. This should be infinitely faster
        private static readonly char[] AllowedSearchCharacters =
            (" abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!+-.?,/*&`'\"_").ToCharArray();

        public static string FilterCharacters(this string value, char[] allowed, bool blacklist = false)
        {
            StringBuilder sb = new StringBuilder(value);
            int dest = 0;
            for (int i = 0; i <= sb.Length - 1; i++)
            {
                if (blacklist ^ allowed.Contains(sb[i]))
                {
                    sb[dest] = sb[i];
                    dest++;
                }
            }

            sb.Length = dest;
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
                if (char.IsWhiteSpace(sb[end]))
                    end--;
                else
                    break;
            }
            int dest = 0;
            bool previousIsWhitespace = false;

            for (int i = start; i <= end; i++)
            {
                if (char.IsWhiteSpace(sb[i]))
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

        /// <summary>
        /// Use the Bitap Fuzzy Algorithm to search for a string
        /// This is used in grep, for an easy understanding
        /// ref: https://en.wikipedia.org/wiki/Bitap_algorithm
        /// source: https://www.programmingalgorithms.com/algorithm/fuzzy-bitap-algorithm
        /// </summary>
        /// <param name="text">The string to search</param>
        /// <param name="pattern">The query to search for</param>
        /// <param name="k">The maximum distance (in Levenshtein) to be allowed</param>
        /// <param name="dist">The Levenstein distance of the result. -1 if inapplicable</param>
        /// <returns></returns>
        public static int BitapFuzzySearch32(string text, string pattern, int k, out int dist)
        {
            // This forces ASCII, because it's faster to stop caring if ss and ß are the same
            // No it's not perfect, but it works better for those who just want to do lazy searching
            string inputString = text.FilterCharacters(AllowedSearchCharacters);
            string query = pattern.FilterCharacters(AllowedSearchCharacters);
            inputString = inputString.Replace('_', ' ').Replace('-', ' ');
            query = query.Replace('_', ' ').Replace('-', ' ');
            query = query.CompactWhitespaces();
            inputString = inputString.CompactWhitespaces();
            // Case insensitive. We just removed the fancy characters, so latin alphabet lowercase is all we should have
            query = query.ToLowerInvariant();
            inputString = inputString.ToLowerInvariant();

            // Shortcut
            if (text.Equals(query))
            {
                dist = 0;
                return 0;
            }

            int result = -1;
            int m = query.Length;
            int[] R;
            int[] patternMask = new int[128];
            int i, d;
            dist = k + 1;

            // We are doing bitwise operations, this can be affected by how many bits the CPU is able to process
            int WORD_SIZE = 31;

            if (string.IsNullOrEmpty(query)) return -1;
            if (m > WORD_SIZE) return -1; //Error: The pattern is too long!

            R = new int[(k + 1) * sizeof(int)];
            for (i = 0; i <= k; ++i)
                R[i] = ~1;

            for (i = 0; i <= 127; ++i)
                patternMask[i] = ~0;

            for (i = 0; i < m; ++i)
                patternMask[query[i]] &= ~(1 << i);

            for (i = 0; i < inputString.Length; ++i)
            {
                int oldRd1 = R[0];

                R[0] |= patternMask[inputString[i]];
                R[0] <<= 1;

                for (d = 1; d <= k; ++d)
                {
                    int tmp = R[d];

                    R[d] = (oldRd1 & (R[d] | patternMask[inputString[i]])) << 1;
                    oldRd1 = tmp;
                }

                if (0 == (R[k] & (1 << m)))
                {
                    dist = R[k];
                    result = (i - m) + 1;
                    break;
                }
            }

            return result;
        }

        public static int BitapFuzzySearch64(string text, string pattern, int k, out int dist)
        {
            // This forces ASCII, because it's faster to stop caring if ss and ß are the same
            // No it's not perfect, but it works better for those who just want to do lazy searching
            string inputString = text.FilterCharacters(AllowedSearchCharacters);
            string query = pattern.FilterCharacters(AllowedSearchCharacters);
            inputString = inputString.Replace('_', ' ').Replace('-', ' ');
            query = query.Replace('_', ' ').Replace('-', ' ');
            query = query.CompactWhitespaces();
            inputString = inputString.CompactWhitespaces();
            // Case insensitive. We just removed the fancy characters, so latin alphabet lowercase is all we should have
            query = query.ToLowerInvariant();
            inputString = inputString.ToLowerInvariant();

            // Shortcut
            if (text.Equals(query))
            {
                dist = 0;
                return 0;
            }

            int result = -1;
            int m = query.Length;
            ulong[] R;
            ulong[] patternMask = new ulong[128];
            int i, d;
            dist = text.Length;

            // We are doing bitwise operations, this can be affected by how many bits the CPU is able to process
            int WORD_SIZE = 63;

            if (string.IsNullOrEmpty(query)) return -1;
            if (m > WORD_SIZE) return -1; //Error: The pattern is too long!

            R = new ulong[(k + 1) * sizeof(ulong)];
            for (i = 0; i <= k; ++i)
                R[i] = ~1UL;

            for (i = 0; i <= 127; ++i)
                patternMask[i] = ~0UL;

            for (i = 0; i < m; ++i)
                patternMask[query[i]] &= ~(1UL << i);

            for (i = 0; i < inputString.Length; ++i)
            {
                ulong oldRd1 = R[0];

                R[0] |= patternMask[inputString[i]];
                R[0] <<= 1;

                for (d = 1; d <= k; ++d)
                {
                    ulong tmp = R[d];

                    R[d] = (oldRd1 & (R[d] | patternMask[inputString[i]])) << 1;
                    oldRd1 = tmp;
                }

                if (0 == (R[k] & (1UL << m)))
                {
                    dist = (int)R[k];
                    result = (i - m) + 1;
                    break;
                }
            }

            return result;
        }

        public static int BitapFuzzySearch(string text, string pattern, int k, out int dist)
        {
            if (IntPtr.Size > 4)
            {
                return BitapFuzzySearch64(text, pattern, k, out dist);
            }
            return BitapFuzzySearch32(text, pattern, k, out dist);
        }

        public static bool FuzzyMatches(this string text, string query)
        {
            int k = Math.Max(Math.Min((int)(text.Length / 6D), (int)(query.Length / 6D)), 1);
            if (query.Length <= 4 || text.Length <= 4) k = 0;
            return BitapFuzzySearch(text, query, k, out int dist) > -1;
        }
    }
}
