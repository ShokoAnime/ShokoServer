using Newtonsoft.Json;
using Shoko.Models.Server;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Repositories;
using System;
using System.ComponentModel.DataAnnotations;
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
    }
}
