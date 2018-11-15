using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v2.Modules
{
    [ApiController]
    [Route("/api/dev")]
    [ApiVersion("2.0")]
    public class Dev : BaseController
    {
        /*public Dev() : base("")
        {
            //Get("/contracts/{entity?}", x => { return ExtractContracts((string) x.entity); }); //ContractExtractor was removed.
            //Get("/relationtree/{id?}", x => { return GetRelationTree((string) x.id); });
        }

        /// <summary>
        /// Dumps the contracts as JSON files embedded in a zip file.
        /// </summary>
        /// <param name="entityType">The type of the entity to dump (can be <see cref="string.Empty"/> or <c>null</c> to dump all).</param>
        private object ExtractContracts(string entityType)
        {
            var zipStream = new ContractExtractor().GetContractsAsZipStream(entityType);

            return new StreamResponse(() => zipStream, "application/zip").AsAttachment("contracts.zip");
        }*/

        private class Relation
        {
            public int AnimeID { get; set; }
            public string MainTitle { get; set; }
            public List<Relation> Relations { get; set; }
        }

        [HttpGet("relationtree")]
        private List<Relation> GetRelationTreeForAll()
        {
            var series = Repo.Instance.AnimeSeries.GetAll().Select(a => a.AniDB_ID).OrderBy(a => a).ToArray();
            List<Relation> result = new List<Relation>(series.Length);
            foreach (var i in series)
            {
                var relations = Repo.Instance.AniDB_Anime_Relation.GetFullLinearRelationTree(i);
                var anime = Repo.Instance.AniDB_Anime.GetByID(i);
                result.Add(new Relation
                {
                    AnimeID = i,
                    MainTitle = anime?.MainTitle,
                    Relations = relations.Select(a => new Relation
                    {
                        AnimeID = a,
                        MainTitle = Repo.Instance.AniDB_Anime.GetByID(a)?.MainTitle
                    }).ToList()
                });
            }

            return result;
        }

        [HttpGet("relationtree/{id}")]
        private Relation GetRelationTreeForAnime(int id)
        {
            var anime = Repo.Instance.AniDB_Anime.GetByID(id);
            if (anime == null) return null;
            var relations = Repo.Instance.AniDB_Anime_Relation.GetFullLinearRelationTree(id);

            return new Relation
            {
                AnimeID = id,
                MainTitle = anime?.MainTitle,
                Relations = relations.Select(a => new Relation
                {
                    AnimeID = a,
                    MainTitle = Repo.Instance.AniDB_Anime.GetByID(a)?.MainTitle
                }).ToList()
            };
        }
    }
}
