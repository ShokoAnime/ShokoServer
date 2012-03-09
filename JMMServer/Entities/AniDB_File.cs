using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using NLog;
using JMMServer.Repositories;
using AniDBAPI;
using JMMServer.WebCache;

namespace JMMServer.Entities
{
	public class AniDB_File
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		#region DB columns
		public int AniDB_FileID { get; private set; }
		public int FileID { get; set; }
		public string Hash { get; set; }
		public int AnimeID { get; set; }
		public int GroupID { get; set; }
		public string File_Source { get; set; }
		public string File_AudioCodec { get; set; }
		public string File_VideoCodec { get; set; }
		public string File_VideoResolution { get; set; }
		public string File_FileExtension { get; set; }
		public int File_LengthSeconds { get; set; }
		public string File_Description { get; set; }
		public int File_ReleaseDate { get; set; }
		public string Anime_GroupName { get; set; }
		public string Anime_GroupNameShort { get; set; }
		public int Episode_Rating { get; set; }
		public int Episode_Votes { get; set; }
		public DateTime DateTimeUpdated { get; set; }
		public int IsWatched { get; set; }
		public DateTime? WatchedDate { get; set; }
		public string CRC { get; set; }
		public string MD5 { get; set; }
		public string SHA1 { get; set; }
		public string FileName { get; set; }
		public long FileSize { get; set; }
		#endregion


		private string subtitlesRAW;
		private string languagesRAW;
		private string episodesRAW;
		private string episodesPercentRAW;


		[XmlIgnore]
		public List<Language> Languages
		{
			get
			{
				List<Language> lans = new List<Language>();

				CrossRef_Languages_AniDB_FileRepository repFileLanguages = new CrossRef_Languages_AniDB_FileRepository();
				LanguageRepository repLanguages = new LanguageRepository();

				List<CrossRef_Languages_AniDB_File> fileLanguages = repFileLanguages.GetByFileID(this.FileID);

				foreach (CrossRef_Languages_AniDB_File crossref in fileLanguages)
				{
					Language lan = repLanguages.GetByID(crossref.LanguageID);
					if (lan != null) lans.Add(lan);
				}
				return lans;
			}
		}


		[XmlIgnore]
		public List<Language> Subtitles
		{
			get
			{
				List<Language> subs = new List<Language>();

				CrossRef_Subtitles_AniDB_FileRepository repFileSubtitles = new CrossRef_Subtitles_AniDB_FileRepository();
				LanguageRepository repLanguages = new LanguageRepository();

				List<CrossRef_Subtitles_AniDB_File> fileSubtitles = repFileSubtitles.GetByFileID(this.FileID);


				foreach (CrossRef_Subtitles_AniDB_File crossref in fileSubtitles)
				{
					Language sub = repLanguages.GetByID(crossref.LanguageID);
					if (sub != null) subs.Add(sub);
				}
				return subs;

			}
		}

		[XmlIgnore]
		public List<int> EpisodeIDs
		{
			get
			{
				List<int> ids = new List<int>();

				CrossRef_File_EpisodeRepository repFileEps = new CrossRef_File_EpisodeRepository();
				List<CrossRef_File_Episode> fileEps = repFileEps.GetByHash(this.Hash);

				foreach (CrossRef_File_Episode crossref in fileEps)
				{
					ids.Add(crossref.EpisodeID);
				}
				return ids;
			}
		}

		[XmlIgnore]
		public List<AniDB_Episode> Episodes
		{
			get
			{
				List<AniDB_Episode> eps = new List<AniDB_Episode>();

				CrossRef_File_EpisodeRepository repFileEps = new CrossRef_File_EpisodeRepository();
				List<CrossRef_File_Episode> fileEps = repFileEps.GetByHash(this.Hash);

				foreach (CrossRef_File_Episode crossref in fileEps)
				{
					if (crossref.Episode != null) eps.Add(crossref.Episode);
				}
				return eps;
			}
		}

		[XmlIgnore]
		public List<CrossRef_File_Episode> EpisodeCrossRefs
		{
			get
			{
				CrossRef_File_EpisodeRepository repFileEps = new CrossRef_File_EpisodeRepository();
				return repFileEps.GetByHash(this.Hash);
			}
		}



		public string SubtitlesRAW
		{
			get
			{
				if (!string.IsNullOrEmpty(subtitlesRAW))
					return subtitlesRAW;
				string ret = "";
				foreach (Language lang in this.Subtitles)
				{
					if (ret.Length > 0)
						ret += ",";
					ret += lang.LanguageName;
				}
				return ret;
			}
			set
			{
				subtitlesRAW = value;
			}
		}


		public string LanguagesRAW
		{
			get
			{
				if (!string.IsNullOrEmpty(languagesRAW))
					return languagesRAW;
				string ret = "";
				foreach (Language lang in this.Languages)
				{
					if (ret.Length > 0)
						ret += ",";
					ret += lang.LanguageName;
				}
				return ret;
			}
			set
			{
				languagesRAW = value;
			}
		}


		public string EpisodesRAW
		{
			get
			{
				if (!string.IsNullOrEmpty(episodesRAW))
					return episodesRAW;
				string ret = "";
				foreach (CrossRef_File_Episode cross in EpisodeCrossRefs)
				{
					if (ret.Length > 0)
						ret += ", ";
					ret += cross.EpisodeID.ToString();
				}
				return ret;
			}
			set
			{
				episodesRAW = value;
			}
		}


		public string EpisodesPercentRAW
		{
			get
			{
				if (!string.IsNullOrEmpty(episodesPercentRAW))
					return episodesPercentRAW;
				string ret = "";
				foreach (CrossRef_File_Episode cross in EpisodeCrossRefs)
				{
					if (ret.Length > 0)
						ret += ", ";
					ret += cross.Percentage.ToString();
				}
				return ret;
			}
			set
			{
				episodesPercentRAW = value;
			}
		}

		public string SubtitlesRAWForWebCache
		{
			get
			{
				char apostrophe = ("'").ToCharArray()[0];

				if (!string.IsNullOrEmpty(subtitlesRAW))
					return subtitlesRAW;
				string ret = "";
				foreach (Language lang in this.Subtitles)
				{
					if (ret.Length > 0)
						ret += apostrophe;
					ret += lang.LanguageName;
				}
				return ret;
			}
			set
			{
				subtitlesRAW = value;
			}
		}


		public string LanguagesRAWForWebCache
		{
			get
			{
				char apostrophe = ("'").ToCharArray()[0];

				if (!string.IsNullOrEmpty(languagesRAW))
					return languagesRAW;
				string ret = "";
				foreach (Language lang in this.Languages)
				{
					if (ret.Length > 0)
						ret += apostrophe;
					ret += lang.LanguageName;
				}
				return ret;
			}
			set
			{
				languagesRAW = value;
			}
		}


		public string EpisodesRAWForWebCache
		{
			get
			{
				char apostrophe = ("'").ToCharArray()[0];

				if (!string.IsNullOrEmpty(episodesRAW))
					return episodesRAW;
				string ret = "";
				foreach (CrossRef_File_Episode cross in EpisodeCrossRefs)
				{
					if (ret.Length > 0)
						ret += apostrophe;
					ret += cross.EpisodeID.ToString();
				}
				return ret;
			}
			set
			{
				episodesRAW = value;
			}
		}


		public string EpisodesPercentRAWForWebCache
		{
			get
			{
				char apostrophe = ("'").ToCharArray()[0];

				if (!string.IsNullOrEmpty(episodesPercentRAW))
					return episodesPercentRAW;
				string ret = "";
				foreach (CrossRef_File_Episode cross in EpisodeCrossRefs)
				{
					if (ret.Length > 0)
						ret += apostrophe;
					ret += cross.Percentage.ToString();
				}
				return ret;
			}
			set
			{
				episodesPercentRAW = value;
			}
		}


		public void Populate(Raw_AniDB_File fileInfo)
		{
			this.Anime_GroupName = fileInfo.Anime_GroupName;
			this.Anime_GroupNameShort = fileInfo.Anime_GroupNameShort;
			this.AnimeID = fileInfo.AnimeID;
			this.CRC = fileInfo.CRC;
			this.DateTimeUpdated = DateTime.Now;
			this.Episode_Rating = fileInfo.Episode_Rating;
			this.Episode_Votes = fileInfo.Episode_Votes;
			this.File_AudioCodec = fileInfo.File_AudioCodec;
			this.File_Description = fileInfo.File_Description;
			this.File_FileExtension = fileInfo.File_FileExtension;
			this.File_LengthSeconds = fileInfo.File_LengthSeconds;
			this.File_ReleaseDate = fileInfo.File_ReleaseDate;
			this.File_Source = fileInfo.File_Source;
			this.File_VideoCodec = fileInfo.File_VideoCodec;
			this.File_VideoResolution = fileInfo.File_VideoResolution;
			this.FileID = fileInfo.FileID;
			this.FileName = fileInfo.FileName;
			this.FileSize = fileInfo.FileSize;
			this.GroupID = fileInfo.GroupID;
			this.Hash = fileInfo.ED2KHash;
			this.IsWatched = fileInfo.IsWatched;
			this.MD5 = fileInfo.MD5;
			this.SHA1 = fileInfo.SHA1;

			this.languagesRAW = fileInfo.LanguagesRAW;
			this.subtitlesRAW = fileInfo.SubtitlesRAW;
			this.episodesPercentRAW = fileInfo.EpisodesPercentRAW;
			this.episodesRAW = fileInfo.EpisodesRAW;
		}

		public void Populate(AniDB_FileRequest fileInfo)
		{
			this.Anime_GroupName = fileInfo.Anime_GroupName;
			this.Anime_GroupNameShort = fileInfo.Anime_GroupNameShort;
			this.AnimeID = fileInfo.AnimeID;
			this.CRC = fileInfo.CRC;
			this.DateTimeUpdated = DateTime.Now;
			this.Episode_Rating = fileInfo.Episode_Rating;
			this.Episode_Votes = fileInfo.Episode_Votes;
			this.File_AudioCodec = fileInfo.File_AudioCodec;
			this.File_Description = fileInfo.File_Description;
			this.File_FileExtension = fileInfo.File_FileExtension;
			this.File_LengthSeconds = fileInfo.File_LengthSeconds;
			this.File_ReleaseDate = fileInfo.File_ReleaseDate;
			this.File_Source = fileInfo.File_Source;
			this.File_VideoCodec = fileInfo.File_VideoCodec;
			this.File_VideoResolution = fileInfo.File_VideoResolution;
			this.FileID = fileInfo.FileID;
			this.FileName = fileInfo.FileName;
			this.FileSize = fileInfo.FileSize;
			this.GroupID = fileInfo.GroupID;
			this.Hash = fileInfo.Hash;
			this.MD5 = fileInfo.MD5;
			this.SHA1 = fileInfo.SHA1;

			this.languagesRAW = fileInfo.LanguagesRAW;
			this.subtitlesRAW = fileInfo.SubtitlesRAW;
			this.episodesPercentRAW = fileInfo.EpisodesPercentRAW;
			this.episodesRAW = fileInfo.EpisodesRAW;
		}

		public void CreateLanguages()
		{
			char apostrophe = ("'").ToCharArray()[0];

			LanguageRepository repLanguages = new LanguageRepository();

			if (languagesRAW != null) //Only create relations if the origin of the data if from Raw (WebService/AniDB)
			{
				if (languagesRAW.Trim().Length == 0) return;
				// Delete old if changed

				CrossRef_Languages_AniDB_FileRepository repFileLanguages = new CrossRef_Languages_AniDB_FileRepository();
				

				List<CrossRef_Languages_AniDB_File> fileLanguages = repFileLanguages.GetByFileID(this.FileID);
				foreach (CrossRef_Languages_AniDB_File fLan in fileLanguages)
				{
					repFileLanguages.Delete(fLan.CrossRef_Languages_AniDB_FileID);
				}
	

				string[] langs = languagesRAW.Split(apostrophe);
				foreach (string language in langs)
				{
					string rlan = language.Trim().ToLower();
					if (rlan.Length > 0)
					{
						Language lan = repLanguages.GetByLanguageName(rlan);
						if (lan == null)
						{
							lan = new Language();
							lan.LanguageName = rlan;
							repLanguages.Save(lan);
						}
						CrossRef_Languages_AniDB_File cross = new CrossRef_Languages_AniDB_File();
						cross.LanguageID = lan.LanguageID;
						cross.FileID = FileID;
						repFileLanguages.Save(cross);
					}
				}
			}

			if (subtitlesRAW != null)
			{
				if (subtitlesRAW.Trim().Length == 0) return;

				// Delete old if changed
				CrossRef_Subtitles_AniDB_FileRepository repFileSubtitles = new CrossRef_Subtitles_AniDB_FileRepository();
				List<CrossRef_Subtitles_AniDB_File> fileSubtitles = repFileSubtitles.GetByFileID(this.FileID);

				foreach (CrossRef_Subtitles_AniDB_File fSub in fileSubtitles)
				{
					repFileSubtitles.Delete(fSub.CrossRef_Subtitles_AniDB_FileID);
				}

				string[] subs = subtitlesRAW.Split(apostrophe);
				foreach (string language in subs)
				{
					string rlan = language.Trim().ToLower();
					if (rlan.Length > 0)
					{
						Language lan = repLanguages.GetByLanguageName(rlan);
						if (lan == null)
						{
							lan = new Language();
							lan.LanguageName = rlan;
							repLanguages.Save(lan);
						}
						CrossRef_Subtitles_AniDB_File cross = new CrossRef_Subtitles_AniDB_File();
						cross.LanguageID = lan.LanguageID;
						cross.FileID = FileID;
						repFileSubtitles.Save(cross);
					}
				}
			}
		}

		public void CreateCrossEpisodes(string localFileName)
		{
			if (episodesRAW != null) //Only create relations if the origin of the data if from Raw (AniDB)
			{
				CrossRef_File_EpisodeRepository repFileEpisodes = new CrossRef_File_EpisodeRepository();
				List<CrossRef_File_Episode> fileEps = repFileEpisodes.GetByHash(this.Hash);

				foreach (CrossRef_File_Episode fileEp in fileEps)
				{
					// only delete cross refs from AniDB, not manual associations by the user
					if (fileEp.CrossRefSource == (int)CrossRefSource.AniDB)
						repFileEpisodes.Delete(fileEp.CrossRef_File_EpisodeID);
				}

				char apostrophe = ("'").ToCharArray()[0];
				char epiSplit = ',';
				if (episodesRAW.Contains(apostrophe))
					epiSplit = apostrophe;

				char eppSplit = ',';
				if (episodesPercentRAW.Contains(apostrophe))
					eppSplit = apostrophe;

				string[] epi = episodesRAW.Split(epiSplit);
				string[] epp = episodesPercentRAW.Split(eppSplit);
				for (int x = 0; x < epi.Length; x++)
				{

					string epis = epi[x].Trim();
					string epps = epp[x].Trim();
					if (epis.Length > 0)
					{
						int epid = 0;
						int.TryParse(epis, out epid);
						int eppp = 100;
						int.TryParse(epps, out eppp);
						if (epid != 0)
						{
							CrossRef_File_Episode cross = new CrossRef_File_Episode();
							cross.Hash = Hash;
							cross.CrossRefSource = (int)CrossRefSource.AniDB;
							cross.AnimeID = this.AnimeID;
							cross.EpisodeID = epid;
							cross.Percentage = eppp;
							cross.EpisodeOrder = x + 1;
							cross.FileName = localFileName;
							cross.FileSize = FileSize;
							repFileEpisodes.Save(cross);
						}
					}
				}
			}
		}

		public string ToXML()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(@"<AniDB_File>");
			sb.Append(string.Format("<ED2KHash>{0}</ED2KHash>", Hash));
			sb.Append(string.Format("<Hash>{0}</Hash>", Hash));
			sb.Append(string.Format("<CRC>{0}</CRC>", CRC));
			sb.Append(string.Format("<MD5>{0}</MD5>", MD5));
			sb.Append(string.Format("<SHA1>{0}</SHA1>", SHA1));
			sb.Append(string.Format("<FileID>{0}</FileID>", FileID));
			sb.Append(string.Format("<AnimeID>{0}</AnimeID>", AnimeID));
			sb.Append(string.Format("<GroupID>{0}</GroupID>", GroupID));
			sb.Append(string.Format("<File_LengthSeconds>{0}</File_LengthSeconds>", File_LengthSeconds));
			sb.Append(string.Format("<File_Source>{0}</File_Source>", File_Source));
			sb.Append(string.Format("<File_AudioCodec>{0}</File_AudioCodec>", File_AudioCodec));
			sb.Append(string.Format("<File_VideoCodec>{0}</File_VideoCodec>", File_VideoCodec));
			sb.Append(string.Format("<File_VideoResolution>{0}</File_VideoResolution>", File_VideoResolution));
			sb.Append(string.Format("<File_FileExtension>{0}</File_FileExtension>", File_FileExtension));
			sb.Append(string.Format("<File_Description>{0}</File_Description>", File_Description));
			sb.Append(string.Format("<FileName>{0}</FileName>", FileName));
			sb.Append(string.Format("<File_ReleaseDate>{0}</File_ReleaseDate>", File_ReleaseDate));
			sb.Append(string.Format("<Anime_GroupName>{0}</Anime_GroupName>", Anime_GroupName));
			sb.Append(string.Format("<Anime_GroupNameShort>{0}</Anime_GroupNameShort>", Anime_GroupNameShort));
			sb.Append(string.Format("<Episode_Rating>{0}</Episode_Rating>", Episode_Rating));
			sb.Append(string.Format("<Episode_Votes>{0}</Episode_Votes>", Episode_Votes));
			sb.Append(string.Format("<DateTimeUpdated>{0}</DateTimeUpdated>", DateTimeUpdated));
			sb.Append(string.Format("<EpisodesRAW>{0}</EpisodesRAW>", EpisodesRAW));
			sb.Append(string.Format("<SubtitlesRAW>{0}</SubtitlesRAW>", SubtitlesRAW));
			sb.Append(string.Format("<LanguagesRAW>{0}</LanguagesRAW>", LanguagesRAW));
			sb.Append(string.Format("<EpisodesPercentRAW>{0}</EpisodesPercentRAW>", EpisodesPercentRAW));
			sb.Append(@"</AniDB_File>");

			return sb.ToString();
		}
	}
}
