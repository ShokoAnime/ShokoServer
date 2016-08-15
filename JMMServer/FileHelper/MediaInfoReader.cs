using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using JMMContracts.PlexAndKodi;
using JMMServer.Entities;
using JMMServer.FileHelper.MediaInfo;
using JMMServer.FileHelper.Subtitles;
using JMMServer.Repositories;
using NLog;
using Stream = JMMContracts.PlexAndKodi.Stream;

namespace JMMServer.FileHelper
{
    public class MediaInfoReader
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static bool ReadMediaInfo(VideoLocal_Place v)
        {
            try
            {
                logger.Trace($"Getting media info for: {v.FullServerPath}");

                string name = (v.ImportFolder.CloudID == null) ? v.FullServerPath.Replace("/", "\\") : PlexAndKodi.Helper.ReplaceSchemeHost(PlexAndKodi.Helper.ConstructVideoLocalStream(0, v.VideoLocalID.ToString(), "file", false));
                Media m = MediaConvert.Convert(name,v.GetFile()); //Mediainfo should have libcurl.dll for http
                if (m != null && !string.IsNullOrEmpty(m.Duration))
                {
                    VideoLocal info = v.VideoLocal;
                    info.VideoResolution = (!string.IsNullOrEmpty(m.Width) && !string.IsNullOrEmpty(m.Height))
                        ? m.Width + "x" + m.Height
                        : string.Empty;
                    info.VideoCodec = (!string.IsNullOrEmpty(m.VideoCodec)) ? m.VideoCodec : string.Empty;
                    info.AudioCodec = (!string.IsNullOrEmpty(m.AudioCodec)) ? m.AudioCodec : string.Empty;
                    info.Duration = (!string.IsNullOrEmpty(m.Duration)) ? (long)double.Parse(m.Duration,NumberStyles.Any,CultureInfo.InvariantCulture) : 0;
                    info.VideoBitrate = info.VideoBitDepth = info.VideoFrameRate = info.AudioBitrate = string.Empty;
                    List<Stream> vparts = m.Parts[0].Streams.Where(a => a.StreamType == "1").ToList();
                    if (vparts.Count > 0)
                    {
                        if (!string.IsNullOrEmpty(vparts[0].Bitrate))
                            info.VideoBitrate = vparts[0].Bitrate;
                        if (!string.IsNullOrEmpty(vparts[0].BitDepth))
                            info.VideoBitDepth = vparts[0].BitDepth;
                        if (!string.IsNullOrEmpty(vparts[0].FrameRate))
                            info.VideoFrameRate = vparts[0].FrameRate;
                    }
                    List<Stream> aparts = m.Parts[0].Streams.Where(a => a.StreamType == "2").ToList();
                    if (aparts.Count > 0)
                    {
                        if (!string.IsNullOrEmpty(aparts[0].Bitrate))
                            info.AudioBitrate = aparts[0].Bitrate;
                    }
                    m.Id = v.VideoLocalID.ToString();
                    List<Stream> subs = SubtitleHelper.GetSubtitleStreams(v);
                    if (subs.Count > 0)
                    {
                        m.Parts[0].Streams.AddRange(subs);
                    }
                    foreach (Part p in m.Parts)
                    {
                        p.Id = null;
                        p.Accessible = "1";
                        p.Exists = "1";
                        bool vid = false;
                        bool aud = false;
                        bool txt = false;
                        foreach (Stream ss in p.Streams.ToArray())
                        {
                            if ((ss.StreamType == "1") && !vid)
                            {
                                vid = true;
                            }
                            if ((ss.StreamType == "2") && !aud)
                            {
                                aud = true;
                                ss.Selected = "1";
                            }
                            if ((ss.StreamType == "3") && !txt)
                            {
                                txt = true;
                                ss.Selected = "1";
                            }
                        }
                    }
                    info.Media = m;
                    return true;
                }
                logger.Error($"File {v.FullServerPath} do not exists, we're unable to read the media information from it");
            }
            catch (Exception e)
            {
                logger.Error($"Unable to read the media information of file {v.FullServerPath} ERROR: {e}");
            }
            return false;
        }
    }
}