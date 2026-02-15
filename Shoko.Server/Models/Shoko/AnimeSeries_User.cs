using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Abstractions.UserData;
using Shoko.Abstractions.UserData.Enums;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.Shoko;

public class AnimeSeries_User : ISeriesUserData
{
    /// <summary>
    ///   Local DB Row ID.
    /// </summary>
    public int AnimeSeries_UserID { get; set; }

    /// <summary>
    ///   Shoko User ID.
    /// </summary>
    public int JMMUserID { get; set; }

    /// <summary>
    ///   Shoko Series ID.
    /// </summary>
    public int AnimeSeriesID { get; set; }

    /// <summary>
    ///   AniDB Anime ID, if available.
    /// </summary>
    public int? AnidbAnimeID => RepoFactory.AnimeSeries.GetByID(AnimeSeriesID)?.AniDB_ID;

    /// <inheritdoc />
    public bool IsFavorite { get; set; }

    /// <inheritdoc />
    public int UnwatchedEpisodeCount { get; set; }

    /// <inheritdoc />
    public int HiddenUnwatchedEpisodeCount { get; set; }

    /// <inheritdoc />
    public int WatchedEpisodeCount { get; set; }

    /// <summary>
    ///   The date and time the series was last watched.
    /// </summary>
    public DateTime? WatchedDate { get; set; }

    /// <summary>
    ///   How many times videos have been started/played for the series. Only
    ///   used by Shoko Desktop and APIv1.
    /// </summary>
    public int PlayedCount { get; set; }

    /// <summary>
    ///   How many videos have been played to completion for the series.
    /// </summary>
    public int WatchedCount { get; set; }

    /// <summary>
    ///   How many times videos have been stopped for the series. Only used by
    ///   Shoko Desktop and APIv1.
    /// </summary>
    public int StoppedCount { get; set; }

    /// <summary>
    ///   The last time an episode was updated, regardless of if it was watched
    ///   to completion or not. Used to determine continue watching and next-up
    ///   order for the series and on the dashboard.
    /// </summary>
    public DateTime? LastEpisodeUpdate { get; set; }

    /// <summary>
    ///   The last time an video was updated, regardless of if it was watched
    ///   to completion or not.
    /// </summary>
    public DateTime? LastVideoUpdate { get; set; }

    /// <summary>
    ///   Indicates that the user has rated the series.
    /// </summary>
    [MemberNotNullWhen(true, nameof(AbsoluteUserRating), nameof(UserRating), nameof(UserRatingVoteType))]
    public bool HasUserRating => AbsoluteUserRating.HasValue && UserRatingVoteType.HasValue;

    private int? _absoluteUserRating;

    /// <summary>
    ///   The user rating, on a scale of 100-1000, where a rating of 8.32 on the
    ///   1-10 scale becomes 832, or <c>null</c> if unrated.
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
            if (!_absoluteUserRating.HasValue)
                _userRatingVoteType = null;
            else if (!UserRatingVoteType.HasValue)
                _userRatingVoteType = SeriesVoteType.Temporary;
        }
    }

    /// <summary>
    ///   The user rating, on a scale of 1-10, or <c>null</c> if unrated.
    /// </summary>
    public double? UserRating
    {
        get => AbsoluteUserRating.HasValue ? Math.Round(AbsoluteUserRating.Value / 100D, 2) : null;
        set => AbsoluteUserRating = value.HasValue ? (int)Math.Round(value.Value * 100D, 0) : null;
    }

    private SeriesVoteType? _userRatingVoteType;

    /// <summary>
    ///   The user rating vote type.
    /// </summary>
    public SeriesVoteType? UserRatingVoteType
    {
        get => _userRatingVoteType;
        set
        {
            _userRatingVoteType = value;
            if (!_userRatingVoteType.HasValue)
                _absoluteUserRating = null;
            else if (!AbsoluteUserRating.HasValue)
                _absoluteUserRating = 0;
        }
    }

    /// <summary>
    ///   The unique tags assigned to the user by the series
    /// </summary>
    public List<string> UserTags { get; set; } = [];

    /// <summary>
    ///   The last time the series user data was updated by the user. E.g.
    ///   setting the user rating or watching an episode.
    /// </summary>
    public DateTime LastUpdated { get; set; }

    /// <summary>
    ///   The Shoko Series, if available.
    /// </summary>
    public AnimeSeries? AnimeSeries => RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);

    #region IUserData Implementation

    int IUserData.UserID => JMMUserID;

    DateTime IUserData.LastUpdatedAt => LastUpdated;

    IUser IUserData.User => RepoFactory.JMMUser.GetByID(JMMUserID) ??
        throw new NullReferenceException($"Unable to find IUser with the given id. (User={JMMUserID})");

    #endregion

    #region ISeriesUserData Implementation

    int ISeriesUserData.SeriesID => AnimeSeriesID;

    int ISeriesUserData.VideoPlaybackCount => WatchedCount;

    DateTime? ISeriesUserData.LastEpisodePlayedAt => WatchedDate;

    // Skim it at runtime until we decide to cache it in the DB.
    DateTime? ISeriesUserData.LastVideoPlayedAt
        => (AnimeSeries?.VideoLocals ?? [])
            .Select(video => RepoFactory.VideoLocalUser.GetByUserAndVideoLocalID(JMMUserID, video.VideoLocalID)?.WatchedDate)
            .WhereNotNull()
            .OrderDescending()
            .FirstOrDefault();

    DateTime? ISeriesUserData.LastVideoUpdatedAt => LastEpisodeUpdate;

    IReadOnlyList<string> ISeriesUserData.UserTags => UserTags;

    IShokoSeries? ISeriesUserData.Series => AnimeSeries;

    #endregion
}
