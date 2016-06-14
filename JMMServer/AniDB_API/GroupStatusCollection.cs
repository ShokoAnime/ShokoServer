using System;
using System.Collections.Generic;
using System.Text;

namespace AniDBAPI
{
	public class GroupStatusCollection
	{
		private List<Raw_AniDB_GroupStatus> groups = new List<Raw_AniDB_GroupStatus>();
		public List<Raw_AniDB_GroupStatus> Groups
		{
			get { return groups; }
		}

		public List<Raw_AniDB_GroupStatus> GetMissingEpisodes(int latestEpNumber)
		{
			List<Raw_AniDB_GroupStatus> missingGroups = new List<Raw_AniDB_GroupStatus>();

			foreach (Raw_AniDB_GroupStatus grp in groups)
			{
				if (grp.LastEpisodeNumber > latestEpNumber)
					missingGroups.Add(grp);
			}

			return missingGroups;
		}

		public int LatestEpisodeNumber
		{
			get
			{
				int latest = 0;

				foreach (Raw_AniDB_GroupStatus grp in groups)
				{
					if (grp.LastEpisodeNumber > latest)
						latest = grp.LastEpisodeNumber;
				}

				return latest;
			}
		}

		public GroupStatusCollection(int animeID, string sRecMessage)
		{
			/*
			// 225 GROUPSTATUS
			1612|MDAN|1|9|784|2|1-9
			1412|Dattebayo|1|9|677|55|1-9
			6371|Hatsuyuki Fansub|1|9|738|4|1-9
			5900|Shiroi-Fansubs|1|9|645|3|1-9
			6897|Black Ocean Team|1|8|0|0|1-8
			7209|Otaku Trad Team|1|8|0|0|1-8
			5816|ALanime Fansub|1|7|836|2|1-7
			1472|Saikou-BR|1|6|0|0|1-6
			6638|Yuurisan-Subs & Shinsen-Subs|1|5|674|12|1-5
			7624|Desire & Himmel & Inmeliora|1|5|657|15|1-5
			2777|S?ai`No`Naka|1|5|867|1|1-5
			5618|Yuurisan-Subs|1|5|594|4|1-5
			6738|AnimeManganTR|1|5|0|0|1-5
			7673|PA-Fansub|1|4|0|0|1-4
			7512|Anime Brat|1|3|0|0|1-3
			7560|Demon Sub|1|3|0|0|1-3
			6197|Funny and Fantasy subs|1|2|896|1|1-2
			7887|Yaoi Daisuki no Fansub & Sleepless Beauty no Fansub|1|7|0|0|5,7
			7466|Aasasubs Clique|1|1|578|4|1
			7429|Inter-Anime Fansub|1|1|656|1|1
			6358|Aino Fansub|1|8|0|0|8
			6656|Atelier Thryst|1|1|747|13|1
			*/    

			// remove the header info
			string[] sDetails = sRecMessage.Substring(0).Split('\n');

			if (sDetails.Length <= 2) return;

			for (int i=1; i< sDetails.Length -1; i++) // first item will be the status command, and last will be empty
			{
				//BaseConfig.MyAnimeLog.Write("s: {0}", sDetails[i]);

				Raw_AniDB_GroupStatus grp = new Raw_AniDB_GroupStatus();
				grp.AnimeID = animeID;

				try
				{

					// {int group id}|{str group name}|{int completion state}|{int last episode number}|{int rating}|{int votes}|{str episode range}\n
					string[] flds = sDetails[i].Substring(0).Split('|');
					grp.GroupID = int.Parse(flds[0]);
					grp.GroupName = flds[1];
					grp.CompletionState = int.Parse(flds[2]);
					grp.LastEpisodeNumber = int.Parse(flds[3]);
					grp.Rating = int.Parse(flds[4]);
					grp.Votes = int.Parse(flds[5]);
					grp.EpisodeRange = flds[6];
					groups.Add(grp);
				}
				catch (Exception ex)
				{
					NLog.LogManager.GetCurrentClassLogger().ErrorException(ex.ToString(), ex);
				}

				//BaseConfig.MyAnimeLog.Write("grp: {0}", grp);
			}
		}
	}
}
