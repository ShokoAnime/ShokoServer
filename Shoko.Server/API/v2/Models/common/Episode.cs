using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using System.Collections.Generic;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Server.Models;

namespace Shoko.Server.API.Model.common
{
    public class Episode
    {
        public int id { get; set; }
        public string type { get; set; }
        public ArtCollection art { get; set; }
        public string title { get; set; }
        public string summary { get; set; }
        public string year { get; set; }
        public string air { get; set; }
	    public string season { get; set; }
        public string rating { get; set; }
        public string votes { get; set; }
        public string userrating { get; set; }
        public int view { get; set; }
        public int eptype { get; set; }
        public int epnumber { get; set; }
        public List<RawFile> files { get; set; }

        public Episode()
        {

        }

        internal Episode GenerateFromAnimeEpisodeID(int anime_episode_id, int uid, int level)
        {
            Episode ep = new Episode();

            if (anime_episode_id > 0)
            {
                ep = GenerateFromAnimeEpisode(Repositories.RepoFactory.AnimeEpisode.GetByID(anime_episode_id), uid, level);
            }

            return ep;
        }

        internal Episode GenerateFromAnimeEpisode(SVR_AnimeEpisode aep, int uid, int level)
        {
            Episode ep = new Episode();
            if (aep != null)
            {
                CL_AnimeEpisode_User cae = aep.GetUserContract(uid);
                if (cae != null)
                {

                    ep.id = aep.AniDB_EpisodeID;
                    ep.art = new ArtCollection();
                    ep.type = aep.EpisodeTypeEnum.ToString();
                    ep.title = aep.PlexContract?.Title;
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

                    ep.view = cae.IsWatched() ? 1 : 0;
                    ep.epnumber = cae.EpisodeNumber;
                    ep.eptype = cae.EpisodeType;
	                ep.season = aep.PlexContract?.Season;

                    // until fanart refactor this will be good for start
                    if (aep.PlexContract?.Thumb != null) { ep.art.thumb.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(aep.PlexContract?.Thumb), index = 0 }); }
                    if (aep.PlexContract?.Art != null) { ep.art.fanart.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(aep.PlexContract?.Art), index = 0 }); }

                    if (level != 1)
                    {
                        List<SVR_VideoLocal> vls = Repositories.RepoFactory.VideoLocal.GetByAniDBEpisodeID(ep.id);
                        if (vls.Count > 0)
                        {
                            ep.files = new List<RawFile>();
                            foreach (SVR_VideoLocal vl in vls)
                            {
                                ep.files.Add(new RawFile(vl, (level-1), uid));
                            }
                        }
                    }
                }
            }

            return ep;
        }
    }
}
