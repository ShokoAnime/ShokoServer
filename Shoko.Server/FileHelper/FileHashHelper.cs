using System;
using System.Collections.Generic;
using System.IO;
using NLog;
using Shoko.Models.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.FileHelper
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
        public static Hashes GetHashInfo(string fileName, bool forceRefresh, Hasher.OnHashProgress hashProgress,
            bool getCRC32, bool getMD5, bool getSHA1)
        {
            return Hasher.CalculateHashes(fileName, hashProgress, getCRC32, getMD5, getSHA1);
        }


        public static bool IsVideo(string fileName)
        {
            List<string> videoExtensions = GetVideoExtensions();
            if (videoExtensions.Count == 0) return false;

            if (videoExtensions.Contains(Path.GetExtension(fileName).Replace(".", string.Empty).Trim().ToUpper()))
                return true;

            return false;
        }

        public static List<string> GetVideoExtensions()
        {
            List<string> extList = new List<string>();


            try
            {
                string[] exts = ServerSettings.Instance.Import.VideoExtensions;

                if (exts == null || exts.Length == 0)
                {
                    logger.Error("Could not find VideoExtensions app setting in config file");
                    return extList;
                }

                foreach (string ext in exts)
                {
                    extList.Add(ext.Trim().ToUpper());
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in GetVideoExtensions: " + ex);
            }

            return extList;
        }
    }
}