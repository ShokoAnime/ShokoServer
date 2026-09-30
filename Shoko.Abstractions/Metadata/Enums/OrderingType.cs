namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   What an ordering of a series' episodes follows.
/// </summary>
public enum OrderingType
{
    /// <summary>
    ///   The series' own seasons, the ordering every series has.
    /// </summary>
    Default = -1,

    /// <summary>
    ///   Unknown ordering type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///   Original air date.
    /// </summary>
    OriginalAirDate = 1,

    /// <summary>
    ///   Absolute ordering.
    /// </summary>
    Absolute = 2,

    /// <summary>
    ///   DVD ordering.
    /// </summary>
    DVD = 3,

    /// <summary>
    ///   Digital ordering.
    /// </summary>
    Digital = 4,

    /// <summary>
    ///   Web ordering. Aliased to Digital.
    /// </summary>
    Web = Digital,

    /// <summary>
    ///   Story arc ordering.
    /// </summary>
    StoryArc = 5,

    /// <summary>
    ///   Production ordering.
    /// </summary>
    Production = 6,

    /// <summary>
    ///   TV ordering.
    /// </summary>
    TV = 7,

    /// <summary>
    ///   An ordering a user made on this server.
    /// </summary>
    User = 8,
}
