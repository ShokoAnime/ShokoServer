﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Stream = Shoko.Models.PlexAndKodi.Stream;

namespace Shoko.Server.API.v2.Models.common;

[DataContract]
public class RawFile : BaseDirectory
{
    private Logger logger = LogManager.GetCurrentClassLogger();

    public override string type => "file";

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public string crc32 { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public string ed2khash { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public string md5 { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    public string sha1 { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime created { get; set; }

    [DataMember(IsRequired = false, EmitDefaultValue = false)]
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime updated { get; set; }

    [DataMember(IsRequired = true, EmitDefaultValue = true)]
    public long duration { get; set; }

    [DataMember(IsRequired = true, EmitDefaultValue = true)]
    public string filename { get; set; }

    [DataMember(IsRequired = true, EmitDefaultValue = true)]
    public string server_path { get; set; }

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

    [DataMember(IsRequired = false, EmitDefaultValue = true)]
    public int is_preferred { get; set; }

    [DataContract]
    public class RecentFile : RawFile
    {
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int series_id { get; set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int ep_id { get; set; }

        public RecentFile() { }

        public RecentFile(HttpContext ctx, SVR_VideoLocal vl, int level, int uid, AnimeEpisode e = null) : base(ctx,
            vl, level, uid, e)
        {
        }
    }

    public RawFile()
    {
    }

    public RawFile(HttpContext ctx, SVR_VideoLocal vl, int level, int uid, AnimeEpisode e = null)
    {
        if (vl == null)
        {
            return;
        }

        id = vl.VideoLocalID;

        crc32 = vl.CRC32;
        ed2khash = vl.ED2KHash;
        md5 = vl.MD5;
        sha1 = vl.SHA1;

        created = vl.DateTimeCreated;
        updated = vl.DateTimeUpdated;
        duration = vl.Duration;

        var releaseGroup = vl.ReleaseGroup;
        if (releaseGroup != null)
        {
            group_full = releaseGroup.GroupName;
            group_short = releaseGroup.GroupNameShort;
            group_id = releaseGroup.AniDB_ReleaseGroupID;
        }

        size = vl.FileSize;
        hash = vl.Hash;
        hash_source = vl.HashSource;

        is_ignored = vl.IsIgnored ? 1 : 0;
        var vl_user = vl.GetUserRecord(uid);
        offset = vl_user?.ResumePosition ?? 0;

        var place = vl.GetBestVideoLocalPlace();
        if (place != null)
        {
            filename = place.FilePath;
            server_path = place.FullServerPath;
            videolocal_place_id = place.VideoLocal_Place_ID;
            import_folder_id = place.ImportFolderID;
        }

        url = APIV2Helper.ConstructVideoLocalStream(ctx, uid, vl.VideoLocalID.ToString(),
            "file" + Path.GetExtension(filename), false);

        recognized = e != null || vl.EpisodeCrossRefs.Count != 0;

        if (vl.Media?.GeneralStream == null || level < 0)
        {
            return;
        }

        var new_media = new MediaInfo();

        try
        {
            var legacy = new Media(vl.VideoLocalID, vl.Media);

            new_media.AddGeneral(MediaInfo.General.format, legacy.Container);
            new_media.AddGeneral(MediaInfo.General.duration, legacy.Duration);
            new_media.AddGeneral(MediaInfo.General.id, legacy.Id);
            new_media.AddGeneral(MediaInfo.General.overallbitrate, legacy.Bitrate);

            if (legacy.Parts != null)
            {
                new_media.AddGeneral(MediaInfo.General.size, legacy.Parts[0].Size);

                foreach (var p in legacy.Parts[0].Streams)
                {
                    switch (p.StreamType)
                    {
                        //video
                        case 1:
                            new_media.AddVideo(p);
                            break;
                        //audio
                        case 2:
                            new_media.AddAudio(p);
                            break;
                        //subtitle
                        case 3:
                            new_media.AddSubtitle(p);
                            break;
                        //menu
                        case 4:
                            var mdict = new Dictionary<string, string>();
                            new_media.AddMenu(mdict);
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }

        media = new_media;
    }

    /// <summary>
    /// Base on MediaInfo output using Stream objects
    /// </summary>
    [DataContract]
    public class MediaInfo
    {
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public Dictionary<General, object> general { get; private set; }

        //public Dictionary<int, Dictionary<Audio, string>> audios { get; private set; }
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public Dictionary<int, Stream> audios { get; private set; }

        //public Dictionary<int, Dictionary<Video, string>> videos { get; private set; }
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public Dictionary<int, Stream> videos { get; private set; }

        //public Dictionary<int, Dictionary<Subtitle, string>> subtitles { get; private set; }
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public Dictionary<int, Stream> subtitles { get; private set; }

        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public Dictionary<int, Dictionary<string, string>> menus { get; private set; }

        public MediaInfo()
        {
            general = new Dictionary<General, object>();
            audios = new Dictionary<int, Stream>();
            videos = new Dictionary<int, Stream>();
            subtitles = new Dictionary<int, Stream>();
            menus = new Dictionary<int, Dictionary<string, string>>();
        }

        public void AddGeneral(General param, object value)
        {
            general.Add(param, value);
        }

        public void AddAudio(Stream dict)
        {
            audios.Add(audios.Count + 1, (Stream)dict.Clone());
        }

        public void AddVideo(Stream dict)
        {
            videos.Add(videos.Count + 1, (Stream)dict.Clone());
        }

        public void AddSubtitle(Stream dict)
        {
            subtitles.Add(subtitles.Count + 1, (Stream)dict.Clone());
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
