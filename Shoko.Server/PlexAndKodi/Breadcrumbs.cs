using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Force.DeepCloner;
using NLog;
using Shoko.Models.PlexAndKodi;

namespace Shoko.Server.PlexAndKodi
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
            cache.GrandParentTitle = cache.ParentTitle ?? string.Empty;
            cache.ParentKey = cache.Key;
            cache.ParentTitle = cache.Title ?? string.Empty;
            cache.ParentIndex = cache.Index;

            cache.Key = v.Key;
            cache.Title = v.Title ?? string.Empty;
            cache.Index = v.Index.ToString();
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
            if (Key.Contains("/Metadata/") && !Key.Contains(md5))
                Key += "/" + md5;
        }

        public void FillInfo(IProvider prov, Video m, bool noimage, bool addkey = true)
        {
            if (Key != null)
            {
                LogManager.GetCurrentClassLogger().Info("Key found " + Key);

                if (addkey && Key.Contains("/Metadata/"))
                {
                    string md5 = ToKey();
                    string finalurl = Key + "/" + md5;
                    if (!string.IsNullOrEmpty(prov.ExcludeTags))
                        finalurl += "?excludetags=" + prov.ExcludeTags;
                    m.Key = prov.Proxyfy(finalurl);
                }
                else
                {
                    string finalurl = Key;
                    if (!string.IsNullOrEmpty(prov.ExcludeTags))
                        finalurl += "?excludetags=" + prov.ExcludeTags;
                    m.Key = prov.Proxyfy(finalurl);
                }
            }
            if (ParentKey != null)
                m.ParentKey = prov.Proxyfy(ParentKey);
            if (GrandParentKey != null)
                m.GrandparentKey = prov.Proxyfy(GrandParentKey);
            m.ParentTitle = ParentTitle ?? string.Empty;
            m.GrandparentTitle = GrandParentTitle ?? string.Empty;
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
            if (key == null) return new BreadCrumbs();
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