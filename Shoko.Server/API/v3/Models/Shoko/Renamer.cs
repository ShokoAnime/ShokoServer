using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.API.v3.Models.Common;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class Renamer
{
    /// <summary>
    /// The ID of the renamer
    /// </summary>
    public string RenamerID { get; set; }

    /// <summary>
    /// The assembly version of the renamer.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// The name of the renamer. This is a unique ID!
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// A short description about the renamer.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// If the renamer is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The setting type definitions for the renamer.
    /// </summary>
    public List<RenamerSetting> Settings { get; set; }

    public class RenamerSetting
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string? Description { get; set; }
        public CodeLanguage? Language { get; set; }
        public RenamerSettingType SettingType { get; set; }
    }

    /// <summary>
    /// Represents the result of a file relocation process.
    /// </summary>
    public class RelocateResult
    {
        /// <summary>
        /// The file id.
        /// </summary>
        [Required]
        public int FileID { get; set; }

        /// <summary>
        /// The file location id. May be null if a location to use was not
        /// found.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? FileLocationID { get; set; } = null;

        /// <summary>
        /// The id of the script that produced the final location for the
        /// file if the relocation was successful and was not the result of
        /// a manual relocation.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? ScriptID { get; set; } = null;

        /// <summary>
        /// The new id of the <see cref="ImportFolder"/> where the file now
        /// resides, if the relocation was successful. Remember to check
        /// <see cref="IsSuccess"/> to see the status of the relocation.
        /// </summary>
        /// 
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? ImportFolderID { get; set; } = null;

        /// <summary>
        /// Indicates whether the file was relocated successfully.
        /// </summary>
        [Required]
        public bool IsSuccess { get; set; } = false;

        /// <summary>
        /// Indicates whether the file was actually relocated from one
        /// location to another, or if it was already at it's correct
        /// location.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsRelocated { get; set; } = null;

        /// <summary>
        /// Indicates if the result is only a preview and the file has not
        /// actually been relocated yet.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsPreview { get; set; } = null;

        /// <summary>
        /// The error message if the relocation was not successful.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// The new relative path from the <see cref="ImportFolder"/>'s path
        /// on the server, if relocation was successful. Remember to check
        /// <see cref="IsSuccess"/> to see the status of the relocation.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? RelativePath { get; set; } = null;

        /// <summary>
        /// The new absolute path for the file on the server, if relocation
        /// was successful. Remember to check <see cref="IsSuccess"/> to see
        /// the status of the relocation.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? AbsolutePath { get; set; } = null;
    }

        public class BatchAutoRelocateBody
        {
            /// <summary>
            /// Indicates whether the result should be a preview of the
            /// relocation.
            /// </summary>
            public bool Preview { get; set; } = true;

            /// <summary>
            /// Move the files. Leave as `null` to use the default
            /// setting for move on import.
            /// </summary>
            public bool Move { get; set; } = false;

            /// <summary>
            /// Indicates whether empty directories should be deleted after
            /// relocating the file.
            /// </summary>
            public bool DeleteEmptyDirectories { get; set; } = true;

            /// <summary>
            /// List of Shoko file IDs to relocate to the new location.
            /// </summary>
            [Required]
            public List<int> FileIDs { get; set; } = [];
        }

        public class BatchPreviewAutoRelocateWithRenamerBody
        {
            /// <summary>
            /// The name of the renamer to use.
            /// </summary>
            [Required]
            public string RenamerName { get; set; } = string.Empty;

            /// <summary>
            /// The script body, if any is needed for the renamer. Can be
            /// omitted if the renamer doesn't require a script.
            /// </summary>
            public string? ScriptBody { get; set; } = null;

            /// <summary>
            /// Move the files. Leave as `null` to use the default
            /// setting for move on import.
            /// </summary>
            public bool Move { get; set; } = false;

            /// <summary>
            /// List of Shoko file IDs to preview the new location for.
            /// </summary>
            [Required]
            public List<int> FileIDs { get; set; } = [];
        }
    }
