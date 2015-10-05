using System;
using System.Collections.Generic;
using System.Linq;
using JMMModels.Childs;
using JMMModels.ClientExtensions;

namespace JMMModels.Extensions
{
    public static class ImageExtensions
    {

        #region Helpers

        private static void AddImageIfExists(List<IImageInfo> infos, IImageInfo info)
        {
            if (string.IsNullOrEmpty(info.ImageLocalPath))
                return;
            ImageInfo i=new ImageInfo();
            info.CopyTo(i);
            infos.Add(i);
        }
        private static void AddCollectionIfExists(List<IImageInfo> infos, IEnumerable<IImageInfo> source)
        {
            if ((source == null) || (source.Count() == 0))
                return;
            foreach(ImageInfo i in source)
                AddImageIfExists(infos,i);
        }

        private static List<IImageInfo> GetImageWrapper(Func<AnimeSerie, Func<List<IImageInfo>>> func, AnimeSerie serie)  
        {
            List<IImageInfo> infos = new List<IImageInfo>();
            if (serie.AniDB_Anime == null)
                return infos;
            infos.AddRange(func(serie)());
            return infos;
        }
        private static List<IImageInfo> GetImageWrapper<T>(Func<AnimeSerie, Func<T, List<IImageInfo>>> func, AnimeSerie serie, T input)
        {
            List<IImageInfo> infos = new List<IImageInfo>();
            if (serie.AniDB_Anime == null)
                return infos;
            infos.AddRange(func(serie)(input));
            return infos;
        }
        private static List<IImageInfo> GetImageWrapper(Func<AnimeGroup, Func<List<IImageInfo>>> func, AnimeGroup serie)
        {
            List<IImageInfo> infos = new List<IImageInfo>();
            if (serie.AnimeSeries == null)
                return infos;
            foreach (AnimeSerie a in serie.AnimeSeries)
                infos.AddRange(func(serie)());
            return infos;
        }
        private static List<IImageInfo> GetImageWrapper<T>(Func<AnimeGroup, Func<T, List<IImageInfo>>> func, AnimeGroup serie, T input)
        {
            List<IImageInfo> infos = new List<IImageInfo>();
            if (serie.AnimeSeries == null)
                return infos;
            foreach (AnimeSerie a in serie.AnimeSeries)
                infos.AddRange(func(serie)(input));
            return infos;
        }

        #endregion

        public static List<IImageInfo> GetCovers(this AniDB_Anime anime)
        {
            List<IImageInfo> infos = new List<IImageInfo>();
            AddImageIfExists(infos, anime);
            AddCollectionIfExists(infos, anime.MovieDBPosters);
            AddCollectionIfExists(infos, anime.TvDBPosters);
            AddCollectionIfExists(infos, anime.TraktPosters);
            return infos;
        }

        public static List<IImageInfo> GetCovers(this AnimeSerie serie)
        {
            return GetImageWrapper(a => a.GetCovers, serie);
        }



        public static List<IImageInfo> GetSeasonCovers(this AniDB_Anime anime, int season)
        {
            List<IImageInfo> infos = new List<IImageInfo>();
            AddCollectionIfExists(infos, anime.TvDBPosters.Where(a=>a.Season==season));
            AddCollectionIfExists(infos, anime.TraktPosters.Where(a=>a.Season==season));
            return infos;
        }
        public static List<IImageInfo> GetSeasonCovers(this AnimeSerie serie, int season)
        {
            return GetImageWrapper(a => a.GetSeasonCovers, serie, season);
        }


        public static List<IImageInfo> GetFanarts(this AnimeSerie serie)
        {
            return GetImageWrapper(a => a.GetFanarts, serie);
        }



        public static List<IImageInfo> GetFanarts(this AniDB_Anime anime)
        {
            List<IImageInfo> infos = new List<IImageInfo>();
            AddCollectionIfExists(infos, anime.MovieDBFanarts);
            AddCollectionIfExists(infos, anime.TvDBFanarts);
            AddCollectionIfExists(infos, anime.TraktFanarts);
            return infos;
        }

        public static List<IImageInfo> GetBanners(this AniDB_Anime anime)
        {
            List<IImageInfo> infos = new List<IImageInfo>();
            AddCollectionIfExists(infos, anime.TvDBBanners);
            return infos;
        }

        public static List<IImageInfo> GetBanners(this AnimeSerie serie)
        {
            return GetImageWrapper(a => a.GetBanners, serie);
        }



    }
}
