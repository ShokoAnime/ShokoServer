using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Models.Server;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

using RenameFileHelper = Shoko.Server.Renamer.RenameFileHelper;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class Renamer : BaseModel
{
    /// <summary>
    /// The assembly version of the renamer.
    /// </summary>
    public string Version { get; set; }
    /// <summary>
    /// A short description about the renamer.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// If the renamer is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Lower numbers mean higher priority. Will be null if a priority is not set yet.
    /// </summary>
    public int? Priority { get; set; }

    public Renamer(string name, (Type type, string description, string version) value)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var scripts = RepoFactory.RenameScript.GetByRenamerType(name);
        Name = name;
        Size = scripts.Count;
        Version = value.version;
        Description = value.description;
        Enabled = !settings.Plugins.EnabledRenamers.TryGetValue(name, out var enabled) || enabled;
        Priority = settings.Plugins.RenamerPriorities.TryGetValue(Name, out var priority) ? priority : null;
    }

    public class Script
    {

        /// <summary>
        /// Script ID.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Script name. Must be unique across all scripts.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The last known name of the <see cref="Models.Shoko.Renamer"/> this script belongs to.
        /// </summary>
        public string RenamerName { get; set; }

        /// <summary>
        /// True if the renamer is locally available.
        /// </summary>
        public bool RenamerIsAvailable { get; set; }

        /// <summary>
        /// Determines if the script should ran automatically on import if the renamer is enabled.
        /// </summary>
        public bool EnabledOnImport { get; set; }

        /// <summary>
        /// The script body.
        /// </summary>
        public string? Body { get; set; }

        public Script(RenameScript script)
        {
            ID = script.RenameScriptID;
            Name = script.ScriptName;
            RenamerName = script.RenamerType;
            RenamerIsAvailable = RenameFileHelper.Renamers.ContainsKey(script.RenamerType);
            EnabledOnImport = script.IsEnabledOnImport == 1;
            Body = !string.IsNullOrWhiteSpace(script.Script) ? script.Script : null;
        }
    }

    /// <summary>
    /// Represents the result of a file relocation process.
    /// </summary>
    public class RelocateResult
    {
        /// <summary>
        /// The file location id.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// The file id.
        /// </summary>
        public int FileID { get; set; }

        /// <summary>
        /// The id of the script that produced the final location for the
        /// file if the relocation was successful and was not the result of
        /// a manual relocation.
        /// </summary>
        public int? ScriptID { get; set; } = null;

        /// <summary>
        /// The error message if the relocation was not successful.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// The new id of the <see cref="ImportFolder"/> where the file now
        /// resides, if the relocation was successful. Remember to check
        /// <see cref="IsSuccess"/> to see the status of the relocation.
        /// </summary>
        public int? ImportFolderID { get; set; } = null;

        /// <summary>
        /// The new relative path from the <see cref="ImportFolder"/>'s path
        /// on the server, if relocation was successful. Remember to check
        /// <see cref="IsSuccess"/> to see the status of the relocation.
        /// </summary>
        public string? RelativePath { get; set; } = null;

        /// <summary>
        /// The new absolute path for the file on the server, if relocation
        /// was successful. Remember to check <see cref="IsSuccess"/> to see
        /// the status of the relocation.
        /// </summary>
        public string? AbsolutePath { get; set; } = null;

        /// <summary>
        /// Indicates whether the file was relocated successfully.
        /// </summary>
        public bool IsSuccess { get; set; } = false;

        /// <summary>
        /// Indicates whether the file was actually relocated from one
        /// location to another, or if it was already at it's correct
        /// location.
        /// </summary>
        public bool IsRelocated { get; set; } = false;

        /// <summary>
        /// Indicates if the result is only a preview and the file has not
        /// actually been relocated yet.
        /// </summary>
        public bool IsPreview { get; set; } = false;
    }

    public static class Input
    {
        public class ModifyRenamerBody
        {
            /// <summary>
            /// If the renamer is enabled.
            /// </summary>
            public bool? Enabled { get; set; } = null;

            /// <summary>
            /// Lower numbers mean higher priority. Will be null if a priority is not set yet.
            /// </summary>
            public int? Priority { get; set; } = null;

            public ModifyRenamerBody() { }

            public ModifyRenamerBody(string name)
            {
                var settings = Utils.SettingsProvider.GetSettings();
                Enabled = !settings.Plugins.EnabledRenamers.TryGetValue(name, out var enabled) || enabled;
                Priority = settings.Plugins.RenamerPriorities.TryGetValue(name, out var priority) ? priority : null;
            }

            public Renamer MergeWithExisting(string name, (Type type, string description, string value) value)
            {
                // Get the settings object.
                var settings = Utils.SettingsProvider.GetSettings();

                // Set the enabled status.
                if (Enabled.HasValue)
                {
                    if (!settings.Plugins.EnabledRenamers.TryAdd(name, Enabled.Value))
                        settings.Plugins.EnabledRenamers[name] = Enabled.Value;
                }

                // Set the priority.
                if (Priority.HasValue)
                {
                    if (!settings.Plugins.RenamerPriorities.TryAdd(name, Priority.Value))
                        settings.Plugins.RenamerPriorities[name] = Priority.Value;
                }

                // Save the settings.
                Utils.SettingsProvider.SaveSettings();

                return new Renamer(name, value);
            }
        }

        public class ModifyScriptBody
        {

            /// <summary>
            /// Script name. Must be unique across all scripts.
            /// </summary>
            [Required]
            public string Name { get; set; } = "";

            /// <summary>
            /// The name of the <see cref="Models.Shoko.Renamer"/> this script
            /// belongs to.
            /// </summary>
            [Required]
            public string RenamerName { get; set; } = "";

            /// <summary>
            /// Determines if the script should ran automatically on import if the renamer is enabled.
            /// </summary>
            [Required]
            public bool EnabledOnImport { get; set; } = false;

            /// <summary>
            /// The script body.
            /// </summary>
            public string? Body { get; set; } = null;

            public ModifyScriptBody() { }

            public ModifyScriptBody(RenameScript script)
            {
                Name = script.ScriptName;
                RenamerName = script.RenamerType;
                EnabledOnImport = script.IsEnabledOnImport == 1;
                Body = !string.IsNullOrWhiteSpace(script.Script) ? script.Script : null;
            }

            public Script MergeWithExisting(RenameScript script)
            {
                script.ScriptName = Name;
                script.RenamerType = RenamerName;
                script.IsEnabledOnImport = EnabledOnImport ? 1 : 0;
                script.Script = Body ?? "";

                // Check to make sure we multiple scripts enable on import, since
                // only one can be selected.
                if (EnabledOnImport)
                {
                    var allScripts = RepoFactory.RenameScript.GetAll();
                    foreach (var s in allScripts)
                    {
                        if (s.IsEnabledOnImport == 1 &&
                            (script.RenameScriptID == 0 || (script.RenameScriptID != s.RenameScriptID)))
                        {
                            s.IsEnabledOnImport = 0;
                            RepoFactory.RenameScript.Save(s);
                        }
                    }
                }

                RepoFactory.RenameScript.Save(script);
                return new Script(script);
            }
        }

        public class NewScriptBody
        {
            /// <summary>
            /// Script name. Must be unique across all scripts.
            /// </summary>
            [Required]
            public string Name { get; set; } = "";

            /// <summary>
            /// The name of the <see cref="Models.Shoko.Renamer"/> this script
            /// belongs to.
            /// </summary>
            [Required]
            public string RenamerName { get; set; } = "";

            /// <summary>
            /// Determines if the script should be automatically ran on import
            /// if the renamer is enabled.
            /// </summary>
            [Required]
            public bool EnabledOnImport { get; set; } = false;

            /// <summary>
            /// The script body.
            /// </summary>
            public string? Body { get; set; } = null;
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
}
