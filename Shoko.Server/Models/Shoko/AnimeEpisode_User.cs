using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Abstractions.UserData;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Shoko;

public class AnimeEpisode_User : IEpisodeUserData
{
    #region Server DB columns

    /// <summary>
    /// Local DB Row ID.
    /// </summary>
    public int AnimeEpisode_UserID { get; set; }

    /// <summary>
    /// Shoko User ID.
    /// </summary>
    public int JMMUserID { get; set; }

    /// <summary>
    /// Shoko Episode ID.
    /// </summary>
    public int AnimeEpisodeID { get; set; }

    /// <summary>
    /// Shoko Series ID.
    /// </summary>
    public int AnimeSeriesID { get; set; }

    /// <summary>
    /// The date and time the episode was last watched.
    /// </summary>
    public DateTime? WatchedDate { get; set; }

    /// <summary>
    ///  How many times videos have been started/played for the episode. Only used by Shoko Desktop and APIv1.
    /// </summary>
    public int PlayedCount { get; set; }

    /// <summary>
    /// How many videos have been played to completion for the episode.
    /// </summary>
    public int WatchedCount { get; set; }

    /// <summary>
    ///  How many times videos have been stopped for the episode. Only used by Shoko Desktop and APIv1.
    /// </summary>
    public int StoppedCount { get; set; }

    /// <summary>
    /// Indicates that the user has watched the episode to completion at least once.
    /// </summary>
    public bool IsWatched => WatchedCount > 0;

    /// <summary>
    /// Indicates that the user has marked the episode as favorite.
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Indicates that the user has rated the episode.
    /// </summary>
    [MemberNotNullWhen(true, nameof(AbsoluteUserRating), nameof(UserRating))]
    public bool HasUserRating => AbsoluteUserRating.HasValue;

    private int? _absoluteUserRating;

    /// <summary>
    /// The user rating, on a scale of 100-1000, where a rating of 8.32 on the 1-10 scale becomes 832.
    /// </summary>
    public int? AbsoluteUserRating
    {
        get => _absoluteUserRating;
        set
        {
            if (value is -1)
                value = null;
            if (value.HasValue && value % 10 != 0)
                value = (int)(Math.Round((double)value.Value / 10, 0, MidpointRounding.AwayFromZero) * 10);
            if (value is not null && (value < 100 || value > 1000))
                throw new ArgumentOutOfRangeException(nameof(AbsoluteUserRating), "User rating must be between 1 and 10, or -1 or null for no rating.");

            _absoluteUserRating = value;
        }
    }

    /// <summary>
    /// The user rating, on a scale of 1-10.
    /// </summary>
    public double? UserRating
    {
        get => AbsoluteUserRating.HasValue ? Math.Round(AbsoluteUserRating.Value / 100D, 2) : null;
        set => AbsoluteUserRating = value.HasValue ? (int)Math.Round(value.Value * 100D, 0) : null;
    }

    /// <summary>
    ///   The unique tags assigned to the user by the series
    /// </summary>
    public List<string> UserTags { get; set; } = [];

    /// <summary>
    /// The last time the episode user data was updated by the user. E.g. setting
    /// the user rating or watching a video linked to the episode.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    #endregion

    public AnimeSeries? AnimeSeries => RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);

    public AnimeEpisode? AnimeEpisode => RepoFactory.AnimeEpisode.GetByID(AnimeEpisodeID);

    #region IUserData Implementation

    int IUserData.UserID => JMMUserID;

    DateTime IUserData.LastUpdatedAt => LastUpdated;

    IUser IUserData.User => RepoFactory.JMMUser.GetByID(JMMUserID) ??
        throw new NullReferenceException($"Unable to find IUser with the given id. (User={JMMUserID})");

    #endregion

    #region IEpisodeUserData Implementation

    int IEpisodeUserData.SeriesID => AnimeSeriesID;

    int IEpisodeUserData.EpisodeID => AnimeEpisodeID;

    int IEpisodeUserData.PlaybackCount => WatchedCount;

    DateTime? IEpisodeUserData.LastPlayedAt => WatchedDate;

    // Skim it at runtime until we decide to cache it in the DB.
    DateTime? IEpisodeUserData.LastVideoPlayedAt
        => (AnimeEpisode?.VideoLocals ?? [])
            .Select(video => RepoFactory.VideoLocalUser.GetByUserAndVideoLocalID(JMMUserID, video.VideoLocalID)?.WatchedDate)
            .WhereNotNull()
            .OrderDescending()
            .FirstOrDefault();

    // Skim it at runtime until we decide to cache it in the DB.
    DateTime? IEpisodeUserData.LastVideoUpdatedAt
        => (AnimeEpisode?.VideoLocals ?? [])
            .Select(video => RepoFactory.VideoLocalUser.GetByUserAndVideoLocalID(JMMUserID, video.VideoLocalID)?.LastUpdated)
            .WhereNotNull()
            .OrderDescending()
            .FirstOrDefault();

    IReadOnlyList<string> IEpisodeUserData.UserTags => UserTags;

    IShokoSeries? IEpisodeUserData.Series => AnimeSeries;

    IShokoEpisode? IEpisodeUserData.Episode => AnimeEpisode;

    #endregion
}
