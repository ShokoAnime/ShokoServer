using JMMContracts.PlexAndKodi;
using JMMServer.Entities;
using System.Collections.Generic;

namespace JMMServer.API.Model.common
{
    public class Episode : BaseDirectory
    {
        public override string type { get { return "ep"; } }
        public string season { get; set; }
        public string votes { get; set; }
        public int view { get; set; }
        public string eptype { get; set; }
        public int epnumber { get; set; }
        public List<RawFile> files { get; set; }

        public Episode()
        {

        }

        internal Episode GenerateFromAnimeEpisodeID(int anime_episode_id, int uid, int level, bool all)
        {
            Episode ep = new Episode();

            if (anime_episode_id > 0)
            {
                ep = GenerateFromAnimeEpisode(Repositories.RepoFactory.AnimeEpisode.GetByID(anime_episode_id), uid, level, all);
            }

            return ep;
        }

        internal Episode GenerateFromAnimeEpisode(AnimeEpisode aep, int uid, int level, bool all)
        {
            Episode ep = new Episode();
            if (aep != null)
            {
                JMMContracts.Contract_AnimeEpisode cae = aep.GetUserContract(uid);
                if (cae != null)
                {

                    ep.id = aep.AnimeEpisodeID;
                    ep.art = new ArtCollection();
                    ep.name = aep.PlexContract?.Title;
                    ep.summary = aep.PlexContract?.Summary;
                    ep.year = aep.PlexContract?.Year;
                    ep.air = aep.PlexContract?.AirDate.ToString();
                    ep.votes = cae.AniDB_Votes;
                    ep.rating = aep.PlexContract?.Rating;
                    ep.userrating = aep.PlexContract?.UserRating;
                    double rating;
                    if (double.TryParse(ep.rating, out rating))
                    {
                        // 0.1 should be the absolute lowest rating
                        if (rating > 10) ep.rating = (rating / 100).ToString().Replace(',','.');
                    }

                    ep.view = cae.IsWatched;
                    ep.epnumber = cae.EpisodeNumber;
                    ep.eptype = aep.EpisodeTypeEnum.ToString();

                    ep.season = aep.PlexContract?.Season;

                    // until fanart refactor this will be good for start
                    if (aep.PlexContract?.Thumb != null) { ep.art.thumb.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(aep.PlexContract?.Thumb), index = 0 }); }
                    if (aep.PlexContract?.Art != null) { ep.art.fanart.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(aep.PlexContract?.Art), index = 0 }); }

                    if (level != 1)
                    {
                        List<VideoLocal> vls = Repositories.RepoFactory.VideoLocal.GetByAniDBEpisodeID(aep.AniDB_EpisodeID);
                        if (vls.Count > 0)
                        {
                            ep.files = new List<RawFile>();
                            foreach (VideoLocal vl in vls)
                            {
                                ep.files.Add(new RawFile(vl, (level-1), uid));
                            }
                        }
                        else
                        {
                            if (!all)
                            {
                                return null;
                            }
                        }
                    }
                }
            }

            return ep;
        }
    }
}
