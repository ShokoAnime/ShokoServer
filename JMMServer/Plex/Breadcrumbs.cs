using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentNHibernate.Utils;
using JMMContracts.PlexContracts;

namespace JMMServer.Plex
{
    public class Breadcrumbs 
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

        private static int counter = 0;
        private static Dictionary<string, Breadcrumbs> Cache=new Dictionary<string, Breadcrumbs>(); //TODO CACHE EVICTION?
        
        public Breadcrumbs Update(Video v, bool noart=false)
        {
            Breadcrumbs cache = new Breadcrumbs();
            this.CopyTo(cache);
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

        public void FillInfo(Video m, bool noimage, bool addkey = true)
        {
            if (Key != null)
            {
                if ((addkey) && (Key.Contains("/GetMetadata/")))
                {
                    m.Key = PlexHelper.PlexProxy(Key + "/" + ToKey());
                }
                else
                {

                    m.Key = PlexHelper.PlexProxy(Key);
                }
            }
            if (ParentKey != null)
                m.ParentKey = PlexHelper.PlexProxy(ParentKey);
            if (GrandParentKey != null)
                m.GrandparentKey = PlexHelper.PlexProxy(GrandParentKey);
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
            StringBuilder bld=new StringBuilder();
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
                return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(bld.ToString()))).Replace("-", string.Empty);
            }
        }
        public string ToKey()
        {
            string md5 = GenMd5();
            if (Cache.ContainsKey(md5))
                return md5;
            counter++;
            Breadcrumbs cache = new Breadcrumbs();
            this.CopyTo(cache);
            Cache.Add(md5,cache);
            return md5;
        }

        public static Breadcrumbs FromKey(string key)
        {
            if (Cache.ContainsKey(key))
            {
                Breadcrumbs n=new Breadcrumbs();
                Cache[key].CopyTo(n);
                n.UpdateKey(key);
                return n;
            }
            return new Breadcrumbs();
        }

    }
}
