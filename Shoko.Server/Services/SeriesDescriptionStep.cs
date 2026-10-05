namespace Shoko.Server.Services;

/// <summary>
///   The step of the walk that chose a Shoko series' description.
/// </summary>
public enum SeriesDescriptionStep
{
    /// <summary>
    ///   Nothing was found in any preferred language.
    /// </summary>
    None = 0,

    /// <summary>
    ///   A user's overall pick.
    /// </summary>
    OverallPreference = 1,

    /// <summary>
    ///   A source's best-fitting entry: its film, the whole show, the linked
    ///   season, or AniDB's description with a synopsis in it.
    /// </summary>
    Entry = 2,

    /// <summary>
    ///   The show's description, read at once because the linked first season
    ///   had none in the language.
    /// </summary>
    FirstSeasonShow = 3,

    /// <summary>
    ///   The show's description, read because the linked later season had
    ///   none in the language and no source's best entry had one either.
    /// </summary>
    LaterSeasonShow = 4,

    /// <summary>
    ///   AniDB's description holding only notes, the last resort in the
    ///   language.
    /// </summary>
    AnidbNote = 5,
}
