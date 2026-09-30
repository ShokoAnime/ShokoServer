using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Server;


public enum ScheduledUpdateFrequency
{
    [Display(Name = "Never")]
    Never = 1,
    [Display(Name = "Every 6 hours")]
    HoursSix = 2,
    [Display(Name = "Every 12 hours")]
    HoursTwelve = 3,
    [Display(Name = "Every 24 hours")]
    Daily = 4,
    [Display(Name = "Once a week")]
    WeekOne = 5,
    [Display(Name = "Once a month")]
    MonthOne = 6,
    [Display(Name = "Every hour")]
    EveryHour = 7,
}

public static class ScheduledUpdateFrequencyExtensions
{
    extension(ScheduledUpdateFrequency freq)
    {
        public int Hours
            => freq switch
            {
                ScheduledUpdateFrequency.HoursSix => 6,
                ScheduledUpdateFrequency.HoursTwelve => 12,
                ScheduledUpdateFrequency.Daily => 24,
                ScheduledUpdateFrequency.WeekOne => 24 * 7,
                ScheduledUpdateFrequency.MonthOne => 24 * 30,
                ScheduledUpdateFrequency.EveryHour => 1,
                _ => int.MaxValue,
            };
    }
}

public enum ScheduledUpdateType
{
    AniDBCalendar = 1,
    AniDBUpdates = 3,
    AniDBMylistSync = 5,
    AniDBFileUpdates = 10,
    AniDBNotify = 15,
    PluginUpdates = 16,
    EpisodeAiringNotifications = 17,
}

public enum AniDBNotifyType
{
    Message = 1,
    Notification = 2,
}

public enum AniDBMessageType
{
    Normal = 0,
    Anonymous = 1,
    System = 2,
    Moderator = 3,
}

/// <summary>
/// Read status of messages and notifications
/// </summary>
[Flags]
public enum AniDBMessageFlags
{
    /// <summary>
    /// No flags
    /// </summary>
    None = 0,

    /// <summary>
    /// Marked as read on AniDB
    /// </summary>
    ReadOnAniDB = 1,

    /// <summary>
    /// Marked as read locally
    /// </summary>
    ReadOnShoko = 2,

    /// <summary>
    /// Is a file moved notification
    /// </summary>
    FileMoved = 4,

    /// <summary>
    /// Has the file move been handled
    /// </summary>
    FileMoveHandled = 8
}

// TODO: Simplify these values to not be power of 2 and instead be increments of 1. We don't need flag support anymore.
[JsonConverter(typeof(StringEnumConverter))]
public enum ForeignEntityType
{
    None = 0,
    Collection = 1,
    Movie = 2,
    Show = 4,
    Season = 8,
    Episode = 16,
    Company = 32,
    // Studio = 64, // Unused
    Network = 128,
    Person = 256,
    // Character = 512, // Unused
}

public static class ForeignEntityTypeExtensions
{
    extension(ForeignEntityType type)
    {
        public MetadataEntityType? DataType => type switch
        {
            ForeignEntityType.Collection => MetadataEntityType.Collection,
            ForeignEntityType.Movie => MetadataEntityType.Movie,
            ForeignEntityType.Show => MetadataEntityType.Series,
            ForeignEntityType.Season => MetadataEntityType.Season,
            ForeignEntityType.Episode => MetadataEntityType.Episode,
            ForeignEntityType.Company => MetadataEntityType.Studio,
            ForeignEntityType.Network => MetadataEntityType.Network,
            ForeignEntityType.Person => MetadataEntityType.Creator,
            _ => null,
        };
    }

    extension(MetadataEntityType? type)
    {
        public ForeignEntityType ForeignType => type switch
        {
            _ when type == MetadataEntityType.Collection => ForeignEntityType.Collection,
            _ when type == MetadataEntityType.Movie => ForeignEntityType.Movie,
            _ when type == MetadataEntityType.Series => ForeignEntityType.Show,
            _ when type == MetadataEntityType.Season => ForeignEntityType.Season,
            _ when type == MetadataEntityType.Episode => ForeignEntityType.Episode,
            _ when type == MetadataEntityType.Studio => ForeignEntityType.Company,
            _ when type == MetadataEntityType.Network => ForeignEntityType.Network,
            _ when type == MetadataEntityType.Creator => ForeignEntityType.Person,
            _ => ForeignEntityType.None,
        };
    }
}

[JsonConverter(typeof(StringEnumConverter))]
public enum CreatorRoleType
{
    /// <summary>
    /// Voice actor or voice actress.
    /// </summary>
    Actor,

    /// <summary>
    /// This can be anything involved in writing the show.
    /// </summary>
    Staff,

    /// <summary>
    /// The studio responsible for publishing the show.
    /// </summary>
    Studio,

    /// <summary>
    /// The main producer(s) for the show.
    /// </summary>
    Producer,

    /// <summary>
    /// Direction.
    /// </summary>
    Director,

    /// <summary>
    /// Series Composition.
    /// </summary>
    SeriesComposer,

    /// <summary>
    /// Character Design.
    /// </summary>
    CharacterDesign,

    /// <summary>
    /// Music composer.
    /// </summary>
    Music,

    /// <summary>
    /// Responsible for the creation of the source work this show is detrived from.
    /// </summary>
    SourceWork,
}

[JsonConverter(typeof(StringEnumConverter))]
public enum CharacterAppearanceType
{
    Unknown = 0,
    Main_Character,
    Minor_Character,
    Background_Character,
    Cameo
}

[Flags]
public enum FilterPresetType
{
    None = 0,
    UserDefined = 1,
    ContinueWatching = 2,
    All = 4,
    Directory = 8,
    Tag = 16,
    Year = 32,
    Season = 64,
}
