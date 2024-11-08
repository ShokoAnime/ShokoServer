using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_ReleaseGroup : IReleaseGroup
{
    public int AniDB_ReleaseGroupID { get; set; }

    public int GroupID { get; set; }

    public int Rating { get; set; }

    public int Votes { get; set; }

    public int AnimeCount { get; set; }

    public int FileCount { get; set; }

    public string? GroupName { get; set; }

    public string? GroupNameShort { get; set; }

    public string? IRCChannel { get; set; }

    public string? IRCServer { get; set; }

    public string? URL { get; set; }

    public string? Picname { get; set; }

    #region IMetadata Implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.AniDB;

    int IMetadata<int>.ID => GroupID;

    #endregion

    #region IReleaseGroup Implementation

    string? IReleaseGroup.Name => GroupName;

    string? IReleaseGroup.ShortName => GroupNameShort;

    #endregion
}
