using System;
using System.IO;
using JMMContracts;
using JMMServer.Repositories;

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

        public ImportFolder ImportFolder1
        {
            get
            {
                var repNS = new ImportFolderRepository();
                return repNS.GetByID(ImportFolderIDFile1);
            }
        }

        public string FullServerPath1
        {
            get { return Path.Combine(ImportFolder1.ImportFolderLocation, FilePathFile1); }
        }

        public ImportFolder ImportFolder2
        {
            get
            {
                var repNS = new ImportFolderRepository();
                return repNS.GetByID(ImportFolderIDFile2);
            }
        }

        public string FullServerPath2
        {
            get { return Path.Combine(ImportFolder2.ImportFolderLocation, FilePathFile2); }
        }

        public AniDB_File AniDBFile
        {
            get
            {
                var repAniFile = new AniDB_FileRepository();
                return repAniFile.GetByHash(Hash);
            }
        }

        public override string ToString()
        {
            return string.Format("{0} --- {1}", FilePathFile1, FilePathFile2);
        }

        public Contract_DuplicateFile ToContract()
        {
            var contract = new Contract_DuplicateFile();
            contract.DateTimeUpdated = DateTimeUpdated;
            contract.DuplicateFileID = DuplicateFileID;
            contract.FilePathFile1 = FilePathFile1;
            contract.FilePathFile2 = FilePathFile2;
            contract.Hash = Hash;
            contract.ImportFolderIDFile1 = ImportFolderIDFile1;
            contract.ImportFolderIDFile2 = ImportFolderIDFile2;

            if (ImportFolder1 != null)
                contract.ImportFolder1 = ImportFolder1.ToContract();
            else contract.ImportFolder1 = null;

            if (ImportFolder2 != null)
                contract.ImportFolder2 = ImportFolder2.ToContract();
            else contract.ImportFolder2 = null;

            if (AniDBFile != null)
            {
                var eps = AniDBFile.Episodes;
                if (eps.Count > 0)
                {
                    contract.EpisodeNumber = eps[0].EpisodeNumber;
                    contract.EpisodeType = eps[0].EpisodeType;
                    contract.EpisodeName = eps[0].RomajiName;
                    contract.AnimeID = eps[0].AnimeID;
                    var repAnime = new AniDB_AnimeRepository();
                    var anime = repAnime.GetByAnimeID(eps[0].AnimeID);
                    if (anime != null)
                        contract.AnimeName = anime.MainTitle;
                }
            }

            return contract;
        }
    }
}