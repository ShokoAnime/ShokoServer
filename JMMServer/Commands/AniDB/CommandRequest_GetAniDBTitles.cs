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
	public class CommandRequest_GetAniDBTitles : CommandRequestImplementation, ICommandRequest
	{
		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority10; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Getting AniDB Titles from HTTP API");
			}
		}

		public CommandRequest_GetAniDBTitles()
		{
		}

		public CommandRequest_GetAniDBTitles(string hash, bool watched)
		{
			this.CommandType = (int)CommandRequestType.AniDB_GetTitles;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_GetAniDBTitles");

			
			try
			{
				string url = Constants.AniDBTitlesURL;
				logger.Trace("Get AniDB Titles: {0}", url);

				Stream s = Utils.DownloadWebBinary(url);
				int bytes = 2048;
				byte[] data = new byte[2048];
				StringBuilder b = new StringBuilder();
				UTF8Encoding enc = new UTF8Encoding();

				ICSharpCode.SharpZipLib.GZip.GZipInputStream zis = new ICSharpCode.SharpZipLib.GZip.GZipInputStream(s);

				while ((bytes = zis.Read(data, 0, data.Length)) > 0)
					b.Append(enc.GetString(data, 0, bytes));

				zis.Close();

				AniDB_Anime_TitleRepository repTitles = new AniDB_Anime_TitleRepository();
				



				/*string[] lines = b.ToString().Split('\n');
				Dictionary<int, AniDB_Anime_Title> titles = new Dictionary<int, AniDB_Anime_Title>();
				foreach (string line in lines)
				{
					if (line.Trim().Length == 0 || line.Trim().Substring(0, 1) == "#") continue;

					string[] fields = line.Split('|');

					int animeID = 0;
					int.TryParse(fields[0], out animeID);
					if (animeID == 0) continue;

					string titleType = fields[1].Trim().ToLower();
					string language = fields[2].Trim().ToLower();
					string titleValue = fields[3].Trim();

					List<AniDB_Anime_Title> existingtitles = repTitles.GetByAnimeIDLanguageTypeValue(animeID, language, titleType, titleValue);
					if (existingtitles.Count == 0)
					{
					}

					foreach (AniDB_Anime_Title animetitle in existingtitles)
					{
						if (animetitle.Title != titleValue)
						{
							animetitle.Title = titleValue;
							repTitles.Save(animetitle);
						}
					}


					AniDB_Title thisTitle = null;
					if (titles.ContainsKey(animeID))
					{
						thisTitle = titles[animeID];
					}
					else
					{
						thisTitle = new AniDB_Title();
						thisTitle.AnimeID = animeID;
					}

					if (titleType == 1 || titleType == 4)
					{
						if (language == "EN") thisTitle.EnglishName = titleValue;
						if (language == "X-JAT") thisTitle.RomajiName = titleValue;
					}

					if (titleType == 2) thisTitle.Synonyms.Add(titleValue);
					if (titleType == 3) thisTitle.ShortTitles.Add(titleValue);

					titles[animeID] = thisTitle;
				}

				foreach (AniDB_Title aniTitle in titles.Values)
				{
					AniDB_Anime anime = new AniDB_Anime();
					if (!anime.Load(aniTitle.AnimeID))
					{
						anime.AnimeID = aniTitle.AnimeID;

						// populate with blank values instead of nulls
						anime.AnimeNfoID = "";
						anime.AnimeType = -1;
						anime.AwardList = "";
						anime.CharacterIDListRAW = "";
						anime.DateRecordUpdated = "";
						anime.DateTimeUpdated = DateTime.Now.AddDays(-20); // we do this so it is not excluded from updates
						anime.Description = "";
						anime.GenreRAW = "";
						anime.ImageEnabled = 1;
						anime.OtherName = "";
						anime.Picname = "";
						anime.RelatedAnimeIdsRAW = "";
						anime.RelatedAnimeTypesRAW = "";
						anime.ReviewIDListRAW = "";
						anime.KanjiName = "";
						anime.URL = "";
					}
					anime.RomajiName = aniTitle.RomajiName;
					anime.EnglishName = aniTitle.EnglishName;
					anime.Synonyms = aniTitle.SynonymDBList;
					anime.ShortNames = aniTitle.ShortTitlesDBList;

					anime.Save(true, false, false);
				}*/

				
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_GetAniDBTitles: {0}", ex.ToString());
				return;
			}
		}

		/// <summary>
		/// This should generate a unique key for a command
		/// It will be used to check whether the command has already been queued before adding it
		/// </summary>
		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_GetAniDBTitles");
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
