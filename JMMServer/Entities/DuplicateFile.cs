using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using System.IO;
using JMMContracts;

namespace JMMServer.Entities
{
	public class DuplicateFile
	{
		public int DuplicateFileID { get; private set; }
		public string FilePathFile1 { get; set; }
		public string FilePathFile2 { get; set; }
		public string Hash { get; set; }
		public int ImportFolderIDFile1 { get; set; }
		public int ImportFolderIDFile2 { get; set; }
		public DateTime DateTimeUpdated { get; set; }

		public override string ToString()
		{
			return string.Format("{0} --- {1}", FilePathFile1, FilePathFile2);
		}

		public ImportFolder ImportFolder1
		{
			get
			{
				ImportFolderRepository repNS = new ImportFolderRepository();
				return repNS.GetByID(ImportFolderIDFile1);
			}
		}

		public string FullServerPath1
		{
			get
			{
				return Path.Combine(ImportFolder1.ImportFolderLocation, FilePathFile1);
			}
		}

		public ImportFolder ImportFolder2
		{
			get
			{
				ImportFolderRepository repNS = new ImportFolderRepository();
				return repNS.GetByID(ImportFolderIDFile2);
			}
		}

		public string FullServerPath2
		{
			get
			{
				return Path.Combine(ImportFolder2.ImportFolderLocation, FilePathFile2);
			}
		}

		public AniDB_File AniDBFile
		{
			get
			{
				AniDB_FileRepository repAniFile = new AniDB_FileRepository();
				return repAniFile.GetByHash(Hash);
			}
		}

		public Contract_DuplicateFile ToContract()
		{
			Contract_DuplicateFile contract = new Contract_DuplicateFile();
			contract.DateTimeUpdated = this.DateTimeUpdated;
			contract.DuplicateFileID = this.DuplicateFileID;
			contract.FilePathFile1 = this.FilePathFile1;
			contract.FilePathFile2 = this.FilePathFile2;
			contract.Hash = this.Hash;
			contract.ImportFolderIDFile1 = this.ImportFolderIDFile1;
			contract.ImportFolderIDFile2 = this.ImportFolderIDFile2;

			if (this.ImportFolder1 != null)
				contract.ImportFolder1 = this.ImportFolder1.ToContract();
			else contract.ImportFolder1 = null;

			if (this.ImportFolder2 != null)
				contract.ImportFolder2 = this.ImportFolder2.ToContract();
			else contract.ImportFolder2 = null;

			if (AniDBFile != null)
			{
				List<AniDB_Episode> eps = AniDBFile.Episodes;
				if (eps.Count > 0)
				{
					contract.EpisodeNumber = eps[0].EpisodeNumber;
					contract.EpisodeType = eps[0].EpisodeType;
					contract.EpisodeName = eps[0].RomajiName;
					contract.AnimeID = eps[0].AnimeID;
					AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
					AniDB_Anime anime = repAnime.GetByAnimeID(eps[0].AnimeID);
					if (anime != null)
						contract.AnimeName = anime.MainTitle;
				}
			}

			return contract;
		}
	}
}
