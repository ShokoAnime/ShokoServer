using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using JMMDatabase;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_GetReleaseGroup : BaseCommandRequest, ICommandRequest
	{
		public int GroupID { get; set; }
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return $"Getting release group info from UDP API: {GroupID}";
			}
		}

		public CommandRequest_GetReleaseGroup()
		{
		}

		public CommandRequest_GetReleaseGroup(int grpid, bool forced)
		{
			this.GroupID = grpid;
			this.ForceRefresh = forced;
			this.CommandType = CommandRequestType.AniDB_GetReleaseGroup;
			this.Priority = DefaultPriority;
            this.JMMUserId= Store.JmmUserRepo.GetMasterUser().Id;
            this.Id= $"CommandRequest_GetReleaseGroup_{this.GroupID}";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_GetReleaseGroup: {0}", GroupID);

			try
			{
                JMMModels.AniDB_ReleaseGroup relGroup = Store.ReleaseGroupRepo.Find(GroupID.ToString());

				if (ForceRefresh || relGroup == null)
				{
					// redownload anime details from http ap so we can get an update character list
					JMMService.AnidbProcessor.GetReleaseGroupUDP(JMMUserId, GroupID);
				}

			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_GetReleaseGroup: {0} - {1}", GroupID, ex.ToString());
				return;
			}
		}
	}
}
