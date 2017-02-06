using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using JMMServer.Entities;

namespace JMMServer.API.Model.common
{
    [DataContract]
    public class RawFile : BaseDirectory
    {
        public override string type { get { return "file"; } }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string crc32 { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string ed2khash { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string md5 { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string sha1 { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public DateTime created { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public DateTime updated { get; set; }

        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public long duration { get; set; }

        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string filename { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public new long size { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string hash { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int hash_source { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int is_ignored { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public MediaInfo media { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string group_full { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string group_short { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int group_id { get; set; }

        // x-ref
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public bool recognized { get; set; }

        // x-ref with videolocal_user
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public long offset { get; set; }

        // x-ref with videolocal_places
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int videolocal_place_id { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int import_folder_id { get; set; }

        public RawFile()
        {

        }

        public RawFile(Entities.VideoLocal vl, int level, int uid)
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

                if (vl.ReleaseGroup != null)
                {
                    group_full = vl.ReleaseGroup.GroupName;
                    group_short = vl.ReleaseGroup.GroupNameShort;
                    group_id = vl.ReleaseGroup.AniDB_ReleaseGroupID;
                }

                size = vl.FileSize;
                hash = vl.Hash;
                hash_source = vl.HashSource;

                is_ignored = vl.IsIgnored;
                VideoLocal_User vl_user = vl.GetUserRecord(uid);
                if (vl_user != null)
                {
                    offset = vl_user.ResumePosition;
                }
                else
                {
                    offset = 0;
                }

                VideoLocal_Place place = vl.GetBestVideoLocalPlace();
                if (place != null)
                {
                    filename = place.FilePath;
                    videolocal_place_id = place.VideoLocal_Place_ID;
                    import_folder_id = place.ImportFolderID;
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

                        foreach (JMMContracts.PlexAndKodi.Stream p in vl.Media.Parts[0].Streams)
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

        [DataContract]
        public class MediaInfo
        {
            [DataMember(IsRequired = false, EmitDefaultValue = false)]
            public Dictionary<General, string> general { get; private set; }

            //public Dictionary<int, Dictionary<Audio, string>> audios { get; private set; }
            [DataMember(IsRequired = false, EmitDefaultValue = false)]
            public Dictionary<int, JMMContracts.PlexAndKodi.Stream> audios { get; private set; }
            //public Dictionary<int, Dictionary<Video, string>> videos { get; private set; }
            [DataMember(IsRequired = false, EmitDefaultValue = false)]
            public Dictionary<int, JMMContracts.PlexAndKodi.Stream> videos { get; private set; }
            //public Dictionary<int, Dictionary<Subtitle, string>> subtitles { get; private set; }
            [DataMember(IsRequired = false, EmitDefaultValue = false)]
            public Dictionary<int, JMMContracts.PlexAndKodi.Stream> subtitles { get; private set; }

            [DataMember(IsRequired = false, EmitDefaultValue = false)]
            public Dictionary<int, Dictionary<string, string>> menus { get; private set; }

            public MediaInfo()
            {
                general = new Dictionary<General, string>();
                audios = new Dictionary<int, JMMContracts.PlexAndKodi.Stream>();
                videos = new Dictionary<int, JMMContracts.PlexAndKodi.Stream>();
                subtitles = new Dictionary<int, JMMContracts.PlexAndKodi.Stream>();
                menus = new Dictionary<int, Dictionary<string, string>>();
            }

            public void AddGeneral(General param, string value)
            {
                general.Add(param, value);
            }

            public void AddAudio(JMMContracts.PlexAndKodi.Stream dict)
            {
                audios.Add(audios.Count + 1, dict);
            }

            public void AddVideo(JMMContracts.PlexAndKodi.Stream dict)
            {
                videos.Add(videos.Count + 1, dict);
            }

            public void AddSubtitle(JMMContracts.PlexAndKodi.Stream dict)
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
