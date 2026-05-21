
namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   The data source.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum DataSource : byte
{
    /// <summary>
    /// Locally Generated (Automatic).
    /// </summary>
    LocallyGenerated = 0xFC,

    /// <summary>
    /// No Source.
    /// </summary>
    None = 0xFD,

    /// <summary>
    /// User (Manual).
    /// </summary>
    User = 0xFE,

    /// <summary>
    /// Shoko.
    /// </summary>
    Shoko = 0xFF,

    /// <summary>
    /// AniDB.
    /// </summary>
    AniDB = 0x00,

    /// <summary>
    /// The Movie Database (TMDb).
    /// </summary>
    TMDB = 0x01,

    /// <summary>
    /// The Tv DataBase (TvDB).
    /// </summary>
    TvDB = 0x02,

    /// <summary>
    /// AniList.
    /// </summary>
    AniList = 0x03,

    /// <summary>
    /// Animeshon.
    /// </summary>
    Animeshon = 0x04,

    /// <summary>
    ///   Kitsu (previously Hummingbird).
    /// </summary>
    Kitsu = 0x05,

    /// <summary>
    ///   My Anime List (MAL).
    /// </summary>
    MAL = 0x06,

    /// <summary>
    ///   Fanart.TV.
    /// </summary>
    FanartTV = 0x07,

    /// <summary>
    ///   Internet Movie Database (IMDb).
    /// </summary>
    IMDB = 0x08,

    /// <summary>
    ///   The Open Movie Database (OMDb).
    /// </summary>
    OMDB = 0x09,

    /// <summary>
    ///   Trakt.TV.
    /// </summary>
    TraktTv = 0x0A,

    /// <summary>
    ///   The Poster Database (TPDb).
    /// </summary>
    TPDB = 0x0B,

    /// <summary>
    ///   MediUX.
    /// </summary>
    MediUX = 0x0C,

    /// <summary>
    ///   SimKL.
    /// </summary>
    SimKL = 0x0D,
}

/// <summary>
///   Extension methods for <see cref="DataSource"/>.
/// </summary>
public static class DataSourceExtensions
{
    extension(DataSource source)
    {
        /// <summary>
        ///   Gets a value indicating whether the source is local.
        /// </summary>
        public bool IsLocal => source is DataSource.None or DataSource.LocallyGenerated or DataSource.User or DataSource.Shoko;

        /// <summary>
        ///   Gets a value indicating whether the source is remote.
        /// </summary>
        public bool IsRemote => !source.IsLocal;
    }
}
