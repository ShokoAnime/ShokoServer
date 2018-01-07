using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Server;

namespace Shoko.Server.Repositories.Repos
{
    public class MovieDB_FanartRepository : BaseRepository<MovieDB_Fanart, int>
    {
        private PocoIndex<int, MovieDB_Fanart, int> Movies;
        private PocoIndex<int, MovieDB_Fanart, string> Urls;
        private PocoIndex<int, MovieDB_Fanart, string> ImageSizes;

        internal override int SelectKey(MovieDB_Fanart entity) => entity.MovieDB_FanartID;
        
        internal override void PopulateIndexes()
        {
            Movies = new PocoIndex<int, MovieDB_Fanart, int>(Cache, a => a.MovieId);
            Urls = new PocoIndex<int, MovieDB_Fanart, string>(Cache, a => a.URL);
            ImageSizes = new PocoIndex<int, MovieDB_Fanart, string>(Cache, a => a.ImageSize);
        }

        internal override void ClearIndexes()
        {
            Movies = null;
            Urls = null;
            ImageSizes = null;
        }

        public MovieDB_Fanart GetByOnlineID(string url)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Urls.GetOne(url);
                return Table.FirstOrDefault(a => a.URL == url);
            }
        }



        public List<MovieDB_Fanart> GetByMovieID(int id)
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return Movies.GetMultiple(id);
                return Table.Where(a => a.MovieId == id).ToList();
            }
        }
        public Dictionary<int, List<MovieDB_Fanart>> GetByMoviesIds(IEnumerable<int> ids)
        {
            if (ids==null)
                return new Dictionary<int, List<MovieDB_Fanart>>();
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return ids.ToDictionary(a => a, a => Movies.GetMultiple(a).ToList());
                return Table.Where(a => ids.Contains(a.MovieId)).GroupBy(a => a.MovieId).ToDictionary(a=>a.Key, a => a.ToList());
            }
        }

        public Dictionary<int, List<MovieDB_Fanart>> GetByAnimeIDs(IEnumerable<int> animeIds)
        {
            if (animeIds == null)
                return new Dictionary<int, List<MovieDB_Fanart>>();
            Dictionary<int,List<string>> crosses=Repo.CrossRef_AniDB_Other.GetByAnimeIDsAndType(animeIds, CrossRefType.MovieDB);
            List<int> movids = crosses.SelectMany(a => a.Value).Distinct().Select(int.Parse).ToList();
            Dictionary<int, List<MovieDB_Fanart>> images = GetByMoviesIds(movids);
            return crosses.ToDictionary(a => a.Key, a => a.Value.SelectMany(b => images.SafeGetList(int.Parse(b))).ToList());
        }

        public List<MovieDB_Fanart> GetAllOriginal()
        {
            using (CacheLock.ReaderLock())
            {
                if (IsCached)
                    return ImageSizes.GetMultiple(Shoko.Models.Constants.MovieDBImageSize.Original);
                return Table.Where(a => a.ImageSize== Shoko.Models.Constants.MovieDBImageSize.Original).ToList();
            }
        }
    }
}