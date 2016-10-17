using System;
using JMMServer.LZ4;

namespace JMMServer.Providers.Azure
{
    public class Media
    {
        public int MediaID { get; set; }

        public string ED2K { get; set; }

        public byte[] MediaInfo { get; set; }

        public int Version { get; set; }

        public string Username { get; set; }
        public int IsAdminApproved { get; set; }
        public long DateSubmitted { get; set; }

        public JMMContracts.PlexAndKodi.Media GetMedia()
        {
            int size = MediaInfo[0] << 24 | MediaInfo[1] << 16 | MediaInfo[2] << 8 | MediaInfo[3];
            byte[] data = new byte[MediaInfo.Length - 4];
            Array.Copy(MediaInfo, 4, data, 0, data.Length);
            return CompressionHelper.DeserializeObject<JMMContracts.PlexAndKodi.Media>(data, size);
        }
    }
}