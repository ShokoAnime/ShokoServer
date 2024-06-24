using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Server.Renamer;

public class WebAOMSettings
{
    [RenamerSetting(Name = "Group Aware Sorting", Description = "Whether to place files in a folder structure based on the Shoko group structure")]
    public bool GroupAwareSorting { get; set; }

    [RenamerSetting(Type = RenamerSettingType.Code)]
    public string Script { get; set; }
}
