using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.Models.Shoko;

public class JMMUser : IIdentity, IUser
{
    #region Database Columns

    public int JMMUserID { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public int IsAdmin { get; set; }

    public int IsAniDBUser { get; set; }

    private string? _hideCategories;

    public string? HideCategories
    {
        get => _hideCategories;
        set { _hideCategories = value; _hideCategoriesCache = null; _forbiddenAnimeCache = null; }
    }

    public int? CanEditServerSettings { get; set; }

    public string? PlexUsers { get; set; }

    public string? PlexToken { get; set; }

    #endregion

    public string GetAvatarImageAsDataURL()
    {
        if ((this as IWithPrimaryImage).PrimaryImage is not { IsAvailable: true } primaryImage)
            return string.Empty;

        try
        {
            var byteArray = primaryImage.GetStream()?.ToByteArray();
            if (byteArray is null)
                return string.Empty;

            var base64 = Convert.ToBase64String(byteArray);
            return $"data:{primaryImage.ContentType};base64,{base64}";
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    public bool IsAllowedToSee(IShokoSeries ser)
    {
        if (ser is not AnimeSeries shokoSeries)
            throw new ArgumentException("Expected AnimeSeries", nameof(ser));
        if (GetHideCategories().Count == 0) return true;
        var anime = shokoSeries.AniDB_Anime;
        if (anime == null) return false;
        return !GetForbiddenAnimeIds().Contains(anime.AnimeID);
    }

    /// <inheritdoc/>
    public bool IsAllowedToSee(IAnidbAnime anime)
    {
        if (anime is not AniDB_Anime anidbAnime)
            throw new ArgumentException("Expected AniDB_Anime", nameof(anime));
        if (GetHideCategories().Count == 0) return true;
        return !GetForbiddenAnimeIds().Contains(anidbAnime.AnimeID);
    }

    /// <inheritdoc/>
    public bool IsAllowedToSee(IShokoGroup grp)
    {
        if (grp is not AnimeGroup shokoGroup)
            throw new ArgumentException("Expected AnimeGroup", nameof(grp));
        return AllowedGroup(shokoGroup);
    }

    /// <summary>
    /// Returns whether a user is allowed to view this series
    /// </summary>
    public bool AllowedSeries(AnimeSeries ser)
    {
        if (GetHideCategories().Count == 0) return true;
        var anime = ser?.AniDB_Anime;
        if (anime == null) return false;
        return !GetForbiddenAnimeIds().Contains(anime.AnimeID);
    }

    /// <summary>
    /// Returns whether a user is allowed to view this anime
    /// </summary>
    public bool AllowedAnime(AniDB_Anime anime)
    {
        if (GetHideCategories().Count == 0) return true;
        return !GetForbiddenAnimeIds().Contains(anime.AnimeID);
    }

    /// <summary>
    ///   Returns whether the user may see the group: when it holds at least
    ///   one series, at any level, that the user may see.
    /// </summary>
    /// <param name="grp">The group.</param>
    /// <returns><c>true</c> if the user may see the group.</returns>
    public bool AllowedGroup(AnimeGroup grp)
    {
        if (!HasRestrictions()) return true;
        return grp.AllSeries.Any(AllowedSeries);
    }

    /// <summary>
    ///   Returns whether the user may see every series in the group, at any
    ///   level, as changing the group needs.
    /// </summary>
    /// <param name="grp">The group.</param>
    /// <returns><c>true</c> if the user may see the whole group.</returns>
    public bool AllowedWholeGroup(AnimeGroup grp)
    {
        if (!HasRestrictions()) return true;
        return grp.AllSeries.All(AllowedSeries);
    }

    /// <summary>
    ///   Returns whether the user may see the video where files are listed or
    ///   searched: when it is linked to no known series, or to at least one
    ///   series the user may see.
    /// </summary>
    /// <param name="video">The video.</param>
    /// <returns><c>true</c> if the user may see the video.</returns>
    public bool AllowedVideo(VideoLocal video)
        => !HasRestrictions() || IsVideoVisible(video, AllowedAnime);

    /// <summary>
    ///   Returns whether a video is linked to no known series, or to at least
    ///   one whose AniDB anime passes the check.
    /// </summary>
    /// <param name="video">The video.</param>
    /// <param name="isAllowed">Whether the user may see the anime.</param>
    /// <returns><c>true</c> if the video is visible.</returns>
    internal static bool IsVideoVisible(VideoLocal video, Func<AniDB_Anime, bool> isAllowed)
    {
        var isLinked = false;
        foreach (var anime in video.EpisodeCrossReferences.DistinctBy(xref => xref.AnimeID).Select(xref => xref.AniDBAnime).WhereNotNull())
        {
            if (isAllowed(anime))
                return true;
            isLinked = true;
        }

        return !isLinked;
    }

    /// <summary>
    ///   Returns whether the user has restricted tags, and so may be kept
    ///   from seeing some series.
    /// </summary>
    /// <returns><c>true</c> if the user has restricted tags.</returns>
    public bool HasRestrictions()
        => GetHideCategories().Count > 0;

    public bool AllowedTag(AniDB_Tag tag)
    {
        return !GetHideCategories().Contains(tag.TagName);
    }

    [NotMapped]
    private HashSet<string>? _hideCategoriesCache;

    private sealed class ForbiddenAnimeCache(int generation, HashSet<int> ids)
    {
        public int Generation { get; } = generation;
        public HashSet<int> Ids { get; } = ids;
    }

    [NotMapped]
    private volatile ForbiddenAnimeCache? _forbiddenAnimeCache;

    public HashSet<string> GetHideCategories()
    {
        if (_hideCategoriesCache is { } cached) return cached;
        var categories = string.IsNullOrEmpty(HideCategories)
            ? new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            : new HashSet<string>(HideCategories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.InvariantCultureIgnoreCase);
        return Interlocked.CompareExchange(ref _hideCategoriesCache, categories, null) ?? categories;
    }

    private HashSet<int> GetForbiddenAnimeIds()
    {
        var currentGeneration = AniDB_Anime.TagGeneration;
        var cached = _forbiddenAnimeCache;
        if (cached?.Generation == currentGeneration)
            return cached.Ids;

        var hidden = GetHideCategories();
        var forbidden = hidden.Count == 0
            ? []
            : RepoFactory.AniDB_Anime.GetAll()
                .Where(a => hidden.Overlaps(a.GetAllTagsSet()))
                .Select(a => a.AnimeID)
                .ToHashSet();
        _forbiddenAnimeCache = new ForbiddenAnimeCache(currentGeneration, forbidden);
        return forbidden;
    }

    public List<AniDB_Tag> GetHideTags()
        => GetHideCategories()
        .SelectMany(RepoFactory.AniDB_Tag.GetByName)
        .WhereNotNull()
        .OrderBy(tag => tag.TagID)
        .ToList();

    #region IIdentity Implementation

    [NotMapped]
    string IIdentity.AuthenticationType => "API";

    [NotMapped]
    bool IIdentity.IsAuthenticated => true;

    [NotMapped]
    string IIdentity.Name => Username;

    #endregion

    #region IMetadata Implementation

    [NotMapped]
    MetadataGuid IMetadata.ID => new(MetadataSource.Shoko, MetadataEntityType.User, JMMUserID.ToString());

    [NotMapped]
    int IUser.LocalID => JMMUserID;

    #endregion

    #region IUser Implementation

    [NotMapped]
    string IUser.Username => Username;

    [NotMapped]
    bool IUser.IsAdmin => IsAdmin == 1;

    [NotMapped]
    bool IUser.IsAnidbUser => IsAniDBUser == 1;

    [NotMapped]
    IReadOnlyList<IAnidbTag> IUser.RestrictedTags => GetHideTags();

    #endregion
}
