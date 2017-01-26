using System.Runtime.Serialization;


namespace Shoko.Models.PlexAndKodi
{
    [DataContract]
    public enum JMMType
    {
        [EnumMember]
        GroupFilter,
        [EnumMember]
        GroupUnsort,
        [EnumMember]
        Group,
        [EnumMember]
        Serie,
        [EnumMember]
        EpisodeType,
        [EnumMember]
        Episode,
        [EnumMember]
        File,
        [EnumMember]
        Playlist,
        [EnumMember]
        FakeIosThumb
    }


}