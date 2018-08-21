using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class MovieDB_PosterRepository : BaseRepository<MovieDB_Poster, int>
    {
        private PocoIndex<int, MovieDB_Poster, int> Movies;
        private PocoIndex<int, MovieDB_Poster, string> Urls;
        private PocoIndex<int, MovieDB_Poster, string> ImageSizes;

        internal override int SelectKey(MovieDB_Poster entity) => entity.MovieDB_PosterID;
            
        internal override void PopulateIndexes()
        {
            Movies = new PocoIndex<int, MovieDB_Poster, int>(Cache, a => a.MovieId);
            Urls = new PocoIndex<int, MovieDB_Poster, string>(Cache, a => a.URL);
            ImageSizes = new PocoIndex<int, MovieDB_Poster, string>(Cache, a => a.ImageSize);
        }

        internal override void ClearIndexes()
        {
            Movies = null;
            Urls = null;
            ImageSizes = null;
        }



        public MovieDB_Poster GetByOnlineID(string url)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Urls.GetOne(url);
                return Table.FirstOrDefault(a => a.URL == url);
            }
        }



        public List<MovieDB_Poster> GetByMovieID(int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Movies.GetMultiple(id);
                return Table.Where(a => a.MovieId == id).ToList();
            }
        }
        public List<MovieDB_Poster> GetAllOriginal()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return ImageSizes.GetMultiple(Shoko.Models.Constants.MovieDBImageSize.Original);
                return Table.Where(a => a.ImageSize == Shoko.Models.Constants.MovieDBImageSize.Original).ToList();
            }
        }

    }
}