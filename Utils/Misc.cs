using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using DirectoryInfo = Pri.LongPath.DirectoryInfo;

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
        public static string ToName<T,U>(this Expression<Func<T, U>> expr)
        {
            var member = expr.Body as MemberExpression;
            if (member == null)
            {
                var ue = expr.Body as UnaryExpression;
                if (ue != null)
                    member = ue.Operand as MemberExpression;
            }
            return member?.Member.Name;
        }
        public static Dictionary<string, bool> GetSortDescriptions(this CL_GroupFilter gf)
        {
            Dictionary<string, bool> lst = new Dictionary<string, bool>();
            List<GroupFilterSortingCriteria> criterias = GroupFilterSortingCriteria.Create(gf.GroupFilterID, gf.SortingCriteria);
            foreach (GroupFilterSortingCriteria f in criterias)
            {
                KeyValuePair<string, bool> k = GetSortDescription(f.SortType, f.SortDirection);
                lst[k.Key] = k.Value;
            }
            return lst;
        }

        public static IQueryable<T> SortGroups<T>(this CL_GroupFilter gf, IQueryable<T> list) where T: CL_AnimeGroup_User
        {
            List<GroupFilterSortingCriteria> criterias = GroupFilterSortingCriteria.Create(gf.GroupFilterID, gf.SortingCriteria);
            foreach (GroupFilterSortingCriteria f in criterias)
            {
                list = GeneratePredicate(list, f.SortType, f.SortDirection);
            }
            return list;
        }

        public static IQueryable<T> GeneratePredicate<T>(this IQueryable<T> lst, GroupFilterSorting sortType, GroupFilterSortDirection sortDirection) where T : CL_AnimeGroup_User
        {
            Expression<Func<T, dynamic>> selector;

            switch (sortType)
            {
                case GroupFilterSorting.AniDBRating:
                    selector = c =>c.Stat_AniDBRating;
                    break;
                case GroupFilterSorting.EpisodeAddedDate:
                    selector = c => c.EpisodeAddedDate;
                    break;
                case GroupFilterSorting.EpisodeAirDate:
                    selector = c => c.LatestEpisodeAirDate;
                    break;
                case GroupFilterSorting.EpisodeWatchedDate:
                    selector = c => c.WatchedDate;
                    break;
                case GroupFilterSorting.GroupName:
                    selector = c => c.GroupName;
                    break;
                case GroupFilterSorting.SortName:
                    selector = c => c.SortName;
                    break;
                case GroupFilterSorting.MissingEpisodeCount:
                    selector = c => c.MissingEpisodeCount;
                    break;
                case GroupFilterSorting.SeriesAddedDate:
                    selector = c => c.Stat_SeriesCreatedDate;
                    break;
                case GroupFilterSorting.SeriesCount:
                    selector = c => c.Stat_SeriesCount;
                    break;
                case GroupFilterSorting.UnwatchedEpisodeCount:
                    selector = c => c.UnwatchedEpisodeCount;
                    break;
                case GroupFilterSorting.UserRating:
                    selector = c => c.Stat_UserVoteOverall;
                    break;
                case GroupFilterSorting.Year:
                    if (sortDirection == GroupFilterSortDirection.Asc)
                        selector = c => c.Stat_AirDate_Min;   
                    else
                        selector = c => c.Stat_AirDate_Max;
                    break;
                default:
                    selector = c => c.GroupName;
                    break;
            }
            if (lst is IOrderedQueryable<T>)
            {
                IOrderedQueryable<T> n = (IOrderedQueryable<T>) lst;
                if (sortDirection != GroupFilterSortDirection.Asc)
                    return n.ThenByDescending(selector);
                return n.ThenBy(selector);

            }
            if (sortDirection != GroupFilterSortDirection.Asc)
                return lst.OrderByDescending(selector);
            return lst.OrderBy(selector);

        }

        public static KeyValuePair<string, bool> GetSortDescription(this GroupFilterSorting sortType, GroupFilterSortDirection sortDirection)
        {
            string sortColumn = "";
            bool srt = false;
            switch (sortType)
            {
                case GroupFilterSorting.AniDBRating:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, decimal>>)(c => c.Stat_AniDBRating)).ToName();
                    break;
                case GroupFilterSorting.EpisodeAddedDate:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, DateTime?>>)(c => c.EpisodeAddedDate)).ToName();
                    break;
                case GroupFilterSorting.EpisodeAirDate:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, DateTime?>>)(c => c.LatestEpisodeAirDate)).ToName();
                    break;
                case GroupFilterSorting.EpisodeWatchedDate:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, DateTime?>>)(c => c.WatchedDate)).ToName();
                    break;
                case GroupFilterSorting.GroupName:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, string>>)(c => c.GroupName)).ToName();
                    break;
                case GroupFilterSorting.SortName:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, string>>)(c => c.SortName)).ToName();
                    break;
                case GroupFilterSorting.MissingEpisodeCount:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, int>>)(c => c.MissingEpisodeCount)).ToName();
                    break;
                case GroupFilterSorting.SeriesAddedDate:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, DateTime?>>)(c => c.Stat_SeriesCreatedDate)).ToName();
                    break;
                case GroupFilterSorting.SeriesCount:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, int>>)(c => c.Stat_SeriesCount)).ToName();
                    break;
                case GroupFilterSorting.UnwatchedEpisodeCount:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, int>>)(c => c.UnwatchedEpisodeCount)).ToName();
                    break;
                case GroupFilterSorting.UserRating:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, decimal?>>)(c => c.Stat_UserVoteOverall)).ToName();
                    break;
                case GroupFilterSorting.Year:
                    sortColumn = sortDirection == GroupFilterSortDirection.Asc ? ((Expression<Func<CL_AnimeGroup_User, DateTime?>>)(c => c.Stat_AirDate_Min)).ToName() : ((Expression<Func<CL_AnimeGroup_User, DateTime?>>)(c => c.Stat_AirDate_Max)).ToName();
                    break;
                case GroupFilterSorting.GroupFilterName:
                    sortColumn = ((Expression<Func<CL_GroupFilter, string>>)(c => c.GroupFilterName)).ToName();
                    break;
                default:
                    sortColumn = ((Expression<Func<CL_AnimeGroup_User, string>>)(c => c.GroupName)).ToName();
                    break;
            }

            if (sortDirection != GroupFilterSortDirection.Asc)
                srt = true;
            return new KeyValuePair<string, bool>(sortColumn,srt);
        }

        public static Stream DownloadWebBinary(string url)
        {
            try
            {
                HttpWebResponse response = null;
                HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(url);
                // Note: some network proxies require the useragent string to be set or they will deny the http request
                // this is true for instance for EVERY thailand internet connection (also needs to be set for banners/episodethumbs and any other http request we send)
                webReq.UserAgent = "Anime2MP";
                webReq.Timeout = 20000; // 20 seconds
                response = (HttpWebResponse)webReq.GetResponse();

                return response.GetResponseStream();
            }
            catch
            {
                //BaseConfig.MyAnimeLog.Write(ex.ToString());
                return null;
            }
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
        public static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length; //length of s
            int m = t.Length; //length of t

            int[,] d = new int[n + 1, m + 1]; // matrix

            int cost; // cost

            // Step 1
            if (n == 0) return m;
            if (m == 0) return n;

            // Step 2
            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 0; j <= m; d[0, j] = j++) ;

            // Step 3
            for (int i = 1; i <= n; i++)
            {
                //Step 4
                for (int j = 1; j <= m; j++)
                {
                    // Step 5
                    cost = t.Substring(j - 1, 1) == s.Substring(i - 1, 1) ? 0 : 1;

                    // Step 6
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            // Step 7
            return d[n, m];
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

            if (String.IsNullOrEmpty(query)) return -1;
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

            if (String.IsNullOrEmpty(query)) return -1;
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
        private static readonly SecurityIdentifier _everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        public static List<string> RecursiveGetDirectoriesWithoutEveryonePermission(string path)
        {
            List<string> dirs=new List<string>();
            if (!Pri.LongPath.Directory.Exists(path))
                return dirs;
            DirectoryInfo info=new DirectoryInfo(path);
            AuthorizationRuleCollection coll = info.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier));
            bool found = false;
            foreach (AuthorizationRule ar in coll)
            {
                if (ar.IdentityReference.Value == _everyone.Value)
                {
                    FileSystemAccessRule facr = (FileSystemAccessRule)ar;
                    if (facr.AccessControlType == AccessControlType.Allow && 
                        facr.FileSystemRights.HasFlag(FileSystemRights.FullControl))
                    {
                        found = true;
                        break;
                    }                
                }
            }
            if (!found)
                dirs.Add(path);
            foreach (string s in Directory.GetDirectories(path))
            {
                dirs.AddRange(RecursiveGetDirectoriesWithoutEveryonePermission(s));
            }
            return dirs;
        }

        public static bool RecursiveSetDirectoriesToEveryoneFullControlPermission(List<string> paths)
        {
            //C# version do not work, do not inherit permissions to childs.
            string BatchFile = Path.Combine(Path.GetTempPath(), "GrantAccess.bat");
            int exitCode;
            Process proc = new Process();

            proc.StartInfo.FileName = "cmd.exe";
            proc.StartInfo.Arguments = $@"/c {BatchFile}";
            proc.StartInfo.Verb = "runas";
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            proc.StartInfo.UseShellExecute = true;

            try
            {
                StreamWriter BatchFileStream = new StreamWriter(BatchFile);

                //Cleanup previous
                try
                {
                    foreach(string fullPath in paths)
                        BatchFileStream.WriteLine($"icacls \"{fullPath}\" /grant *S-1-1-0:(OI)(CI)F /T");
                }
                finally
                {
                    BatchFileStream.Close();
                }

                proc.Start();

                proc.WaitForExit();

                exitCode = proc.ExitCode;
                proc.Close();

                File.Delete(BatchFile);

                if (exitCode == 0)
                    return true;
            }
            catch (Exception ex)
            {
                //Ignored
            }
            return false;
        }
    }
}
