using System.Collections.Generic;
using NutzCode.CloudFileSystem.Plugins.AmazonCloudDrive;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Configuration;

namespace Shoko.Server.Settings
{
    public class ImportSettings : IDefaultedConfig
    {
        public HashSet<string> VideoExtensions { get; set; }

        public HashSet<string> Exclude { get; set; }

        public RenamingLanguage DefaultSeriesLanguage { get; set; } = RenamingLanguage.Romaji;

        public RenamingLanguage DefaultEpisodeLanguage { get; set; } = RenamingLanguage.Romaji;

        public bool RunOnStart { get; set; } = false;

        public bool ScanDropFoldersOnStart { get; set; } = false;

        public bool Hash_CRC32 { get; set; } = false;
        public bool Hash_MD5 { get; set; } = false;
        public bool Hash_SHA1 { get; set; } = false;

        public bool UseExistingFileWatchedStatus { get; set; } = true;

        public bool AutomaticallyDeleteDuplicatesOnImport { get; set; } = false;

        public bool FileLockChecking { get; set; } = true;

        public bool AggressiveFileLockChecking { get; set; } = true;

        public int FileLockWaitTimeMS { get; set; } = 4000;

        public int AggressiveFileLockWaitTimeSeconds { get; set; } = 8;
        
        public bool RenameThenMove { get; set; }

        public bool RenameOnImport { get; set; } = true;
        public bool MoveOnImport { get; set; } = true;

        public string MediaInfoPath { get; set; }

        public int MediaInfoTimeoutMinutes { get; set; } = 5;
        public void SetDefaults()
        {
            VideoExtensions ??=  new() {"MKV", "AVI", "MP4", "MOV", "OGM", "WMV", "MPG", "MPEG", "MK3D", "M4V"};
            VideoExtensions2 ??=  new() {"MKV", "AVI", "MP4", "MOV", "OGM", "WMV", "MPG", "MPEG", "MK3D", "M4V"};
            Exclude ??= new() { @"[\\\/]\$RECYCLE\.BIN[\\\/]", @"[\\\/]\.Recycle\.Bin[\\\/]", @"[\\\/]\.Trash-\d+[\\\/]" };
        }
    }
}