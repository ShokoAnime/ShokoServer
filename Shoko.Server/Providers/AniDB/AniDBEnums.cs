using System;
using Shoko.Models.Enums;

namespace Shoko.Server.Providers.AniDB
{
    // This file will have a lot of repeated enums, but it's more specific, and eventually, the others should be removed
    [Flags]
    public enum GetFile_State
    {
        None = 0,
        CRCMatch = 1, //file matched official CRC (displayed with green background in AniDB)
        CRCErr = 2, // file DID NOT match official CRC (displayed with red background in AniDB)
        IsV2 = 4, // file is version 2
        IsV3 = 8, // file is version 3
        IsV4 = 16, // file is version 4
        IsV5 = 32, // file is version 5
        Uncensored = 64, // file is uncensored
        Censored = 128, // file is censored
        Chaptered = 4096, // file is chaptered, 0 means both not set and false
    }

    public enum GetFile_Quality
    {
        Unknown,
        VeryHigh,
        High,
        Medium,
        Low,
        VeryLow,
        Corrupted,
        EyeCancer,
    }

    public enum GetFile_Source
    {
        Unknown,
        TV,
        Web,
        DVD,
        BluRay,
        VHS,
        HDDVD,
        HKDVD,
        HDTV,
        DTV,
        Camcorder,
        VCD,
        SVCD,
        LaserDisc,
    }
    
    public enum UpdateType
    {
        None = 0,
        UDPBan = 1,
        HTTPBan = 2,
        InvalidSession = 3,
        OverloadBackoff = 4,
        WaitingOnResponse = 5,
    }
    
    public enum MyList_State
    {
        Unknown = 0,
        HDD = 1,
        Disk = 2,
        Deleted = 3,
        Remote = 4,
    }

    public enum MyList_FileState
    {
        Normal = 0,
        Corrupted = 1,
        SelfEdited = 2,
        SelfRipped = 10,
        OnDVD = 11,
        OnVHS = 12,
        OnTV = 13,
        InTheaters = 14,
        Streamed = 15,
        Other = 100,
    }
    
    public enum UDPReturnCode
    {
        LOGIN_ACCEPTED = 200,
        LOGIN_ACCEPTED_NEW_VERSION = 201,
        LOGGED_OUT = 203,
        RESOURCE = 205,
        STATS = 206,
        TOP = 207,
        UPTIME = 208,
        ENCRYPTION_ENABLED = 209,
        MYLIST_ENTRY_ADDED = 210,
        MYLIST_ENTRY_DELETED = 211,
        ADDED_FILE = 214,
        ADDED_STREAM = 215,
        EXPORT_QUEUED = 217,
        EXPORT_CANCELLED = 218,
        ENCODING_CHANGED = 219,
        FILE = 220,
        MYLIST = 221,
        MYLIST_STATS = 222,
        WISHLIST = 223,
        NOTIFICATION = 224,
        GROUP_STATUS = 225,
        WISHLIST_ENTRY_ADDED = 226,
        WISHLIST_ENTRY_DELETED = 227,
        WISHLIST_ENTRY_UPDATED = 228,
        MULTIPLE_WISHLIST = 229,
        ANIME = 230,
        ANIME_BEST_MATCH = 231,
        RANDOM_ANIME = 232,
        ANIME_DESCRIPTION = 233,
        REVIEW = 234,
        CHARACTER = 235,
        SONG = 236,
        ANIMETAG = 237,
        CHARACTERTAG = 238,
        EPISODE = 240,
        UPDATED = 243,
        TITLE = 244,
        CREATOR = 245,
        NOTIFICATION_ENTRY_ADDED = 246,
        NOTIFICATION_ENTRY_DELETED = 247,
        NOTIFICATION_ENTRY_UPDATE = 248,
        MULTIPLE_NOTIFICATION = 249,
        GROUP = 250,
        CATEGORY = 251,
        BUDDY_LIST = 253,
        BUDDY_STATE = 254,
        BUDDY_ADDED = 255,
        BUDDY_DELETED = 256,
        BUDDY_ACCEPTED = 257,
        BUDDY_DENIED = 258,
        VOTED = 260,
        VOTE_FOUND = 261,
        VOTE_UPDATED = 262,
        VOTE_REVOKED = 263,
        HOT_ANIME = 265,
        RANDOM_RECOMMENDATION = 266,
        RANDOM_SIMILAR = 267,
        NOTIFICATION_ENABLED = 270,
        NOTIFYACK_SUCCESSFUL_MESSAGE = 281,
        NOTIFYACK_SUCCESSFUL_NOTIFIATION = 282,
        NOTIFICATION_STATE = 290,
        NOTIFYLIST = 291,
        NOTIFYGET_MESSAGE = 292,
        NOTIFYGET_NOTIFY = 293,
        SENDMESSAGE_SUCCESSFUL = 294,
        USER_ID = 295,
        CALENDAR = 297,

        PONG = 300,
        AUTHPONG = 301,
        NO_SUCH_RESOURCE = 305,
        API_PASSWORD_NOT_DEFINED = 309,
        FILE_ALREADY_IN_MYLIST = 310,
        MYLIST_ENTRY_EDITED = 311,
        MULTIPLE_MYLIST_ENTRIES = 312,
        WATCHED = 313,
        SIZE_HASH_EXISTS = 314,
        INVALID_DATA = 315,
        STREAMNOID_USED = 316,
        EXPORT_NO_SUCH_TEMPLATE = 317,
        EXPORT_ALREADY_IN_QUEUE = 318,
        EXPORT_NO_EXPORT_QUEUED_OR_IS_PROCESSING = 319,
        NO_SUCH_FILE = 320,
        NO_SUCH_ENTRY = 321,
        MULTIPLE_FILES_FOUND = 322,
        NO_SUCH_WISHLIST = 323,
        NO_SUCH_NOTIFICATION = 324,
        NO_GROUPS_FOUND = 325,
        NO_SUCH_ANIME = 330,
        NO_SUCH_DESCRIPTION = 333,
        NO_SUCH_REVIEW = 334,
        NO_SUCH_CHARACTER = 335,
        NO_SUCH_SONG = 336,
        NO_SUCH_ANIMETAG = 337,
        NO_SUCH_CHARACTERTAG = 338,
        NO_SUCH_EPISODE = 340,
        NO_SUCH_UPDATES = 343,
        NO_SUCH_TITLES = 344,
        NO_SUCH_CREATOR = 345,
        NO_SUCH_GROUP = 350,
        NO_SUCH_CATEGORY = 351,
        BUDDY_ALREADY_ADDED = 355,
        NO_SUCH_BUDDY = 356,
        BUDDY_ALREADY_ACCEPTED = 357,
        BUDDY_ALREADY_DENIED = 358,
        NO_SUCH_VOTE = 360,
        INVALID_VOTE_TYPE = 361,
        INVALID_VOTE_VALUE = 362,
        PERMVOTE_NOT_ALLOWED = 363,
        ALREADY_PERMVOTED = 364,
        HOT_ANIME_EMPTY = 365,
        RANDOM_RECOMMENDATION_EMPTY = 366,
        RANDOM_SIMILAR_EMPTY = 367,
        NOTIFICATION_DISABLED = 370,
        NO_SUCH_ENTRY_MESSAGE = 381,
        NO_SUCH_ENTRY_NOTIFICATION = 382,
        NO_SUCH_MESSAGE = 392,
        NO_SUCH_NOTIFY = 393,
        NO_SUCH_USER = 394,
        CALENDAR_EMPTY = 397,
        NO_CHANGES = 399,

        NOT_LOGGED_IN = 403,
        NO_SUCH_MYLIST_FILE = 410,
        NO_SUCH_MYLIST_ENTRY = 411,
        MYLIST_UNAVAILABLE = 412,

        LOGIN_FAILED = 500,
        LOGIN_FIRST = 501,
        ACCESS_DENIED = 502,
        CLIENT_VERSION_OUTDATED = 503,
        CLIENT_BANNED = 504,
        ILLEGAL_INPUT_OR_ACCESS_DENIED = 505,
        INVALID_SESSION = 506,
        NO_SUCH_ENCRYPTION_TYPE = 509,
        ENCODING_NOT_SUPPORTED = 519,
        BANNED = 555,
        UNKNOWN_COMMAND = 598,

        INTERNAL_SERVER_ERROR = 600,
        ANIDB_OUT_OF_SERVICE = 601,
        SERVER_BUSY = 602,
        NO_DATA = 603,
        TIMEOUT_DELAY_AND_RESUBMIT = 604,
        API_VIOLATION = 666,

        PUSHACK_CONFIRMED = 701,
        NO_SUCH_PACKET_PENDING = 702,

        VERSION = 998,
    }
    
    public enum VoteType
    {
        AnimePermanent = 1,
        AnimeTemporary = 2,
        Group = 3,
        Episode = 4,
    }

    public enum EpisodeType
    {
        Episode = 1,
        Credits = 2,
        Special = 3,
        Trailer = 4,
        Parody = 5,
        Other = 6,
    }

    public enum AnimeType
    {
        None = -1, // Not on AniDB, but for ease of processing
        Movie = 0,
        OVA = 1,
        TVSeries = 2,
        TVSpecial = 3,
        Web = 4,
        Other = 5,
    }
    
    /// <summary>
    /// Explains how the main entry relates to the related entry.
    /// </summary>
    public enum RelationType
    {
        /// <summary>
        /// The relation between the entries cannot be explained in simple terms.
        /// </summary>
        Other = 0,

        /// <summary>
        /// The related entry either continues, or expands upon the story of the main entry.
        /// </summary>
        Sequel = 1,

        /// <summary>
        /// The first story either continues, or expands upon the story of the related entry.
        /// </summary>
        Prequel = 2,
        
        /// <summary>
        /// The entries use the same setting, but follow different stories.
        /// </summary>
        SameSetting = 3,

        /// <summary>
        /// The entries use the same base story, but is set in alternate settings.
        /// </summary>
        AlternativeSetting = 4,

        /// <summary>
        /// Remake, basically
        /// </summary>
        AlternativeVersion = 5,

        /// <summary>
        /// It's a music video with featured content or something
        /// </summary>
        MusicVideo = 6,

        /// <summary>
        /// The entries tell different stories in different settings but otherwise shares some character(s).
        /// </summary>
        SharedCharacters = 7,

        /// <summary>
        /// The related entry is the main-story for the main entry, which is a side-story.
        /// </summary>
        MainStory = 8,

        /// <summary>
        /// The related entry is a longer version of the summarized events in the main entry.
        /// </summary>
        FullStory = 9,

        /// <summary>
        /// The related entry is a side-story for the main entry, which is the main-story.
        /// </summary>
        SideStory = 41,

        /// <summary>
        /// The related entry summarizes the events of the story in the main entry.
        /// </summary>
        Summary = 42,
    }

    public static class EnumExtensions
    {
        public static MyList_State GetMyList_State(this AniDBFile_State a) =>
            a switch
            {
                AniDBFile_State.Deleted => MyList_State.Deleted,
                AniDBFile_State.Disk => MyList_State.Disk,
                AniDBFile_State.Remote => MyList_State.Remote,
                AniDBFile_State.HDD => MyList_State.HDD,
                _ => MyList_State.Unknown,
            };
    }
}
