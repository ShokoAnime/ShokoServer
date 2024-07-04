#nullable enable
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Enums;
using DefaultSetting = Shoko.Server.API.v3.Models.Shoko.Relocation.Setting;

namespace Shoko.Server.API.v3.Models.Shoko.Relocation;

public class Renamer
{
    /// <summary>
    /// The ID of the renamer. Not used anywhere in the API, but could be useful as a list key
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
    public List<Setting>? Settings { get; set; }

    /// <summary>
    /// The default settings for the renamer, if any were provided
    /// </summary>
    public List<DefaultSetting>? DefaultSettings { get; set; }

    public class Setting
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string? Description { get; set; }
        public CodeLanguage? Language { get; set; }
        public RenamerSettingType SettingType { get; set; }
    }

}
