using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using System.Configuration;
using NLog;
using System.IO;
using JMMContracts;

namespace JMMFileHelper
{
	public class FileHashHelper
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		/// <summary>
		/// Get all the hash info and video/audio info for a video file
		/// </summary>
		/// <param name="fileName"></param>
		/// <param name="hashInfo"></param>
		/// <param name="vidInfo"></param>
		public static void GetVideoInfo(string fileName, ref Hashes hashInfo, ref MediaInfoResult vidInfo, bool forceRefresh)
		{
			hashInfo = Hasher.CalculateHashes(fileName, null);
			if (vidInfo == null) vidInfo = new MediaInfoResult();
			MediaInfoReader.ReadMediaInfo(fileName, forceRefresh, ref vidInfo);
		}

		public static Hashes GetHashInfo(string fileName, bool forceRefresh, JMMFileHelper.Hasher.OnHashProgress hashProgress, bool getCRC32, bool getMD5, bool getSHA1)
		{
			return Hasher.CalculateHashes(fileName, hashProgress, getCRC32, getMD5, getSHA1);
		}

        public static MediaInfoResult GetMediaInfo(string fileName, bool forceRefresh)
        {
            return GetMediaInfo(fileName, forceRefresh, false);
        }

        public static MediaInfoResult GetMediaInfo(string fileName, bool forceRefresh, bool useKodi)
        {
            MediaInfoResult vidInfo = new MediaInfoResult();
			MediaInfoReader.ReadMediaInfo(fileName, forceRefresh, ref vidInfo, useKodi);
			return vidInfo;
		}

		

		public static bool IsVideo(string fileName)
		{
			List<string> videoExtensions = GetVideoExtensions();
			if (videoExtensions.Count == 0) return false;

			if (videoExtensions.Contains(Path.GetExtension(fileName).Replace(".", "").Trim().ToUpper()))
				return true;

			return false;
		}

		public static List<string> GetVideoExtensions()
		{
			List<string> extList = new List<string>();

			// Get the AppSettings section.
			NameValueCollection appSettings = ConfigurationManager.AppSettings;

			try
			{
				string exts = appSettings["VideoExtensions"];

				if (appSettings.Count == 0 || exts == null || exts.Trim().Length == 0)
				{
					logger.Error("Could not find VideoExtensions app setting in config file");
					return extList;
				}

				string[] extArray = exts.Split(',');
				foreach (string ext in extArray)
				{
					extList.Add(ext.Trim().ToUpper());
				}
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error in GetVideoExtensions: " + ex.ToString(), ex); 
			}

			return extList;
		}
	}
}
