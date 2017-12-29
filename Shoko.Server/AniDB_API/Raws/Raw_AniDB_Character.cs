using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace AniDBAPI
{
    [Serializable]
    public class Raw_AniDB_Character : XMLBase
    {
        public int CharID { get; set; }
        public int AnimeID { get; set; }
        public string PicName { get; set; }
        public string CharName { get; set; }
        public string CharKanjiName { get; set; }
        public string CharDescription { get; set; }
        public string CharType { get; set; }

        public string CreatorListRaw { get; set; }
        public string EpisodeListRaw { get; set; }

        public List<Raw_AniDB_Seiyuu> Seiyuus { get; set; }

        public Raw_AniDB_Character()
        {
            InitFields();
        }

        private void InitFields()
        {
            CharID = 0;
            CharName = string.Empty;
            CharKanjiName = string.Empty;
            CharDescription = string.Empty;
            CharType = string.Empty;
            PicName = string.Empty;

            CreatorListRaw = string.Empty;
            EpisodeListRaw = string.Empty;

            Seiyuus = new List<Raw_AniDB_Seiyuu>();
        }


        /// <summary>
        /// From UDP API
        /// NO LONGER USED
        /// </summary>
        /// <param name="sRecMessage"></param>
        public Raw_AniDB_Character(string sRecMessage)
        {
            InitFields();

            this.AnimeID = 0;

            // remove the header info
            string[] sDetails = sRecMessage.Substring(14).Split('|');

            //BaseConfig.MyAnimeLog.Write("PROCESSING EPISODE: {0}", sDetails.Length);
            // 235 CHARACTER 7179|?????|Noyamano Shiraume|25513.jpg|4196,3,3875,1||1243176147


            // 235 CHARACTER
            // 0. 7179 ** char id
            // 1. ??? ** char name kanji
            // 2. Noyamano Shiraume ** char name
            // 3. 25513.jpg ** pic name
            // 4  4196,3,3875,1 ** An 'anime block' is {int anime id},{int type},{int creatorid},{boolean is_main_seiyuu} repeated as many times as necessary, separated by a single quote ( ' ).

            CharID = int.Parse(sDetails[0].Trim());
            CharKanjiName = AniDBAPILib.ProcessAniDBString(sDetails[1].Trim());
            CharName = AniDBAPILib.ProcessAniDBString(sDetails[2].Trim());
            PicName = AniDBAPILib.ProcessAniDBString(sDetails[3].Trim());
            CreatorListRaw = AniDBAPILib.ProcessAniDBString(sDetails[4].Trim().Replace("'", "|"));
        }

        public void ProcessFromHTTPResult(XmlNode node, int anid)
        {
            InitFields();

            this.AnimeID = anid;
            this.CharID = int.Parse(AniDBHTTPHelper.TryGetAttribute(node, "id"));
            this.CharType = AniDBHTTPHelper.TryGetAttribute(node, "type");

            this.CharName = AniDBHTTPHelper.TryGetProperty(node, "name")?.Replace('`', '\'');
            this.CharDescription = AniDBHTTPHelper.TryGetProperty(node, "description")?.Replace('`', '\'');
            this.EpisodeListRaw = AniDBHTTPHelper.TryGetProperty(node, "episodes") ?? string.Empty;
            this.PicName = AniDBHTTPHelper.TryGetProperty(node, "picture");

            CreatorListRaw = string.Empty;
            foreach (XmlNode nodeChild in node.ChildNodes)
            {
                if (nodeChild?.Name == "seiyuu")
                {
                    Raw_AniDB_Seiyuu seiyuu = new Raw_AniDB_Seiyuu();

                    if (nodeChild.Attributes?["id"] != null)
                    {
                        string creatorid = nodeChild.Attributes["id"].Value;
                        seiyuu.SeiyuuID = int.Parse(creatorid);

                        if (CreatorListRaw.Length > 0)
                            CreatorListRaw += ",";
                        CreatorListRaw += creatorid.Trim();
                    }

                    if (nodeChild.Attributes?["picture"] != null)
                        seiyuu.PicName = nodeChild.Attributes["picture"].Value;

                    seiyuu.SeiyuuName = nodeChild.InnerText.Replace('`', '\'');
                    Seiyuus.Add(seiyuu);
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("charID: " + CharID.ToString());
            sb.Append(" | characterName: " + CharName);
            sb.Append(" | picName: " + PicName);
            return sb.ToString();
        }
    }
}