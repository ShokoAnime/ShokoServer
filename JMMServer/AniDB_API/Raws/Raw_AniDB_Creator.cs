using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;


namespace AniDBAPI
{
	[Serializable]
	public class Raw_AniDB_Creator : XMLBase
	{
		public int CreatorID { get; set; }
        public string CreatorName { get; set; }
        public string PicName { get; set; }
        public string CreatorKanjiName { get; set; }
		public string CreatorDescription{ get; set; }
        public int CreatorType { get; set; }
		public string URLEnglish { get; set; }
		public string URLJapanese { get; set; }
		public string URLWikiEnglish { get; set; }
		public string URLWikiJapanese { get; set; }



        public Raw_AniDB_Creator()
        {
            CreatorName = string.Empty;
            CreatorKanjiName = string.Empty;
            CreatorType = 1;
            PicName = string.Empty;
			CreatorDescription = "";
			URLEnglish = string.Empty;
			URLJapanese = string.Empty;
			URLWikiEnglish = string.Empty;
			URLWikiJapanese = string.Empty;
        }

        
        public readonly static int LastVersion = 1;

		public Raw_AniDB_Creator(string sRecMessage)
		{
			// remove the header info
			string[] sDetails = sRecMessage.Substring(12).Split('|');

			// 245 CREATOR 200|?????|Suwabe Jun`ichi|1|17015.jpg||http://www.haikyo.or.jp/PROFILE/man/11470.html|Junichi_Suwabe|%E8%AB%8F%E8%A8%AA%E9%83%A8%E9%A0%86%E4%B8%80|1236300570
			// 3396|????|Takatsuka Masaya|1|24705.jpg||http://www.aoni.co.jp/actor/ta/takatsuka-masaya.html|Masaya_Takatsuka|????|1271491220

			// 235 CHARACTER
			// 0. 200 ** creator id
			// 1. ??? ** creator name kanji
			// 2. Suwabe Jun`ichi ** creator name
			// 3. 1 ** type values: 1='person', 2='company', 3='collaboration' 
			// 4. 25513.jpg ** pic name
			// 5. ??? ** desc
			// 6. ??? ** URLEnglish
			// 7. ??? ** URLJapanese
			// 8. ??? ** URLWikiEnglish
			// 9. ??? ** URLWikiJapanese

			CreatorID = int.Parse(sDetails[0].Trim());
            CreatorKanjiName = AniDBAPILib.ProcessAniDBString(sDetails[1].Trim());
			CreatorName = AniDBAPILib.ProcessAniDBString(sDetails[2].Trim());
		    CreatorType = AniDBAPILib.ProcessAniDBInt(AniDBAPILib.ProcessAniDBString(sDetails[3].Trim()));
			PicName = AniDBAPILib.ProcessAniDBString(sDetails[4].Trim());
			CreatorDescription = "";
			URLEnglish = AniDBAPILib.ProcessAniDBString(sDetails[5].Trim());
			URLJapanese = AniDBAPILib.ProcessAniDBString(sDetails[6].Trim());
			URLWikiEnglish = AniDBAPILib.ProcessAniDBString(sDetails[7].Trim());
			URLWikiJapanese = AniDBAPILib.ProcessAniDBString(sDetails[8].Trim());
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("creatorID: " + CreatorID.ToString());
			sb.Append(" | creatorName: " + CreatorName);
			sb.Append(" | picName: " + PicName);

			return sb.ToString();
		}
        
	}
}
