using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class MovieDB_MovieRepository : BaseRepository<MovieDB_Movie, int>
    {
        private PocoIndex<int, MovieDB_Movie, int> Movies;

        internal override int SelectKey(MovieDB_Movie entity) => entity.MovieDB_MovieID;

        internal override void PopulateIndexes()
        {
            Movies = new PocoIndex<int, MovieDB_Movie, int>(Cache, a => a.MovieId);

        }

        internal override void ClearIndexes()
        {
            Movies = null;

        }

        public List<MovieDB_Movie> GetByMovieID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Movies.GetMultiple(id);
                return Table.Where(a => a.MovieId == id).ToList();
            }
        }
        public Dictionary<int, List<MovieDB_Movie>> GetByMoviesIds(IEnumerable<int> ids)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return ids.ToDictionary(a=>a,a => Movies.GetMultiple(a).ToList());
                return Table.Where(a => ids.Contains(a.MovieId)).GroupBy(a=>a.MovieId).ToDictionary(a=>a.Key,a=>a.ToList());
            }
        }


        public Dictionary<int, (CrossRef_AniDB_Other, MovieDB_Movie)> GetByAnimeIDs(IEnumerable<int> animeIds)
        {
            if (animeIds == null)
                return new Dictionary<int, (CrossRef_AniDB_Other, MovieDB_Movie)>();

            Dictionary<int, CrossRef_AniDB_Other> crosses = Repo.CrossRef_AniDB_Other.GetByAnimeIDsAndTypes(animeIds, CrossRefType.MovieDB).ToDictionary(a => a.Key, a => a.Value.First());
            List<int> movids = crosses.Select(a => a.Value).Distinct().Select(a=>int.Parse(a.CrossRefID)).ToList();
            Dictionary<int, MovieDB_Movie> images = GetByMoviesIds(movids).ToDictionary(a=>a.Key,a=>a.Value.First());
            return crosses.ToDictionary(a => a.Key, a => (a.Value, images.ContainsKey(int.Parse(a.Value.CrossRefID)) ? images[int.Parse(a.Value.CrossRefID)] : null));

        }
    }
}