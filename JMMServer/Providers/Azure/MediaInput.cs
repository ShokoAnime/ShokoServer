using System;
using System.Collections.Generic;
using System.Linq;
using Force.DeepCloner;
using JMMServer.Entities;
using JMMServer.LZ4;

namespace JMMServer.Providers.Azure
{
    public class MediaInput
    {
        public string ED2K { get; set; }
        public byte[] MediaInfo { get; set; }
        public int Version { get; set; }
        public string Username { get; set; }
        public string AuthGUID { get; set; }

        public MediaInput()
        {
            this.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                this.Username = Constants.AnonWebCacheUsername;

            this.AuthGUID = string.Empty;
        }

        public MediaInput(VideoLocal v)
        {
            ED2K = v.ED2KHash;
            //Cleanup any File subtitles from media information.
            JMMContracts.PlexAndKodi.Media m = v.Media.DeepClone();
            if (m.Parts != null && m.Parts.Count > 0)
            {
                foreach (JMMContracts.PlexAndKodi.Part p in m.Parts)
                {
                    if (p.Streams != null)
                    {
                        List<JMMContracts.PlexAndKodi.Stream> streams=p.Streams.Where(a=>a.StreamType=="3" && !string.IsNullOrEmpty(a.File)).ToList();
                        if (streams.Count > 0)
                            streams.ForEach(a => p.Streams.Remove(a));
                    }
                }
            }
            //Cleanup the VideoLocal id
            m.Id = null;
            int outsize;
            byte[] data = CompressionHelper.SerializeObject(m, out outsize);
            ED2K = v.ED2KHash;
            MediaInfo = new byte[data.Length + 4];
            MediaInfo[0] = (byte)(outsize >> 24);
            MediaInfo[1] = (byte)((outsize >> 16) & 0xFF);
            MediaInfo[2] = (byte)((outsize >> 8) & 0xFF);
            MediaInfo[3] = (byte)(outsize & 0xFF);
            Array.Copy(data, 0, MediaInfo, 4, data.Length);
            Version = VideoLocal.MEDIA_VERSION;
            this.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                this.Username = Constants.AnonWebCacheUsername;
            this.AuthGUID = string.Empty;



        }
        public MediaInput(string ed2k, JMMContracts.PlexAndKodi.Media m)
        {
            int outsize;
            byte[] data = CompressionHelper.SerializeObject(m, out outsize);
            ED2K = ed2k;
            MediaInfo = new byte[data.Length + 4];
            MediaInfo[0] = (byte)(outsize >> 24);
            MediaInfo[1] = (byte)((outsize >> 16) & 0xFF);
            MediaInfo[2] = (byte)((outsize >> 8) & 0xFF);
            MediaInfo[3] = (byte)(outsize & 0xFF);
            Array.Copy(data, 0, MediaInfo, 4, data.Length);
            Version = VideoLocal.MEDIA_VERSION;
            this.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                this.Username = Constants.AnonWebCacheUsername;
            this.AuthGUID = string.Empty;
        }

    }
}