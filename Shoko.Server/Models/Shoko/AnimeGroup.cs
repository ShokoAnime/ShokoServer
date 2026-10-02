using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.User;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.Models.Shoko;

public class AnimeGroup : IShokoGroup
{
    #region Server DB Columns

    public int AnimeGroupID { get; set; }

    public int? AnimeGroupParentID { get; set; }

    public DateTime DateTimeUpdated { get; set; }

    public DateTime DateTimeCreated { get; set; }

    public DateTime? EpisodeAddedDate { get; set; }

    public DateTime? LatestEpisodeAirDate { get; set; }

    public int MissingEpisodeCount { get; set; }

    public int MissingEpisodeCountGroups { get; set; }

    public int? DefaultAnimeSeriesID { get; set; }

    public int? MainAniDBAnimeID { get; set; }

    #endregion

    #region Titles & Overviews

    /// <summary>
    ///   The group's name: the one a user gave it, else its main series'
    ///   title.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when no user named the group and it has no series.
    /// </exception>
    public string GroupName => TextAccess.Manager.GroupNameOf(this);

    /// <summary>
    ///   The group's overview: the one a user gave it, else its main series'
    ///   preferred overview.
    /// </summary>
    public string Description => TextAccess.Manager.GroupOverviewOf(this);

    /// <summary>
    ///   The name a user gave the group, stored as its <c>user</c> title.
    /// </summary>
    public ITitle? CustomTitle => TextAccess.Manager.CustomTitleOf(((IMetadata)this).ID);

    /// <summary>
    ///   The overview a user gave the group, stored as its <c>user</c>
    ///   overview.
    /// </summary>
    public IText? CustomOverview => TextAccess.Manager.CustomOverviewOf(((IMetadata)this).ID);

    #endregion

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    ///   A predictable sort name, made from <see cref="GroupName"/>, that
    ///   stuffs everything that's not between A-Z under #.
    /// </summary>
    public string SortName => ToGroupSortName(GroupName);

    /// <summary>
    ///   Makes a group's sort name from its name, stuffing everything that's
    ///   not between A-Z under #.
    /// </summary>
    /// <param name="name">The group's name.</param>
    /// <returns>The sort name.</returns>
    internal static string ToGroupSortName(string name)
    {
        var sortName = name.ToSortName().ToUpperInvariant();
        var initialChar = (short)(sortName.Length > 0 ? sortName[0] : ' ');
        return initialChar is >= 65 and <= 90 ? sortName : "#" + sortName;
    }

    public AnimeGroup? Parent => AnimeGroupParentID.HasValue ? RepoFactory.AnimeGroup.GetByID(AnimeGroupParentID.Value) : null;

    public List<AnimeGroup> AllGroupsAbove
    {
        get
        {
            var allGroupsAbove = new List<AnimeGroup>();
            var groupID = AnimeGroupParentID;
            while (groupID.HasValue && groupID.Value != 0)
            {
                var grp = RepoFactory.AnimeGroup.GetByID(groupID.Value);
                if (grp != null)
                {
                    allGroupsAbove.Add(grp);
                    groupID = grp.AnimeGroupParentID;
                }
                else
                {
                    groupID = 0;
                }
            }

            return allGroupsAbove;
        }
    }

    public List<AniDB_Anime> Anime =>
        RepoFactory.AnimeSeries.GetByGroupID(AnimeGroupID).Select(s => s.AniDB_Anime).WhereNotNull().ToList();

    public decimal AniDBRating
    {
        get
        {
            try
            {
                decimal totalRating = 0;
                var totalVotes = 0;

                foreach (var anime in Anime)
                {
                    totalRating += anime.GetAniDBTotalRating();
                    totalVotes += anime.GetAniDBTotalVotes();
                }

                if (totalVotes == 0)
                {
                    return 0;
                }

                return totalRating / totalVotes;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in  AniDBRating: {ex}");
                return 0;
            }
        }
    }

    public List<AnimeGroup> Children => RepoFactory.AnimeGroup.GetByParentID(AnimeGroupID);

    public IEnumerable<AnimeGroup> AllChildren
    {
        get
        {
            var stack = new Stack<AnimeGroup>(Children);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;
                foreach (var childGroup in current.Children)
                    stack.Push(childGroup);
            }
        }
    }

    public AnimeSeries? MainSeries
    {
        get
        {
            var seriesList = AllSeries;
            if (DefaultAnimeSeriesID.HasValue || MainAniDBAnimeID.HasValue)
            {
                AnimeSeries? mainSeries = null;
                if (DefaultAnimeSeriesID.HasValue)
                    mainSeries = seriesList.FirstOrDefault(ser => ser.AnimeSeriesID == DefaultAnimeSeriesID.Value);
                if (mainSeries is null && MainAniDBAnimeID.HasValue)
                    mainSeries = seriesList.FirstOrDefault(ser => ser.AniDB_ID == MainAniDBAnimeID.Value);
                if (mainSeries is not null)
                    return mainSeries;
            }
            return seriesList
                .FirstOrDefault();
        }
    }

    public List<AnimeSeries> Series => RepoFactory.AnimeSeries.GetByGroupID(AnimeGroupID)
        .OrderBy(a => a.AirDate ?? PartialDateOnly.MaxValue)
        .ThenBy(a => a.AnimeSeriesID)
        .ToList();

    public List<AnimeSeries> AllSeries
    {
        get
        {
            var seriesList = new List<AnimeSeries>();
            var stack = new Stack<AnimeGroup>([this]);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                seriesList.AddRange(current.Series);
                foreach (var childGroup in current.Children)
                    stack.Push(childGroup);
            }
            return seriesList
                .OrderBy(a => a.AirDate ?? PartialDateOnly.MaxValue)
                .ThenBy(a => a.AnimeSeriesID)
                .ToList();
        }
    }

    public List<AniDB_Tag> Tags => TagsOf(AllSeries);

    public List<CustomTag> CustomTags => CustomTagsOf(AllSeries);

    public HashSet<int> Years => YearsOf(AllSeries);

    public HashSet<(int Year, YearlySeason Season)> YearlySeasons => YearlySeasonsOf(AllSeries);

    public HashSet<ImageEntityType> AvailableImageTypes => AvailableImageTypesOf(AllSeries);

    public HashSet<ImageEntityType> PreferredImageTypes => PreferredImageTypesOf(AllSeries);

    public List<ITitle> Titles => AllSeries
        .SelectMany(ser => ser.AniDB_Anime?.Titles ?? [])
        .DistinctBy(title => (title.EntityID, title.ID))
        .ToList();

    public override string ToString()
        => $"Group: {GroupName} ({AnimeGroupID})";

    public AnimeGroup TopLevelAnimeGroup
    {
        get
        {
            var parent = Parent;
            if (parent == null)
            {
                return this;
            }

            while (true)
            {
                var next = parent.Parent;
                if (next == null)
                {
                    return parent;
                }

                parent = next;
            }
        }
    }

    public bool IsDescendantOf(int groupID)
        => IsDescendantOf(new[] { groupID });

    public bool IsDescendantOf(IEnumerable<int> groupIDs)
    {
        var idSet = groupIDs.ToHashSet();
        if (idSet.Count == 0)
            return false;

        var parent = Parent;
        while (parent != null)
        {
            if (idSet.Contains(parent.AnimeGroupID))
                return true;

            parent = parent.Parent;
        }

        return false;
    }

    #region Aggregates

    /// <summary>
    ///   The AniDB tags of the given series, heaviest first, each once.
    /// </summary>
    /// <param name="series">The series, all of a group or the ones a user may see.</param>
    /// <returns>The tags.</returns>
    internal static List<AniDB_Tag> TagsOf(IEnumerable<AnimeSeries> series) => series
        .SelectMany(ser => ser.AniDB_Anime?.AnimeTags ?? [])
        .OrderByDescending(a => a.Weight)
        .Select(animeTag => RepoFactory.AniDB_Tag.GetByTagID(animeTag.TagID))
        .WhereNotNull()
        .DistinctBy(a => a.TagID)
        .ToList();

    /// <summary>
    ///   The custom tags of the given series, by name, each once.
    /// </summary>
    /// <param name="series">The series, all of a group or the ones a user may see.</param>
    /// <returns>The tags.</returns>
    internal static List<CustomTag> CustomTagsOf(IEnumerable<AnimeSeries> series) => series
        .SelectMany(ser => RepoFactory.CustomTag.GetByAnimeID(ser.AniDB_ID))
        .DistinctBy(a => a.CustomTagID)
        .OrderBy(a => a.TagName)
        .ToList();

    /// <summary>
    ///   The years the given series aired in.
    /// </summary>
    /// <param name="series">The series, all of a group or the ones a user may see.</param>
    /// <returns>The years.</returns>
    internal static HashSet<int> YearsOf(IEnumerable<AnimeSeries> series)
        => series.SelectMany(a => a.Years).ToHashSet();

    /// <summary>
    ///   The yearly seasons the given series aired in.
    /// </summary>
    /// <param name="series">The series, all of a group or the ones a user may see.</param>
    /// <returns>The seasons.</returns>
    internal static HashSet<(int Year, YearlySeason Season)> YearlySeasonsOf(IEnumerable<AnimeSeries> series)
        => series.SelectMany(a => a.AniDB_Anime?.YearlySeasons ?? []).ToHashSet();

    /// <summary>
    ///   The image types the given series have images of.
    /// </summary>
    /// <param name="series">The series, all of a group or the ones a user may see.</param>
    /// <returns>The image types.</returns>
    internal static HashSet<ImageEntityType> AvailableImageTypesOf(IEnumerable<AnimeSeries> series)
        => series.SelectMany(ser => ser.AvailableImageTypes).ToHashSet();

    /// <summary>
    ///   The image types the given series have a preferred image of.
    /// </summary>
    /// <param name="series">The series, all of a group or the ones a user may see.</param>
    /// <returns>The image types.</returns>
    internal static HashSet<ImageEntityType> PreferredImageTypesOf(IEnumerable<AnimeSeries> series)
        => series.SelectMany(ser => ser.PreferredImageTypes).ToHashSet();

    /// <summary>
    ///   Adds up one kind of episode counts of the given series.
    /// </summary>
    /// <param name="series">The series, all of a group or the ones a user may see.</param>
    /// <param name="selector">Picks the counts of one series.</param>
    /// <returns>The summed counts.</returns>
    internal static EpisodeCounts SumEpisodeCounts(IEnumerable<IShokoSeries> series, Func<IShokoSeries, EpisodeCounts> selector)
    {
        var counts = new EpisodeCounts();
        foreach (var ser in series)
        {
            var ec = selector(ser);
            counts.Episodes += ec.Episodes;
            counts.Specials += ec.Specials;
            counts.Credits += ec.Credits;
            counts.Trailers += ec.Trailers;
            counts.Parodies += ec.Parodies;
            counts.Others += ec.Others;
        }
        return counts;
    }

    /// <summary>
    ///   Adds up the file source counts of the given series.
    /// </summary>
    /// <param name="series">The series, all of a group or the ones a user may see.</param>
    /// <returns>The summed counts.</returns>
    internal static FileSourceCounts SumFileSourceCounts(IEnumerable<IShokoSeries> series)
    {
        var counts = new FileSourceCounts();
        foreach (var ser in series)
        {
            var fsc = ser.FileSourceCounts;
            counts.Unknown += fsc.Unknown;
            counts.Other += fsc.Other;
            counts.TV += fsc.TV;
            counts.DVD += fsc.DVD;
            counts.BluRay += fsc.BluRay;
            counts.Web += fsc.Web;
            counts.VHS += fsc.VHS;
            counts.VCD += fsc.VCD;
            counts.LaserDisc += fsc.LaserDisc;
            counts.Camera += fsc.Camera;
            counts.Film += fsc.Film;
        }
        return counts;
    }

    /// <summary>
    ///   Adds up the release provider counts of the given series.
    /// </summary>
    /// <param name="series">The series, all of a group or the ones a user may see.</param>
    /// <returns>The summed counts, by provider.</returns>
    internal static IReadOnlyDictionary<string, int> SumReleaseProviderCounts(IEnumerable<IShokoSeries> series)
    {
        var counts = new Dictionary<string, int>();
        foreach (var ser in series)
        {
            foreach (var (provider, count) in ser.ReleaseProviderCounts)
            {
                counts.TryGetValue(provider, out var existing);
                counts[provider] = existing + count;
            }
        }
        return counts;
    }

    #endregion

    #region IMetadata Implementation

    /// <summary>
    ///   The ID last handed out, with the row ID it was made from. Kept since
    ///   it is asked for on every read of a title, and made again only when
    ///   the row's ID changes on insert.
    /// </summary>
    private Tuple<int, MetadataGuid>? _metadataID;

    /// <summary>
    ///   The texts the text manager worked out for this entry, kept here so
    ///   reading them back skips looking them up.
    /// </summary>
    internal object? TextMemo;

    MetadataGuid IMetadata.ID
    {
        get
        {
            var local = AnimeGroupID;
            if (_metadataID is { } cached && cached.Item1 == local)
                return cached.Item2;

            var id = new MetadataGuid(MetadataSource.Shoko, MetadataEntityType.Collection, local.ToString());
            _metadataID = new(local, id);
            return id;
        }
    }

    #endregion

    #region IWithTitles Implementation

    /// <summary>
    ///   The main series, for a title the group cannot go without.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when the group has no series.
    /// </exception>
    /// <returns>The main series.</returns>
    private AnimeSeries RequireMainSeries()
        => MainSeries ?? throw new InvalidOperationException($"Group {AnimeGroupID} has no series to take its name from.");

    string IWithTitles.Title => GroupName;

    ITitle IWithTitles.DefaultTitle => CustomTitle ?? RequireMainSeries().DefaultTitle;

    ITitle? IWithTitles.PreferredTitle => CustomTitle ?? RequireMainSeries().PreferredTitle;

    IReadOnlyList<ITitle> IWithTitles.Titles
    {
        get
        {
            var titles = new List<ITitle>();
            var custom = CustomTitle;
            if (custom is not null)
                titles.Add(custom);

            var mainSeriesId = MainSeries?.AnimeSeriesID;
            foreach (var series in (this as IShokoGroup).AllSeries)
            {
                foreach (var title in series.Titles)
                {
                    if ((custom is not null || series.LocalID != mainSeriesId) && title.Type == TitleType.Main)
                    {
                        titles.Add(new TitleStub
                        {
                            Language = title.Language,
                            LanguageCode = title.LanguageCode,
                            CountryCode = title.CountryCode,
                            Value = title.Value,
                            Source = title.Source,
                            Type = TitleType.Official,
                        });
                        continue;
                    }
                    titles.Add(title);
                }
            }

            return titles;
        }
    }

    #endregion

    #region IWithOverviews Implementation

    IText? IWithOverviews.DefaultOverview => CustomOverview ?? (MainSeries as IWithOverviews)?.DefaultOverview;

    IText? IWithOverviews.PreferredOverview => CustomOverview ?? MainSeries?.PreferredOverview;

    IReadOnlyList<IText> IWithOverviews.Overviews
    {
        get
        {
            var overviews = new List<IText>();
            if (CustomOverview is { } custom)
                overviews.Add(custom);

            foreach (var series in (this as IShokoGroup).AllSeries)
                overviews.AddRange(series.Overviews);

            return overviews;
        }
    }

    #endregion

    #region IWithImages Implementation

    IImageCrossReference? IWithImages.GetBestImageCrossReferenceForType(ImageEntityType imageType, bool primaryImage)
    {
        var withImages = (IWithImages)this;
        if (primaryImage)
        {
            // If a preferred image is set and available for the group, return it.
            if (withImages.GetPreferredImageCrossReferenceForType(imageType) is { IsEnabled: true, IsPrimaryAvailable: true } preferredImageCrossReference)
                return preferredImageCrossReference;

            // If a preferred image is set and available for the main series, return it.
            var mainSeries = (this as IShokoGroup).MainSeries;
            if (mainSeries.GetPreferredImageCrossReferenceForType(imageType) is { IsEnabled: true, IsPrimaryAvailable: true } mainSeriesPreferredImageCrossReference)
                return mainSeriesPreferredImageCrossReference;

            // If a default image is set and available for the main series, return it.
            var defaultImageCrossReference = mainSeries.GetDefaultImageCrossReferenceForType(imageType);
            if (defaultImageCrossReference is { IsEnabled: true, IsPrimaryAvailable: true })
                return defaultImageCrossReference;

            // Otherwise, return the first available image, first enabled image, or the first image.
            var selectedImageCrossReference = withImages.GetImageCrossReferences(new() { ImageType = imageType }) is { Count: > 0 } xrefs ? (
                xrefs.FirstOrDefault(i => i is { IsEnabled: true, IsDesired: true, IsPrimaryAvailable: true }) ??
                xrefs.FirstOrDefault(i => i is { IsEnabled: true, IsPrimaryAvailable: true }) ??
                xrefs.FirstOrDefault(i => i is { IsEnabled: true, IsDesired: true }) ??
                xrefs.FirstOrDefault(i => i is { IsEnabled: true })
            ) : null;
            if (selectedImageCrossReference is not null)
                return selectedImageCrossReference;
        }
        else
        {
            if (withImages.GetPreferredImageCrossReferenceForType(imageType) is { IsEnabled: true, IsPrimaryAvailable: true } preferredImageCrossReference)
                return preferredImageCrossReference;

            var mainSeries = (this as IShokoGroup).MainSeries;
            if (mainSeries.GetPreferredImageCrossReferenceForType(imageType) is { IsEnabled: true, IsPrimaryAvailable: true } mainSeriesPreferredImageCrossReference)
                return mainSeriesPreferredImageCrossReference;

            var defaultImageCrossReference = mainSeries.GetDefaultImageCrossReferenceForType(imageType);
            if (defaultImageCrossReference is { IsEnabled: true, IsPrimaryAvailable: true })
                return defaultImageCrossReference;

            var selectedImageCrossReference = withImages.GetImageCrossReferences(new() { ImageType = imageType }) is { Count: > 0 } xrefs ? (
                xrefs.FirstOrDefault(i => i is { IsEnabled: true, IsDesired: true, IsPrimaryAvailable: true }) ??
                xrefs.FirstOrDefault(i => i is { IsEnabled: true, IsPrimaryAvailable: true }) ??
                xrefs.FirstOrDefault(i => i is { IsEnabled: true, IsDesired: true }) ??
                xrefs.FirstOrDefault(i => i is { IsEnabled: true })
            ) : null;
            if (selectedImageCrossReference is not null)
                return selectedImageCrossReference;
        }

        return null;
    }

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => DateTimeCreated.ToUniversalTime();

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => DateTimeUpdated.ToUniversalTime();

    #endregion

    #region IShokoGroup Implementation

    int IShokoGroup.LocalID => AnimeGroupID;

    int? IShokoGroup.ParentGroupID => AnimeGroupParentID;

    int IShokoGroup.TopLevelGroupID => TopLevelAnimeGroup.AnimeGroupID;

    int IShokoGroup.MainSeriesID => (this as IShokoGroup).MainSeries.LocalID;

    bool IShokoGroup.HasConfiguredMainSeries => DefaultAnimeSeriesID.HasValue;

    bool IShokoGroup.HasCustomTitle => CustomTitle is not null;

    bool IShokoGroup.HasCustomOverview => CustomOverview is not null;

    IShokoGroup? IShokoGroup.ParentGroup => Parent;

    IShokoGroup IShokoGroup.TopLevelGroup => TopLevelAnimeGroup;

    IReadOnlyList<IShokoGroup> IShokoGroup.Groups => Children;

    IReadOnlyList<IShokoGroup> IShokoGroup.AllGroups => AllChildren.ToList();

    IReadOnlyList<IShokoGroup> IShokoGroup.AllParentGroups => AllGroupsAbove;

    IShokoSeries IShokoGroup.MainSeries => MainSeries ??
        throw new NullReferenceException($"Unable to get main series for group {AnimeGroupID} when accessed through IShokoGroup.MainSeries");

    IReadOnlyList<IShokoSeries> IShokoGroup.Series => Series;

    EpisodeCounts IShokoGroup.EpisodeCounts => SumEpisodeCounts((this as IShokoGroup).AllSeries, ser => ser.EpisodeCounts);

    FileSourceCounts IShokoGroup.FileSourceCounts => SumFileSourceCounts((this as IShokoGroup).AllSeries);

    EpisodeCounts IShokoGroup.LocalEpisodeCounts => SumEpisodeCounts((this as IShokoGroup).AllSeries, ser => ser.LocalEpisodeCounts);

    EpisodeCounts IShokoGroup.MissingEpisodeCounts => SumEpisodeCounts((this as IShokoGroup).AllSeries, ser => ser.MissingEpisodeCounts);

    EpisodeCounts IShokoGroup.UnairedEpisodeCounts => SumEpisodeCounts((this as IShokoGroup).AllSeries, ser => ser.UnairedEpisodeCounts);

    IReadOnlyDictionary<string, int> IShokoGroup.ReleaseProviderCounts => SumReleaseProviderCounts((this as IShokoGroup).AllSeries);

    IReadOnlyList<IShokoSeries> IShokoGroup.AllSeries => AllSeries;

    IGroupUserData IShokoGroup.GetUserData(IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.LocalID is 0 || RepoFactory.JMMUser.GetByID(user.LocalID) is null)
            throw new ArgumentException("User is not stored in the database!", nameof(user));
        lock (RepoFactory.AnimeGroup_User.GetWriteLock(user.LocalID, AnimeGroupID))
        {
            var userData = RepoFactory.AnimeGroup_User.GetByUserAndGroupID(user.LocalID, AnimeGroupID)
                ?? new() { JMMUserID = user.LocalID, AnimeGroupID = AnimeGroupID };
            if (userData.AnimeGroup_UserID is 0)
                RepoFactory.AnimeGroup_User.Save(userData);
            return userData;
        }
    }

    #endregion
}
