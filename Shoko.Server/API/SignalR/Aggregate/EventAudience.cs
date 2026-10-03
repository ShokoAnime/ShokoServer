using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// Who may receive an event about an entity, by the AniDB anime behind it and
/// the restricted tags of each user on the feed. The anime are looked up once
/// per event, and only when a user with restricted tags is listening.
/// </summary>
public sealed class EventAudience
{
    #region Fields

    /// <summary>
    /// Every user on the feed, whatever their restricted tags.
    /// </summary>
    public static readonly EventAudience Everyone = new(null, null, Fallback.Visible);

    /// <summary>
    /// The users without restricted tags, for an event that can't be checked.
    /// </summary>
    public static readonly EventAudience Unrestricted = new(null, null, Fallback.Hidden);

    private readonly Func<IEnumerable<int>>? _getAnimeIDs;

    private readonly Func<int, IAnidbAnime?>? _getAnime;

    private readonly Fallback _fallback;

    private Resolution? _resolved;

    #endregion

    #region Constructors

    private EventAudience(Func<IEnumerable<int>>? getAnimeIDs, Func<int, IAnidbAnime?>? getAnime, Fallback fallback)
    {
        _getAnimeIDs = getAnimeIDs;
        _getAnime = getAnime;
        _fallback = fallback;
    }

    #endregion

    #region Factories

    /// <summary>
    /// The users who may see a Shoko series, the rule episodes and seasons
    /// follow too: hidden from restricted users when its anime is unknown.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="getAnime">Looks up an AniDB anime by ID.</param>
    /// <returns>The audience.</returns>
    public static EventAudience ForSeries(IShokoSeries series, Func<int, IAnidbAnime?> getAnime)
        => new(() => [series.AnidbAnimeID], getAnime, Fallback.Hidden);

    /// <summary>
    /// The users who may see a Shoko episode: those who may see its series,
    /// or everyone when it has none.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <param name="getAnime">Looks up an AniDB anime by ID.</param>
    /// <returns>The audience.</returns>
    public static EventAudience ForEpisode(IShokoEpisode episode, Func<int, IAnidbAnime?> getAnime)
        => new(() => SeriesOf(episode) is { } series ? [series.AnidbAnimeID] : [], getAnime, Fallback.VisibleWhenUnlinked);

    /// <summary>
    /// The users who may see a Shoko group: those who may see any series in
    /// it, at any level. An empty group is hidden from restricted users.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="getAnime">Looks up an AniDB anime by ID.</param>
    /// <returns>The audience.</returns>
    public static EventAudience ForGroup(IShokoGroup group, Func<int, IAnidbAnime?> getAnime)
        => new(() => group.AllSeries.Select(series => series.AnidbAnimeID), getAnime, Fallback.Hidden);

    /// <summary>
    /// The users who may see a video: everyone when it is linked to no known
    /// anime, or those who may see any anime it is linked to.
    /// </summary>
    /// <param name="video">The video, if any.</param>
    /// <param name="getAnime">Looks up an AniDB anime by ID.</param>
    /// <param name="moreAnimeIDs">More anime the video is linked to, such as those of a release or a snapshot taken before a removal.</param>
    /// <param name="isRemoval">Hides the video from restricted users when it is linked but none of its anime is known any more.</param>
    /// <returns>The audience.</returns>
    public static EventAudience ForVideo(IVideo? video, Func<int, IAnidbAnime?> getAnime, Func<IEnumerable<int>>? moreAnimeIDs = null, bool isRemoval = false)
        => new(
            () => (video?.CrossReferences.Select(xref => xref.AnidbAnimeID) ?? []).Concat(moreAnimeIDs?.Invoke() ?? []),
            getAnime,
            isRemoval ? Fallback.VisibleWhenUnlinked : Fallback.Visible
        );

    /// <summary>
    /// The users who may see an AniDB anime. An anime that is no longer known
    /// is hidden from restricted users.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <param name="getAnime">Looks up an AniDB anime by ID.</param>
    /// <returns>The audience.</returns>
    public static EventAudience ForAnime(int animeID, Func<int, IAnidbAnime?> getAnime)
        => new(() => [animeID], getAnime, Fallback.Hidden);

    /// <summary>
    /// The users who may see an airing: everyone when its anime is unknown,
    /// as the airing calendar shows it.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID of the airing's episode, if any.</param>
    /// <param name="getAnime">Looks up an AniDB anime by ID.</param>
    /// <returns>The audience.</returns>
    public static EventAudience ForAiring(int? animeID, Func<int, IAnidbAnime?> getAnime)
        => animeID is { } id ? new(() => [id], getAnime, Fallback.Visible) : Everyone;

    /// <summary>
    /// The users who may see a metadata entry, by the rule of the metadata
    /// entry routes. Only AniDB and Shoko entries and videos are kept from
    /// users; an entry being removed is hidden from restricted users.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="getAnime">Looks up an AniDB anime by ID.</param>
    /// <param name="isRemoval">Whether the entry is being removed, so what it belongs to may be gone.</param>
    /// <returns>The audience.</returns>
    public static EventAudience ForEntry(IMetadata entry, Func<int, IAnidbAnime?> getAnime, bool isRemoval = false)
    {
        var audience = entry switch
        {
            IAnidbAnime anime => ForAnime(anime.AnidbID, getAnime),
            ISeason<IAnidbAnime, IAnidbEpisode> season => ForAnime(season.SeriesID.TryGetNumericID<int>(out var animeID) ? animeID : 0, getAnime),
            IAnidbEpisode episode => new(() => [episode.AnidbAnimeID], getAnime, Fallback.Visible),
            IShokoSeries series => ForSeries(series, getAnime),
            ISeason<IShokoSeries, IShokoEpisode> season => ForSeries(season.Series, getAnime),
            IShokoEpisode episode => ForEpisode(episode, getAnime),
            IVideo video => ForVideo(video, getAnime, isRemoval: isRemoval),
            _ => Everyone,
        };
        if (!isRemoval || entry is IVideo || ReferenceEquals(audience, Everyone))
            return audience;
        return new(audience._getAnimeIDs, getAnime, Fallback.Hidden);
    }

    #endregion

    #region Checks

    /// <summary>
    /// Whether a user has no restricted tags, and so may see everything.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns><see langword="true"/> when the user has no restricted tags.</returns>
    public static bool IsUnrestricted(IUser user)
        => user is JMMUser shokoUser ? !shokoUser.HasRestrictions() : user.RestrictedTags.Count is 0;

    /// <summary>
    /// Whether a user may receive the event. A user without restricted tags
    /// always may, and is checked without looking anything up.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns><see langword="true"/> when the user may receive the event.</returns>
    public bool IsVisibleTo(IUser user)
    {
        if (ReferenceEquals(this, Everyone) || IsUnrestricted(user))
            return true;

        var (anime, isVisible) = _resolved ??= Resolve();
        if (anime.Count is 0)
            return isVisible;

        foreach (var entry in anime)
        {
            if (user.IsAllowedToSee(entry))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Looks up the known anime behind the entity, and what a restricted user
    /// is told when none is known.
    /// </summary>
    /// <returns>The known anime, and whether the entity is visible without any.</returns>
    private Resolution Resolve()
    {
        if (_getAnimeIDs is null || _getAnime is null)
            return new([], _fallback is not Fallback.Hidden);

        var anime = new List<IAnidbAnime>();
        var isLinked = false;
        foreach (var animeID in _getAnimeIDs().Where(animeID => animeID > 0).Distinct())
        {
            isLinked = true;
            if (_getAnime(animeID) is { } entry)
                anime.Add(entry);
        }

        var isVisible = _fallback switch
        {
            Fallback.Visible => true,
            Fallback.VisibleWhenUnlinked => !isLinked,
            _ => false,
        };
        return new(anime, isVisible);
    }

    /// <summary>
    /// The series of a Shoko episode, without throwing when it is gone.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The series, or <see langword="null"/> when it is gone.</returns>
    private static IShokoSeries? SeriesOf(IShokoEpisode episode)
        => episode is AnimeEpisode animeEpisode ? animeEpisode.AnimeSeries : episode.Series;

    #endregion

    #region Nested Types

    /// <summary>
    /// The known anime behind the entity, looked up once per event.
    /// </summary>
    /// <param name="Anime">The known anime.</param>
    /// <param name="IsVisible">Whether a restricted user may see the entity when none is known.</param>
    private sealed record Resolution(IReadOnlyList<IAnidbAnime> Anime, bool IsVisible);

    /// <summary>
    /// What a restricted user is told when none of the anime behind the entity
    /// is known.
    /// </summary>
    private enum Fallback
    {
        /// <summary>
        /// The entity is visible.
        /// </summary>
        Visible,

        /// <summary>
        /// The entity is visible only when it is linked to no anime at all.
        /// </summary>
        VisibleWhenUnlinked,

        /// <summary>
        /// The entity is hidden.
        /// </summary>
        Hidden,
    }

    #endregion
}
