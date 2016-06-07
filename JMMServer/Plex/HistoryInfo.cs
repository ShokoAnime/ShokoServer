using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using JMMContracts.PlexContracts;

namespace JMMServer.Plex
{
    public class HistoryInfo
    {
        private static int counter;

        private static readonly Dictionary<string, HistoryInfo> Cache = new Dictionary<string, HistoryInfo>();
        //TODO CACHE EVICTION?

        public string Key { get; set; }
        public string ParentKey { get; set; }
        public string GrandParentKey { get; set; }
        public string Title { get; set; }
        public string ParentTitle { get; set; }
        public string GrandParentTitle { get; set; }
        public string Thumb { get; set; }
        public string ParentThumb { get; set; }
        public string GrandParentThumb { get; set; }
        public string Art { get; set; }
        public string ParentArt { get; set; }
        public string GrandParentArt { get; set; }

        public HistoryInfo Update(Video v)
        {
            var cache = new HistoryInfo();
            this.CopyTo(cache);
            cache.GrandParentKey = cache.ParentKey;
            cache.GrandParentTitle = cache.ParentTitle ?? "";
            cache.GrandParentArt = cache.ParentArt;
            cache.GrandParentThumb = cache.ParentThumb;
            cache.ParentKey = cache.Key;
            cache.ParentTitle = cache.Title ?? "";
            cache.ParentArt = cache.Art;
            cache.ParentThumb = cache.Thumb;
            cache.Key = v.Key;
            cache.Title = v.Title ?? "";
            cache.Art = v.Art;
            cache.Thumb = v.Thumb;
            return cache;
        }

        private string GenMd5()
        {
            var bld = new StringBuilder();
            bld.AppendLine(ParentKey);
            bld.AppendLine(GrandParentKey);
            bld.AppendLine(Title);
            bld.AppendLine(ParentTitle);
            bld.AppendLine(GrandParentTitle);
            bld.AppendLine(Thumb);
            bld.AppendLine(ParentThumb);
            bld.AppendLine(GrandParentThumb);
            bld.AppendLine(Art);
            bld.AppendLine(ParentArt);
            bld.AppendLine(GrandParentArt);
            using (var md5 = new MD5CryptoServiceProvider())
            {
                return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(bld.ToString())))
                    .Replace("-", string.Empty);
            }
        }

        public string ToKey()
        {
            var md5 = GenMd5();
            if (Cache.ContainsKey(md5))
                return md5;
            counter++;
            var cache = new HistoryInfo();
            this.CopyTo(cache);
            Cache.Add(md5, cache);
            return md5;
        }

        public static HistoryInfo FromKey(string key)
        {
            if (Cache.ContainsKey(key))
                return Cache[key];
            return new HistoryInfo();
        }
    }
}