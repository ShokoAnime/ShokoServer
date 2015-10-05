using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using System.IO;
using JMMServer.Repositories;
using JMMContracts;
using JMMFileHelper;
using AniDBAPI;
using System.Xml;
using JMMDatabase;
using JMMModels;
using JMMModels.Childs;
using JMMServer.WebCache;
using JMMServer.Commands.AniDB;
using JMMServerModels.DB.Childs;
using Raven.Client;
using AniDB_File = JMMServer.Entities.AniDB_File;
using VideoLocal = JMMModels.VideoLocal;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_GetFile : BaseCommandRequest, ICommandRequest
	{
		public string HashAndSize { get; set; }
		public bool ForceAniDB { get; set; }


		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority3; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Getting file info from UDP API: {0}", Hash);
			}
		}

		public CommandRequest_GetFile()
		{
		}

		public CommandRequest_GetFile(string hashandsize, bool forceAniDB)
		{
		    this.HashAndSize = hashandsize;
			this.ForceAniDB = forceAniDB;
		    this.JMMUserId = Store.JmmUserRepo.GetMasterUser().Id;
			this.CommandType = CommandRequestType.AniDB_GetFileUDP;
			this.Priority = DefaultPriority;
		    this.Id=$"CommandRequest_GetFile_{HashAndSize}";
		}

		public override void ProcessCommand()
		{
			logger.Info("Get AniDB file info: {0}", HashAndSize);

			
			try
			{
			    VideoLocal s=Store.VideoLocalRepo.Find(HashAndSize);
			    if (s == null)  return;
			    JMMModels.AniDB_File aniFile = Store.AniDB_FileRepo.Find(HashAndSize);
                Raw_AniDB_File fileInfo = null;
                if (aniFile == null || ForceAniDB)
                {
                    fileInfo = JMMService.AnidbProcessor.GetFileInfo(JMMUserId, new Hash { ED2KHash = s.Hash, FileSize = s.FileSize, Info = s.FileInfo.Path });
                }
			    if (fileInfo != null)
			    {
			        if (aniFile==null)
                        aniFile=new JMMModels.AniDB_File();
                    fileInfo.PopulateAniDBFile(JMMUserId, aniFile);
			        using (IDocumentSession session = Store.GetSession())
			        {
                        foreach (AniDB_File_Episode f in aniFile.Episodes)
                        {
                            CommandRequest_GetEpisode cmdEp = new CommandRequest_GetEpisode(int.Parse(f.AniDBEpisodeId));
                            cmdEp.Save(session);
                        }
                        Store.AniDB_FileRepo.Save(aniFile,session);
                    }
				}
				
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_GetFile: {0} - {1}", HashAndSize, ex.ToString());
				return;
			}
		}
    }
}
