using System;
using System.Collections.Generic;
using System.IO;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Repositories;

namespace Shoko.Server.Entities
{
    public class SVR_DuplicateFile : DuplicateFile
    {

        public SVR_DuplicateFile()
        {
        }
        public override string ToString()
        {
            return $"{FilePathFile1} --- {FilePathFile2}";
        }

        public SVR_ImportFolder ImportFolder1 => RepoFactory.ImportFolder.GetByID(ImportFolderIDFile1);

        public string FullServerPath1 => Path.Combine(ImportFolder1.ImportFolderLocation, FilePathFile1);

        public SVR_ImportFolder ImportFolder2 => RepoFactory.ImportFolder.GetByID(ImportFolderIDFile2);

        public string FullServerPath2 => Path.Combine(ImportFolder2.ImportFolderLocation, FilePathFile2);

        public SVR_AniDB_File AniDBFile => RepoFactory.AniDB_File.GetByHash(Hash);

        public CL_DuplicateFile ToClient()
        {
            CL_DuplicateFile cl = this.CloneToClient();
            if (AniDBFile != null)
            {
                List<SVR_AniDB_Episode> eps = AniDBFile.Episodes;
                if (eps.Count > 0)
                {
                    cl.EpisodeNumber = eps[0].EpisodeNumber;
                    cl.EpisodeType = eps[0].EpisodeType;
                    cl.EpisodeName = eps[0].RomajiName;
                    cl.AnimeID = eps[0].AnimeID;
                    SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(eps[0].AnimeID);
                    if (anime != null)
                        cl.AnimeName = anime.MainTitle;
                }
            }

            return cl;
        }
    }
}