using System;
using System.Collections.Generic;

namespace JMMServer.API.Model.common
{
    public class RawFile
    {
        public Dictionary<string, string> audio { get; set; }

        public Dictionary<string, string> video { get; set; }

        public string crc32 { get; set; }
        public string ed2khash { get; set; }
        public string md5 { get; set; }
        public string sha1 { get; set; }

        public DateTime created { get; set; }
        public DateTime updated { get; set; }
        public long duration { get; set; }

        public string filename { get; set; }
        public long size { get; set; }
        public string hash { get; set; }
        public int hashsource { get; set; }

        public string info { get; set; }
        public int isignored { get; set; }

        public JMMContracts.PlexAndKodi.Media media { get; set; }
        public int mediasize { get; set; }

        public int id { get; set; }

        public string url { get; set; }

        public ArtCollection art { get; set; }

        public RawFile()
        {
            audio = new Dictionary<string, string>();
            video = new Dictionary<string, string>();
            art = new ArtCollection();
        }

        public RawFile(Entities.VideoLocal vl)
        {
            if (vl != null)
            {
                id = vl.VideoLocalID;
                audio = new Dictionary<string, string>();
                video = new Dictionary<string, string>();
                art = new ArtCollection();

                audio.Add("bitrate", vl.AudioBitrate);
                audio.Add("codec", vl.AudioCodec);

                video.Add("bitrate", vl.VideoBitrate);
                video.Add("bitdepth", vl.VideoBitDepth);
                video.Add("codec", vl.VideoCodec);
                video.Add("fps", vl.VideoFrameRate);
                video.Add("resolution", vl.VideoResolution);

                crc32 = vl.CRC32;
                ed2khash = vl.ED2KHash;
                md5 = vl.MD5;
                sha1 = vl.SHA1;

                created = vl.DateTimeCreated;
                updated = vl.DateTimeUpdated;
                duration = vl.Duration;

                filename = vl.FileName;
                size = vl.FileSize;
                hash = vl.Hash;
                hashsource = vl.HashSource;

                info = vl.Info;
                isignored = vl.IsIgnored;

                mediasize = vl.MediaSize;

                media = vl.Media;

                if (media?.Parts != null)
                {
                    foreach (JMMContracts.PlexAndKodi.Part p in media.Parts)
                    {
                        string ff = "file." + p.Container;
                        // TODO APIV2: replace 1 with userid or rewrite file server
                        url = APIHelper.ConstructVideoLocalStream(1, media.Id, ff, false);
                    }
                }
            }
        }
    }
}
