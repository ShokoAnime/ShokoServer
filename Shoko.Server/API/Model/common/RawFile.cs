using System;
using System.Collections.Generic;
using Shoko.Server.Entities;

namespace Shoko.Server.API.Model.common
{
    public class RawFile
    {
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
        public int hash_source { get; set; }

        public int is_ignored { get; set; }

        public int id { get; set; }

        public string url { get; set; }

        public MediaInfo media { get; set; }

        // x-ref 
        public bool recognized { get; set; }
        // x-ref with videolocal_places
        public int videolocal_place_id { get; set; }
        public int import_folder_id { get; set; }

        public RawFile()
        {

        }

        public RawFile(VideoLocal vl, int level, int uid)
        {
            if (vl != null)
            {
                id = vl.VideoLocalID;

                crc32 = vl.CRC32;
                ed2khash = vl.ED2KHash;
                md5 = vl.MD5;
                sha1 = vl.SHA1;

                created = vl.DateTimeCreated;
                updated = vl.DateTimeUpdated;
                duration = vl.Duration;

                size = vl.FileSize;
                hash = vl.Hash;
                hash_source = vl.HashSource;

                is_ignored = vl.IsIgnored;

                if (vl.Places != null && vl.Places.Count >= 0)
                {
                    filename = vl.Places[0].FilePath;
                    videolocal_place_id = vl.Places[0].VideoLocal_Place_ID;
                    import_folder_id = vl.Places[0].ImportFolderID;
                }

                if (vl.EpisodeCrossRefs.Count == 0)
                { recognized = false; }
                else
                { recognized = true; }

                if (vl.Media != null && ( level > 1 || level == 0))
                {
                    media = new MediaInfo();

                    url = APIHelper.ConstructVideoLocalStream(uid, vl.Media.Id, "file." + vl.Media.Container, false);

                    MediaInfo new_media = new MediaInfo();

                    new_media.AddGeneral(MediaInfo.General.format, vl.Media.Container);
                    new_media.AddGeneral(MediaInfo.General.duration, vl.Media.Duration);
                    new_media.AddGeneral(MediaInfo.General.id, vl.Media.Id);
                    new_media.AddGeneral(MediaInfo.General.overallbitrate, vl.Media.Bitrate);

                    if (vl.Media.Parts != null)
                    {
                        new_media.AddGeneral(MediaInfo.General.size, vl.Media.Parts[0].Size);

                        foreach (Shoko.Models.PlexAndKodi.Stream p in vl.Media.Parts[0].Streams)
                        {
                            switch (p.StreamType)
                            {
                                //video
                                case "1":
                                    new_media.AddVideo(p);
                                    break;
                                //audio
                                case "2":
                                    new_media.AddAudio(p);
                                    break;
                                //subtitle
                                case "3":
                                    new_media.AddSubtitle(p);
                                    break;
                                //menu
                                case "4":
                                    Dictionary<string, string> mdict = new Dictionary<string, string>();
                                    //TODO APIv2: menu object could be usefull for external players
                                    new_media.AddMenu(mdict);
                                    break;
                            }
                        }
                    }

                    media = new_media;
                }
            }


        }

        /// <summary>
        /// Base on MediaInfo output using Stream objects
        /// </summary>

        public class MediaInfo
        {
            public Dictionary<General, string> general { get; private set; }

            //public Dictionary<int, Dictionary<Audio, string>> audios { get; private set; }
            public Dictionary<int, Shoko.Models.PlexAndKodi.Stream> audios { get; private set; }
            //public Dictionary<int, Dictionary<Video, string>> videos { get; private set; }
            public Dictionary<int, Shoko.Models.PlexAndKodi.Stream> videos { get; private set; }
            //public Dictionary<int, Dictionary<Subtitle, string>> subtitles { get; private set; }
            public Dictionary<int, Shoko.Models.PlexAndKodi.Stream> subtitles { get; private set; }

            public Dictionary<int, Dictionary<string, string>> menus { get; private set; }

            public MediaInfo()
            {
                general = new Dictionary<General, string>();
                audios = new Dictionary<int, Shoko.Models.PlexAndKodi.Stream>();
                videos = new Dictionary<int, Shoko.Models.PlexAndKodi.Stream>();
                subtitles = new Dictionary<int, Shoko.Models.PlexAndKodi.Stream>();
                menus = new Dictionary<int, Dictionary<string, string>>();
            }

            public void AddGeneral(General param, string value)
            {
                general.Add(param, value);
            }

            public void AddAudio(Shoko.Models.PlexAndKodi.Stream dict)
            {
                audios.Add(audios.Count + 1, dict);
            }

            public void AddVideo(Shoko.Models.PlexAndKodi.Stream dict)
            {
                videos.Add(videos.Count + 1, dict);
            }

            public void AddSubtitle(Shoko.Models.PlexAndKodi.Stream dict)
            {
                subtitles.Add(subtitles.Count + 1, dict);
            }

            public void AddMenu(Dictionary<string, string> dict)
            {
                menus.Add(menus.Count + 1, dict);
            }
 
            public enum General
            {
                id,
                format,
                format_version,
                size,
                duration,
                overallbitrate,
                overallbitrate_mode,
                encoded,
                encoded_date,
                encoded_lib,
                attachments
            }
            /*
             * maybe for later use as stream is already doing the same
            public enum Audio
            {
                id,
                format,
                duration,
                bitrate,
                bitratemode,
                channel,
                sampling,
                bitdepht,
                size,
                title,
                encoded,
                lang,
                _default,
                _forced
            }

            public enum Video
            {
                id,
                format,
                format_profile,
                refframe,
                duration,
                bitrate,
                width,
                height,
                aspectratio,
                framerate,
                frameratemode,
                colorspace,
                chromasampling,
                bitdepth,
                scantype,
                size,
                title,
                encoded,
                encoded_settings,
                lang,
                _default,
                _forced
            }

            public enum Subtitle
            {
                id,
                format,
                title,
                lang,
                duration,
                _default,
                _forced
            }
            */
        }
    }
}
