
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   The type of data entity.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum DataEntityType : byte
{
    /// <summary>
    ///   Catch-all for all unknown types.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///   Any type of collection to hold entries.
    /// </summary>
    Collection = 1,

    /// <summary>
    ///   Alias for <see cref="Collection"/>.
    /// </summary>
    Group = Collection,

    /// <summary>
    ///   Alias for <see cref="Collection"/>.
    /// </summary>
    BoxSet = Collection,

    /// <summary>
    ///   Any kind of series.
    /// </summary>
    Series = 2,

    /// <summary>
    ///   Alias for <see cref="Series"/>.
    /// </summary>
    Anime = Series,

    /// <summary>
    ///   Alias for <see cref="Series"/>.
    /// </summary>
    Show = Series,

    /// <summary>
    ///   Any kind of season within a series.
    /// </summary>
    Season = 3,

    /// <summary>
    ///   Any kind of episodes within seasons and/or series.
    /// </summary>
    Episode = 4,

    /// <summary>
    ///   Any kind of movies.
    /// </summary>
    Movie = 5,

    /// <summary>
    ///   Any kind of videos.
    /// </summary>
    Video = 6,

    /// <summary>
    ///   Any kind of companies which have worked on any work.
    /// </summary>
    Company = 7,

    /// <summary>
    ///   A studio. Alias for <see cref="Company"/>.
    /// </summary>
    Studio = Company,

    /// <summary>
    ///   
    /// </summary>
    Network = 8,

    /// <summary>
    ///   Any kind of creator
    /// </summary>
    Creator = 9,

    /// <summary>
    /// Person. Alias for <see cref="Creator"/>.
    /// </summary>
    Person = Creator,

    /// <summary>
    /// Staff member. Alias for <see cref="Creator"/>.
    /// </summary>
    StaffMember = Creator,

    /// <summary>
    ///   Any kind of character within a work.
    /// </summary>
    Character = 10,

    /// <summary>
    ///   Any kind of user.
    /// </summary>
    User = 11,
}
