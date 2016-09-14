using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using JMMContracts.PlexAndKodi;
using Force.DeepCloner;

namespace JMMServer.PlexAndKodi
{
    public class BreadCrumbs
    {
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
        public string Index { get; set; }
        public string ParentIndex { get; set; }

        private static Dictionary<string, BreadCrumbs> Cache = new Dictionary<string, BreadCrumbs>();
        //TODO CACHE EVICTION?

        public BreadCrumbs Update(Video v, bool noart = false)
        {
            BreadCrumbs cache = this.DeepClone();
            cache.GrandParentKey = cache.ParentKey;
            cache.GrandParentTitle = cache.ParentTitle ?? "";
            cache.ParentKey = cache.Key;
            cache.ParentTitle = cache.Title ?? "";
            cache.ParentIndex = cache.Index;

            cache.Key = v.Key;
            cache.Title = v.Title ?? "";
            cache.Index = v.Index;
            if (!noart)
            {
                cache.GrandParentThumb = cache.ParentThumb;
                cache.GrandParentArt = cache.ParentArt;
                cache.ParentArt = cache.Art;
                cache.ParentThumb = cache.Thumb;
                cache.Art = v.Art;
                cache.Thumb = v.Thumb;
            }
            return cache;
        }

        public void UpdateKey(string md5)
        {
            if (Key.Contains("/GetMetadata/") && !Key.Contains(md5))
                Key += "/" + md5;
        }

        public void FillInfo(IProvider prov, Video m, bool noimage, bool addkey = true)
        {
            if (Key != null)
            {
                if (addkey && Key.Contains("/GetMetadata/"))
                    m.Key = prov.Proxyfy(Key + "/" + ToKey());
                else
                    m.Key = prov.Proxyfy(Key);
            }
            if (ParentKey != null)
                m.ParentKey = prov.Proxyfy(ParentKey);
            if (GrandParentKey != null)
                m.GrandparentKey = prov.Proxyfy(GrandParentKey);
            m.ParentTitle = ParentTitle ?? "";
            m.GrandparentTitle = GrandParentTitle ?? "";
            if (!noimage)
            {
                m.ParentArt = ParentArt;
                m.GrandparentArt = GrandParentArt;
                m.ParentThumb = ParentThumb;
                m.GrandparentThumb = GrandParentThumb;
            }
        }

        private string GenMd5()
        {
            StringBuilder bld = new StringBuilder();
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
            bld.AppendLine(Index);
            bld.AppendLine(ParentIndex);
            using (MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider())
            {
                return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(bld.ToString())))
                    .Replace("-", string.Empty);
            }
        }

        public string ToKey()
        {
            string md5 = GenMd5();
            if (Cache.ContainsKey(md5))
                return md5;
            BreadCrumbs cache = this.DeepClone();
            Cache.Add(md5, cache);
            return md5;
        }

        public static BreadCrumbs FromKey(string key)
        {
            if (Cache.ContainsKey(key))
            {
                BreadCrumbs n = Cache[key].DeepClone();
                n.UpdateKey(key);
                return n;
            }
            return new BreadCrumbs();
        }
    }
}