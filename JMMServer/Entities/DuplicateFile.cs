using System;
using System.Collections.Generic;
using System.IO;
using JMMContracts;
using JMMServer.Repositories;
using JMMServer.Repositories.Cached;

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
            return $"{FilePathFile1} --- {FilePathFile2}";
        }

        public ImportFolder ImportFolder1 => RepoFactory.ImportFolder.GetByID(ImportFolderIDFile1);

        public string FullServerPath1 => Path.Combine(ImportFolder1.ParsedImportFolderLocation, FilePathFile1);

        public ImportFolder ImportFolder2 => RepoFactory.ImportFolder.GetByID(ImportFolderIDFile2);

        public string FullServerPath2 => Path.Combine(ImportFolder2.ParsedImportFolderLocation, FilePathFile2);

        public AniDB_File AniDBFile => RepoFactory.AniDB_File.GetByHash(Hash);

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
                    AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(eps[0].AnimeID);
                    if (anime != null)
                        contract.AnimeName = anime.MainTitle;
                }
            }

            return contract;
        }
    }
}