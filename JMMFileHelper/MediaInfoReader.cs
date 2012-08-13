using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NLog;
using JMMContracts;

namespace JMMFileHelper
{
	public class MediaInfoReader
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static bool ReadMediaInfo(string fileNameFull, bool forceRefresh, ref MediaInfoResult info)
		{
			try
			{

				if (!forceRefresh)
				{
					// if we have populated the vid res, we have already read the data
					if (info.VideoResolution.Length > 0) return false;
				}

				MediaInfo mi = MediaInfo.GetInstance();

				if (mi == null) return false;
				if (!File.Exists(fileNameFull)) return false;

				mi.Open(fileNameFull);
				string test = mi.getPlaytime();
				if (test.Length > 0)
				{
					info.VideoResolution = mi.getWidth() + "x" + mi.getHeight();
					info.VideoCodec = mi.getVidCodec();
					info.VideoBitrate = mi.getVidBitrate();
					info.VideoBitDepth = mi.getVidBitDepth();
					info.VideoFrameRate = mi.getFPS();
					info.AudioCodec = mi.getAudioCodec();
					info.AudioBitrate = mi.getAudioBitrate();
					// TODO CreateLanguages(mi.getAudioLanguages(), mi.getTextLanguages());
					int dur = 0;
					int.TryParse(mi.getDuration(), out dur);
					info.Duration = dur;

					info.VideoCodec = info.VideoCodec.Replace("V_MPEG4/ISO/AVC", "H264");
					info.VideoCodec = info.VideoCodec.Replace("V_MPEG2", "MPEG2");
					info.VideoCodec = info.VideoCodec.Replace("MPEG-2V", "MPEG2");
					info.VideoCodec = info.VideoCodec.Replace("DIV3", "DIVX");
					info.VideoCodec = info.VideoCodec.Replace("DX50", "DIVX");

					info.AudioCodec = info.AudioCodec.Replace("MPA1L3", "MP3");
					info.AudioCodec = info.AudioCodec.Replace("MPA2L3", "MP3");
					info.AudioCodec = info.AudioCodec.Replace("A_FLAC", "FLAC");
					info.AudioCodec = info.AudioCodec.Replace("A_AAC/MPEG4/LC/SBR", "AAC");
					info.AudioCodec = info.AudioCodec.Replace("A_AAC", "AAC");
					info.AudioCodec = info.AudioCodec.Replace("A_AC3", "AC3");

				}
				else
				{
					logger.Error("ERROR getting media info:: {0}", fileNameFull);
				}
				mi.Close();

			}
			catch (Exception ex)
			{
				logger.Error("Error reading Media Info for: {0} --- {1}", fileNameFull, ex.ToString());
			}

			return true;
		}
	}
}
