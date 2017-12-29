using System.Globalization;
using System.Xml;
using AniDBAPI;
using Shoko.Models.Enums;

namespace Shoko.Server.AniDB_API.Raws
{
    public class Raw_AniDB_Vote_HTTP : XMLBase
    {
        public int EntityID { get; set; }
        public int VoteValue { get; set; }
        public AniDBVoteType VoteType { get; set; }


        public Raw_AniDB_Vote_HTTP()
        {
            EntityID = -1;
            VoteValue = -1;
            VoteType = AniDBVoteType.Anime;
        }


        public void ProcessAnime(XmlNode node)
        {
            NumberStyles style = NumberStyles.Number;
            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-GB");

            this.VoteType = AniDBVoteType.Anime;
            this.EntityID = int.Parse(node.Attributes["aid"].Value);
            double.TryParse(node.InnerText.Trim(), style, culture, out double val);
            int.TryParse((val * 100).ToString(), out int ival);
            VoteValue = ival;
        }

        public void ProcessAnimeTemp(XmlNode node)
        {
            NumberStyles style = NumberStyles.Number;
            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-GB");

            this.VoteType = AniDBVoteType.AnimeTemp;
            this.EntityID = int.Parse(node.Attributes["aid"].Value);
            double.TryParse(node.InnerText.Trim(), style, culture, out double val);
            int.TryParse((val * 100).ToString(), out int ival);
            VoteValue = ival;
        }

        public void ProcessEpisode(XmlNode node)
        {
            NumberStyles style = NumberStyles.Number;
            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-GB");

            this.VoteType = AniDBVoteType.Episode;
            this.EntityID = int.Parse(node.Attributes["eid"].Value);
            double.TryParse(node.InnerText.Trim(), style, culture, out double val);
            int.TryParse((val * 100).ToString(), out int ival);
            VoteValue = ival;
        }


        public override string ToString()
        {
            return string.Format("AniDB_Vote:: entityID: {0} | voteValue: {1} | voteType: {2}",
                EntityID, VoteValue, VoteType);
        }
    }
}