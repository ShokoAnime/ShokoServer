using System.Collections.Generic;
using System.Linq;
using Nancy;
using Nancy.Responses;
using Shoko.Server.Repositories;
using Shoko.Server.Tasks;

namespace Shoko.Server.API.v2.Modules
{
    public class Dev : Nancy.NancyModule
    {
        public Dev() : base("/api/dev")
        {
            Get("/contracts/{entity?}", x => { return ExtractContracts((string) x.entity); });
            Get("/relationtree/{id?}", x => { return GetRelationTree((string) x.id); });
        }

        /// <summary>
        /// Dumps the contracts as JSON files embedded in a zip file.
        /// </summary>
        /// <param name="entityType">The type of the entity to dump (can be <see cref="string.Empty"/> or <c>null</c> to dump all).</param>
        private object ExtractContracts(string entityType)
        {
            var zipStream = new ContractExtractor().GetContractsAsZipStream(entityType);

            return new StreamResponse(() => zipStream, "application/zip").AsAttachment("contracts.zip");
        }

        private class Relation
        {
            public int AnimeID { get; set; }
            public string MainTitle { get; set; }
            public List<Relation> Relations { get; set; }
        }

        private object GetRelationTree(string id)
        {
            if (string.IsNullOrEmpty(id)) return GetRelationTreeForAll();
            if (!int.TryParse(id, out int anime)) return GetRelationTreeForAll();
            return GetRelationTreeForAnime(anime);
        }

        private object GetRelationTreeForAll()
        {
            var series = Repo.AnimeSeries.GetAll().Select(a => a.AniDB_ID).OrderBy(a => a).ToArray();
            List<Relation> result = new List<Relation>(series.Length);
            foreach (var i in series)
            {
                var relations = Repo.AniDB_Anime_Relation.GetFullLinearRelationTree(i);
                var anime = Repo.AniDB_Anime.GetByID(i);
                result.Add(new Relation
                {
                    AnimeID = i,
                    MainTitle = anime?.MainTitle,
                    Relations = relations.Select(a => new Relation
                    {
                        AnimeID = a,
                        MainTitle = Repo.AniDB_Anime.GetByID(a)?.MainTitle
                    }).ToList()
                });
            }

            return result;
        }

        private object GetRelationTreeForAnime(int id)
        {
            var anime = Repo.AniDB_Anime.GetByID(id);
            if (anime == null) return null;
            var relations = Repo.AniDB_Anime_Relation.GetFullLinearRelationTree(id);

            return new Relation
            {
                AnimeID = id,
                MainTitle = anime?.MainTitle,
                Relations = relations.Select(a => new Relation
                {
                    AnimeID = a,
                    MainTitle = Repo.AniDB_Anime.GetByID(a)?.MainTitle
                }).ToList()
            };
        }
    }
}
