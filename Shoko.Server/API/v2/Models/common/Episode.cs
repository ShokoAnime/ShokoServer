using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using Nancy;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Server.Models;

namespace Shoko.Server.API.v2.Models.common
{
    [DataContract]
    public class Episode : BaseDirectory
    {
        public override string type
        {
            get { return "ep"; }
        }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string season { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string votes { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int view { get; set; }

        [DataMember]
        public string eptype { get; set; }

        [DataMember]
        public int epnumber { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<RawFile> files { get; set; }

        public Episode()
        {
        }

        internal static Episode GenerateFromAnimeEpisodeID(NancyContext ctx, int anime_episode_id, int uid, int level)
        {
            Episode ep = new Episode();

            if (anime_episode_id > 0)
            {
                ep = GenerateFromAnimeEpisode(ctx, Repositories.RepoFactory.AnimeEpisode.GetByID(anime_episode_id), uid,
                    level);
            }

            return ep;
        }

        internal static Episode GenerateFromAnimeEpisode(NancyContext ctx, SVR_AnimeEpisode aep, int uid, int level)
        {
            Episode ep = new Episode();
            if (aep != null)
            {
                CL_AnimeEpisode_User cae = aep.GetUserContract(uid);
                if (cae != null)
                {
                    ep.id = aep.AnimeEpisodeID;
                    ep.art = new ArtCollection();
                    ep.name = aep.PlexContract?.Title;
                    ep.summary = aep.PlexContract?.Summary;
                    ep.year = aep.PlexContract?.Year;
                    ep.air = aep.PlexContract?.AirDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    ep.votes = cae.AniDB_Votes;
                    ep.rating = aep.PlexContract?.Rating;
                    ep.userrating = aep.PlexContract?.UserRating;
                    if (double.TryParse(ep.rating, out double rating))
                    {
                        // 0.1 should be the absolute lowest rating
                        if (rating > 10) ep.rating = (rating / 100).ToString(CultureInfo.InvariantCulture);
                    }

                    ep.view = cae.IsWatched() ? 1 : 0;
                    ep.epnumber = cae.EpisodeNumber;
                    ep.eptype = aep.EpisodeTypeEnum.ToString();

                    ep.season = aep.PlexContract?.Season;

                    // until fanart refactor this will be good for start
                    if (aep.PlexContract?.Thumb != null)
                    {
                        ep.art.thumb.Add(new Art()
                        {
                            url = APIHelper.ConstructImageLinkFromRest(ctx, aep.PlexContract?.Thumb),
                            index = 0
                        });
                    }
                    if (aep.PlexContract?.Art != null)
                    {
                        ep.art.fanart.Add(new Art()
                        {
                            url = APIHelper.ConstructImageLinkFromRest(ctx, aep.PlexContract?.Art),
                            index = 0
                        });
                    }

                    if (level > 0)
                    {
                        List<SVR_VideoLocal> vls = aep.GetVideoLocals();
                        if (vls.Count > 0)
                        {
                            ep.files = new List<RawFile>();
                            foreach (SVR_VideoLocal vl in vls)
                            {
                                RawFile file = new RawFile(ctx, vl, (level - 1), uid);
                                ep.files.Add(file);
                            }
                        }
                    }
                }
            }

            return ep;
        }
    }
}