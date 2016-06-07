using System.Globalization;
using System.Xml;
using AniDBAPI;

namespace JMMServer.AniDB_API.Raws
{
    public class Raw_AniDB_Vote_HTTP : XMLBase
    {
        public Raw_AniDB_Vote_HTTP()
        {
            EntityID = -1;
            VoteValue = -1;
            VoteType = enAniDBVoteType.Anime;
        }

        public int EntityID { get; set; }
        public int VoteValue { get; set; }
        public enAniDBVoteType VoteType { get; set; }


        public void ProcessAnime(XmlNode node)
        {
            var style = NumberStyles.Number;
            var culture = CultureInfo.CreateSpecificCulture("en-GB");

            VoteType = enAniDBVoteType.Anime;
            EntityID = int.Parse(node.Attributes["aid"].Value);
            double val = 0;
            double.TryParse(node.InnerText.Trim(), style, culture, out val);
            var ival = 0;
            int.TryParse((val * 100).ToString(), out ival);
            VoteValue = ival;
        }

        public void ProcessAnimeTemp(XmlNode node)
        {
            var style = NumberStyles.Number;
            var culture = CultureInfo.CreateSpecificCulture("en-GB");

            VoteType = enAniDBVoteType.AnimeTemp;
            EntityID = int.Parse(node.Attributes["aid"].Value);
            double val = 0;
            double.TryParse(node.InnerText.Trim(), style, culture, out val);
            var ival = 0;
            int.TryParse((val * 100).ToString(), out ival);
            VoteValue = ival;
        }

        public void ProcessEpisode(XmlNode node)
        {
            var style = NumberStyles.Number;
            var culture = CultureInfo.CreateSpecificCulture("en-GB");

            VoteType = enAniDBVoteType.Episode;
            EntityID = int.Parse(node.Attributes["eid"].Value);
            double val = 0;
            double.TryParse(node.InnerText.Trim(), style, culture, out val);
            var ival = 0;
            int.TryParse((val * 100).ToString(), out ival);
            VoteValue = ival;
        }


        public override string ToString()
        {
            return string.Format("AniDB_Vote:: entityID: {0} | voteValue: {1} | voteType: {2}",
                EntityID, VoteValue, VoteType);
        }
    }
}