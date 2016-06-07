using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using JMMContracts;
using NLog;

namespace JMMFileHelper
{
    public class FileHashHelper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     Get all the hash info and video/audio info for a video file
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="hashInfo"></param>
        /// <param name="vidInfo"></param>
        public static void GetVideoInfo(string fileName, ref Hashes hashInfo, ref MediaInfoResult vidInfo,
            bool forceRefresh)
        {
            hashInfo = Hasher.CalculateHashes(fileName, null);
            if (vidInfo == null) vidInfo = new MediaInfoResult();
            MediaInfoReader.ReadMediaInfo(fileName, forceRefresh, ref vidInfo);
        }

        public static Hashes GetHashInfo(string fileName, bool forceRefresh, Hasher.OnHashProgress hashProgress,
            bool getCRC32, bool getMD5, bool getSHA1)
        {
            return Hasher.CalculateHashes(fileName, hashProgress, getCRC32, getMD5, getSHA1);
        }

        public static MediaInfoResult GetMediaInfo(string fileName, bool forceRefresh)
        {
            return GetMediaInfo(fileName, forceRefresh, false);
        }

        public static MediaInfoResult GetMediaInfo(string fileName, bool forceRefresh, bool useKodi)
        {
            var vidInfo = new MediaInfoResult();
            MediaInfoReader.ReadMediaInfo(fileName, forceRefresh, ref vidInfo, useKodi);
            return vidInfo;
        }


        public static bool IsVideo(string fileName)
        {
            var videoExtensions = GetVideoExtensions();
            if (videoExtensions.Count == 0) return false;

            if (videoExtensions.Contains(Path.GetExtension(fileName).Replace(".", "").Trim().ToUpper()))
                return true;

            return false;
        }

        public static List<string> GetVideoExtensions()
        {
            var extList = new List<string>();

            // Get the AppSettings section.
            var appSettings = ConfigurationManager.AppSettings;

            try
            {
                var exts = appSettings["VideoExtensions"];

                if (appSettings.Count == 0 || exts == null || exts.Trim().Length == 0)
                {
                    logger.Error("Could not find VideoExtensions app setting in config file");
                    return extList;
                }

                var extArray = exts.Split(',');
                foreach (var ext in extArray)
                {
                    extList.Add(ext.Trim().ToUpper());
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error in GetVideoExtensions: " + ex, ex);
            }

            return extList;
        }
    }
}