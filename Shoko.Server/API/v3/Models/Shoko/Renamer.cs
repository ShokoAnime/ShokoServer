using Newtonsoft.Json;
using Shoko.Models.Server;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Repositories;
using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class Renamer : BaseModel
{
    public Renamer(string name, (Type type, string description) value)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var scripts = RepoFactory.RenameScript.GetByRenamerType(name);
        Name = name;
        Size = scripts.Count;
        Description = value.description;
        Enabled = settings.Plugins.EnabledRenamers.TryGetValue(name, out var enabled) ? enabled : true;
        Priority = settings.Plugins.RenamerPriorities.TryGetValue(Name, out var priority) ? priority : null;
    }

    /// <summary>
    /// A short description about the renamer.
    /// </summary>
    [Required]
    public string Description { get; set; }

    /// <summary>
    /// If the renamer is enabled.
    /// </summary>
    [Required]
    public bool Enabled { get; set; }

    /// <summary>
    /// Lower numbers mean higher priority. Will be null if a priority is not set yet.
    /// </summary>
    [Required]
    public int? Priority { get; set; }

    public class ModifyRenamerBody
    {
        /// <summary>
        /// If the renamer is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Lower numbers mean higher priority. Will be null if a priority is not set yet.
        /// </summary>
        public int? Priority { get; set; } = null;

        public ModifyRenamerBody() { }

        public ModifyRenamerBody(string name)
        {
            var settings = Utils.SettingsProvider.GetSettings();
            Enabled = settings.Plugins.EnabledRenamers.TryGetValue(name, out var enabled) ? enabled : true;
            Priority = settings.Plugins.RenamerPriorities.TryGetValue(name, out var priority) ? priority : null;
        }

        public Renamer MergeWithExisting(string name, (Type type, string description) value)
        {
            // Get the settings object.
            var settings = Utils.SettingsProvider.GetSettings();

            // Set the enabled status.
            if (!settings.Plugins.EnabledRenamers.TryAdd(name, Enabled))
                settings.Plugins.EnabledRenamers[name] = Enabled;

            // Set the priority.
            if (Priority.HasValue)
                if (!settings.Plugins.RenamerPriorities.TryAdd(name, Priority.Value))
                    settings.Plugins.RenamerPriorities[name] = Priority.Value;
            else
                settings.Plugins.RenamerPriorities.Remove(name);

            // Save the settings.
            Utils.SettingsProvider.SaveSettings();

            return new Renamer(name, value);
        }
    }

    public class Script
    {
        public Script(RenameScript script)
        {
            ID = script.RenameScriptID;
            Name = script.ScriptName;
            RenamerName = script.RenamerType;
            RenamerIsAvailable = RenameFileHelper.Renamers.ContainsKey(script.RenamerType);
            EnabledOnImport = script.IsEnabledOnImport == 1;
            Body = !string.IsNullOrWhiteSpace(script.Script) ? script.Script : null;
        }

        /// <summary>
        /// Script ID.
        /// </summary>
        [Required]
        public int ID { get; set; }

        /// <summary>
        /// Script name. Must be unique across all scripts.
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// The last known name of the <see cref="Models.Shoko.Renamer"/> this script belongs to.
        /// </summary>
        [Required]
        public string RenamerName { get; set; }

        /// <summary>
        /// True if the renamer is locally available.
        /// </summary>
        [Required]
        public bool RenamerIsAvailable { get; set; }

        /// <summary>
        /// Determines if the script should ran automatically on import if the renamer is enabled.
        /// </summary>
        [Required]
        public bool EnabledOnImport { get; set; }

        /// <summary>
        /// The script body.
        /// </summary>
        [Required]
        public string? Body { get; set; }
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
        [Required]
        public string? Body { get; set; } = null;

        public ModifyScriptBody() {}

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
