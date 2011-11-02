using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using System.IO;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_GetCharacter : CommandRequestImplementation, ICommandRequest
	{
		public int CharID { get; set; }
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Getting character info from UDP API: {0}", CharID);
			}
		}

		public CommandRequest_GetCharacter()
		{
		}

		public CommandRequest_GetCharacter(int charid, bool forced)
		{
			this.CharID = charid;
			this.ForceRefresh = forced;
			this.CommandType = (int)CommandRequestType.AniDB_GetCharacter;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_GetCharacter: {0}", CharID);

			try
			{
				AniDB_CharacterRepository repChar = new AniDB_CharacterRepository();
				AniDB_Character chr = repChar.GetByCharID(CharID);

				if (ForceRefresh || chr == null)
				{
					// redownload anime details from http ap so we can get an update character list
					chr = JMMService.AnidbProcessor.GetCharacterInfoUDP(CharID);
				}

				if (chr != null || !string.IsNullOrEmpty(chr.PosterPath) && !File.Exists(chr.PosterPath))
				{
					CommandRequest_DownloadImage cmd = new CommandRequest_DownloadImage(chr.AniDB_CharacterID, JMMImageType.AniDB_Character, false);
					cmd.Save();
				}

			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_GetCharacter: {0} - {1}", CharID, ex.ToString());
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_GetCharacter_{0}", this.CharID);
		}

		public override bool LoadFromDBCommand(CommandRequest cq)
		{
			this.CommandID = cq.CommandID;
			this.CommandRequestID = cq.CommandRequestID;
			this.CommandType = cq.CommandType;
			this.Priority = cq.Priority;
			this.CommandDetails = cq.CommandDetails;
			this.DateTimeUpdated = cq.DateTimeUpdated;

			// read xml to get parameters
			if (this.CommandDetails.Trim().Length > 0)
			{
				XmlDocument docCreator = new XmlDocument();
				docCreator.LoadXml(this.CommandDetails);

				// populate the fields
				this.CharID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetCharacter", "CharID"));
				this.ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetCharacter", "ForceRefresh"));
			}

			return true;
		}

		public override CommandRequest ToDatabaseObject()
		{
			GenerateCommandID();

			CommandRequest cq = new CommandRequest();
			cq.CommandID = this.CommandID;
			cq.CommandType = this.CommandType;
			cq.Priority = this.Priority;
			cq.CommandDetails = this.ToXML();
			cq.DateTimeUpdated = DateTime.Now;

			return cq;
		}
	}
}
