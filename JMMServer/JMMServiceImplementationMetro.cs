using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Repositories;
using JMMServer.Entities;
using NLog;

namespace JMMServer
{
	public class JMMServiceImplementationMetro : IJMMServerMetro
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public List<Contract_AnimeGroup> GetAllGroups(int userID)
		{
			List<Contract_AnimeGroup> grps = new List<Contract_AnimeGroup>();
			try
			{
				AnimeGroupRepository repGroups = new AnimeGroupRepository();
				AnimeGroup_UserRepository repUserGroups = new AnimeGroup_UserRepository();

				List<AnimeGroup> allGrps = repGroups.GetAll();

				// user records
				AnimeGroup_UserRepository repGroupUser = new AnimeGroup_UserRepository();
				List<AnimeGroup_User> userRecordList = repGroupUser.GetByUserID(userID);
				Dictionary<int, AnimeGroup_User> dictUserRecords = new Dictionary<int, AnimeGroup_User>();
				foreach (AnimeGroup_User grpUser in userRecordList)
					dictUserRecords[grpUser.AnimeGroupID] = grpUser;

				foreach (AnimeGroup ag in allGrps)
				{
					AnimeGroup_User userRec = null;
					if (dictUserRecords.ContainsKey(ag.AnimeGroupID))
						userRec = dictUserRecords[ag.AnimeGroupID];

					// calculate stats
					Contract_AnimeGroup contract = ag.ToContract(userRec);
					contract.ServerPosterPath = ag.PosterPathNoBlanks;
					grps.Add(contract);
				}

				grps.Sort();

			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
			}
			return grps;
		}
	}
}
