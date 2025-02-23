using System;
using System.Runtime.Serialization;

namespace Shoko.Models.PlexAndKodi
{
    [Serializable]
    [DataContract]
    public enum AnimeTypes
    {
        [EnumMember]
        MediaContainer,
        [EnumMember]
        AnimeGroup,
        [EnumMember]
        AnimeSerie,
        [EnumMember]
        AnimeEpisode,
        [EnumMember]
        AnimeFile,
        [EnumMember]
        AnimeGroupFilter,
        [EnumMember]
        AnimePlaylist,
        [EnumMember]
        AnimeUnsort
    }
}