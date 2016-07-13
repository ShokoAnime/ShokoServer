using JMMServer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.Providers.Azure
{
    public class FileHashInput
    {
        public string ED2K { get; set; }
        public string CRC32 { get; set; }
        public string MD5 { get; set; }
        public string SHA1 { get; set; }
        public long FileSize { get; set; }

        public string Username { get; set; }
        public string AuthGUID { get; set; }

        public FileHashInput()
        {
            this.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                this.Username = Constants.AnonWebCacheUsername;

            this.AuthGUID = string.Empty;
        }

        public FileHashInput(AniDB_File anifile)
        {
            ED2K = anifile.Hash;
            CRC32 = anifile.CRC;
            MD5 = anifile.MD5;
            SHA1 = anifile.SHA1;
            FileSize = anifile.FileSize;

            this.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                this.Username = Constants.AnonWebCacheUsername;

            this.AuthGUID = string.Empty;
        }
    }
}
