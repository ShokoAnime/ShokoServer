using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;


namespace Shoko.Server.Repositories.Repos
{
    public class AniDB_TagRepository : BaseRepository<AniDB_Tag, int>
    {

        internal override void PopulateIndexes()
        {
        }

        internal override void ClearIndexes()
        {
        }



        internal override int SelectKey(AniDB_Tag entity) => entity.TagID;

        public override void PreInit(IProgress<InitProgress> progress, int batchSize)
        {
            List<AniDB_Tag> tags=Where(tag => (tag.TagDescription != null && tag.TagDescription.Contains('`')) || tag.TagName.Contains('`')).ToList();
            if (tags.Count == 0)
                return;
            InitProgress regen = new InitProgress();
            regen.Title = "Fixing Tag Names";
            regen.Step = 0;
            regen.Total = tags.Count;

            BatchAction(tags, batchSize, (tag, original) =>
            {
                tag.TagDescription = tag.TagDescription?.Replace('`', '\'');
                tag.TagName = tag.TagName.Replace('`', '\'');
                regen.Step++;
                progress.Report(regen);
            });
            regen.Step = regen.Total;
            progress.Report(regen);
        }




        public List<AniDB_Tag> GetByAnimeID(int animeID)
        {
            return GetMany(Repo.Instance.AniDB_Anime_Tag.GetByAnimeID(animeID).Select(a => a.TagID).ToList());
        }



        public Dictionary<int, List<AniDB_Tag>> GetByAnimeIDs(IEnumerable<int> ids)
        {
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            return Repo.Instance.AniDB_Anime_Tag.GetTagsIdByAnimeIDs(ids).ToDictionary(a => a.Key, a => GetMany(a.Value));
        }


        /// <summary>
        /// Gets all the tags, but only if we have the anime locally
        /// </summary>
        /// <returns></returns>
        public List<AniDB_Tag> GetAllForLocalSeries()
        {
            return GetMany(GetByAnimeIDs(Repo.Instance.AnimeSeries.GetAllAnimeIds()).SelectMany(a=>a.Value).Select(a=>a.TagID));
        }

        public Dictionary<string, List<int>> GetGroupByTagName()
        {
            using (RepoLock.ReaderLock())
            {
                return WhereAll().GroupBy(a => a.TagName).ToDictionary(a => a.Key, a => a.Select(b => b.TagID).ToList());
            }
        }

        internal IEnumerable<AniDB_Tag> GetByName(string tagName)
        {
            using (RepoLock.ReaderLock())
            {
                return GetAll().Where(s => s.TagName.Equals(tagName, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        internal AniDB_Tag GetByTagID(int tagID)
        {
            using (RepoLock.ReaderLock())
            {
                return GetAll().FirstOrDefault(s => s.TagID == tagID);
            }
        }
    }
}