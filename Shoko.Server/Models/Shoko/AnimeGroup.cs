using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.Models.Shoko;

public class AnimeGroup : IShokoGroup
{
    #region Server DB Columns

    public int AnimeGroupID { get; set; }

    public int? AnimeGroupParentID { get; set; }

    public string GroupName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int IsManuallyNamed { get; set; }

    public DateTime DateTimeUpdated { get; set; }

    public DateTime DateTimeCreated { get; set; }

    public DateTime? EpisodeAddedDate { get; set; }

    public DateTime? LatestEpisodeAirDate { get; set; }

    public int MissingEpisodeCount { get; set; }

    public int MissingEpisodeCountGroups { get; set; }

    public int OverrideDescription { get; set; }

    public int? DefaultAnimeSeriesID { get; set; }

    public int? MainAniDBAnimeID { get; set; }

    #endregion

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Get a predictable sort name that stuffs everything that's not between
    /// A-Z under #.
    /// </summary>
    public string SortName
    {
        get
        {
            var sortName = !string.IsNullOrWhiteSpace(GroupName) ? GroupName.ToSortName().ToUpperInvariant() : "";
            var initialChar = (short)(sortName.Length > 0 ? sortName[0] : ' ');
            return initialChar is >= 65 and <= 90 ? sortName : "#" + sortName;
        }
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
            var stack = new Stack<AnimeGroup>();
            foreach (var child in Children)
            {
                stack.Push(child);
            }

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;
                foreach (var childGroup in current.Children) stack.Push(childGroup);
            }
        }
    }

    public AnimeSeries? MainSeries
    {
        get
        {
            if (DefaultAnimeSeriesID.HasValue)
            {
                var series = RepoFactory.AnimeSeries.GetByID(DefaultAnimeSeriesID.Value);
                if (series != null)
                    return series;
            }

            // Auto selected main series.
            if (MainAniDBAnimeID.HasValue)
            {
                var series = RepoFactory.AnimeSeries.GetByAnimeID(MainAniDBAnimeID.Value);
                if (series != null)
                    return series;
            }

            return null;
        }
    }

    public List<AnimeSeries> Series
    {
        get
        {
            var seriesList = RepoFactory.AnimeSeries
                .GetByGroupID(AnimeGroupID)
                .OrderByDescending(a => a.AirDate.HasValue)
                .ThenByDescending(a => a.AirDate?.IsComplete ?? false)
                .ThenBy(a => a.AirDate ?? PartialDateOnly.MaxValue)
                .ToList();

            // Make sure the default/main series is the first, if it's directly
            // within the group.
            if (!DefaultAnimeSeriesID.HasValue && !MainAniDBAnimeID.HasValue) return seriesList;

            var mainSeries = MainSeries;
            if (mainSeries != null && seriesList.Remove(mainSeries)) seriesList.Insert(0, mainSeries);

            return seriesList;
        }
    }

    public List<AnimeSeries> AllSeries
    {
        get
        {
            var seriesList = new List<AnimeSeries>();
            var stack = new Stack<AnimeGroup>();
            stack.Push(this);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                // get the series for this group
                var thisSeries = current.Series;
                seriesList.AddRange(thisSeries);

                foreach (var childGroup in current.Children)
                {
                    stack.Push(childGroup);
                }
            }

            seriesList = seriesList
                .OrderByDescending(a => a.AirDate.HasValue)
                .ThenByDescending(a => a.AirDate?.IsComplete ?? false)
                .ThenBy(a => a.AirDate ?? PartialDateOnly.MaxValue)
                .ToList();

            // Make sure the default/main series is the first if it's somewhere
            // within the group.
            if (DefaultAnimeSeriesID.HasValue || MainAniDBAnimeID.HasValue)
            {
                AnimeSeries? mainSeries = null;
                if (DefaultAnimeSeriesID.HasValue)
                    mainSeries = seriesList.FirstOrDefault(ser => ser.AnimeSeriesID == DefaultAnimeSeriesID.Value);

                if (mainSeries == null && MainAniDBAnimeID.HasValue)
                    mainSeries = seriesList.FirstOrDefault(ser => ser.AniDB_ID == MainAniDBAnimeID.Value);

                if (mainSeries != null && seriesList.Remove(mainSeries)) seriesList.Insert(0, mainSeries);
            }

            return seriesList;
        }
    }


    public List<AniDB_Tag> Tags => AllSeries
        .SelectMany(ser => ser.AniDB_Anime?.AnimeTags ?? [])
        .OrderByDescending(a => a.Weight)
        .Select(animeTag => RepoFactory.AniDB_Tag.GetByTagID(animeTag.TagID))
        .WhereNotNull()
        .DistinctBy(a => a.TagID)
        .ToList();

    public List<CustomTag> CustomTags => AllSeries
        .SelectMany(ser => RepoFactory.CustomTag.GetByAnimeID(ser.AniDB_ID))
        .DistinctBy(a => a.CustomTagID)
        .OrderBy(a => a.TagName)
        .ToList();

    public HashSet<int> Years => AllSeries.SelectMany(a => a.Years).ToHashSet();

    public HashSet<(int Year, YearlySeason Season)> YearlySeasons => AllSeries.SelectMany(a => a.AniDB_Anime?.YearlySeasons ?? []).ToHashSet();

    public HashSet<ImageEntityType> AvailableImageTypes => AllSeries
        .SelectMany(ser => ser.AvailableImageTypes)
        .ToHashSet();

    public HashSet<ImageEntityType> PreferredImageTypes => AllSeries
        .SelectMany(ser => ser.PreferredImageTypes)
        .ToHashSet();

    public List<AniDB_Anime_Title> Titles => AllSeries
        .SelectMany(ser => ser.AniDB_Anime?.Titles ?? [])
        .DistinctBy(tit => tit.AniDB_Anime_TitleID)
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
        => IsDescendantOf(new int[] { groupID });

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

    #region IMetadata Implementation

    DataEntityType IMetadata.EntityType => DataEntityType.Group;

    DataSource IMetadata.Source => DataSource.Shoko;

    string IMetadata<string>.ID => AnimeGroupID.ToString();

    int IMetadata<int>.ID => AnimeGroupID;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.Title => IsManuallyNamed == 1
        ? GroupName
        : (this as IShokoGroup).MainSeries.Title;

    ITitle IWithTitles.DefaultTitle => IsManuallyNamed == 1
        ? new TitleStub
        {
            Language = TitleLanguage.Unknown,
            LanguageCode = "unk",
            Type = TitleType.Main,
            Value = GroupName,
            Source = DataSource.User,
        }
        : (this as IShokoGroup).MainSeries.DefaultTitle;

    ITitle? IWithTitles.PreferredTitle => IsManuallyNamed == 1
        ? new TitleStub
        {
            Language = TitleLanguage.Unknown,
            LanguageCode = "unk",
            Type = TitleType.Main,
            Value = GroupName,
            Source = DataSource.User,
        }
        : (this as IShokoGroup).MainSeries.PreferredTitle;

    IReadOnlyList<ITitle> IWithTitles.Titles
    {
        get
        {
            var titles = new List<ITitle>();
            if (IsManuallyNamed == 1)
                titles.Add(new TitleStub
                {
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = GroupName,
                    Source = DataSource.User,
                    Type = TitleType.Main,
                });

            var mainSeriesId = (this as IShokoGroup).MainSeriesID;
            foreach (var series in (this as IShokoGroup).AllSeries)
            {
                foreach (var title in series.Titles)
                {
                    if ((IsManuallyNamed == 1 || series.ID != mainSeriesId) && title.Type == TitleType.Main)
                    {
                        titles.Add(new TitleStub()
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

    #region IWithDescription Implementation

    IText? IWithDescriptions.DefaultDescription => OverrideDescription == 1
        ? new TextStub()
        {
            Source = DataSource.Shoko,
            Language = TitleLanguage.Unknown,
            LanguageCode = "unk",
            Value = Description,
        }
        : (this as IShokoGroup).MainSeries.DefaultDescription;

    IText? IWithDescriptions.PreferredDescription => OverrideDescription == 1
        ? new TextStub()
        {
            Source = DataSource.Shoko,
            Language = TitleLanguage.Unknown,
            LanguageCode = "unk",
            Value = Description,
        }
        : (this as IShokoGroup).MainSeries.PreferredDescription;

    IReadOnlyList<IText> IWithDescriptions.Descriptions
    {
        get
        {
            var titles = new List<IText>();
            if (OverrideDescription == 1)
            {
                titles.Add(new TextStub()
                {
                    Source = DataSource.Shoko,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = Description,
                });
            }

            foreach (var series in (this as IShokoGroup).AllSeries)
                titles.AddRange(series.Descriptions);

            return titles;
        }
    }

    #endregion

    #region IWithImages Implementation

    public IImage? GetPreferredImageForType(ImageEntityType imageType)
        => GetImages(imageType: imageType).FirstOrDefault(image => image.IsPreferred);

    public IImageCrossReference? GetPreferredImageCrossReferenceForType(ImageEntityType imageType)
        => GetImageCrossReferences(imageType: imageType).FirstOrDefault(xref => xref.IsPreferred);

    public IReadOnlyList<IImage> GetImages(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null, bool primaryImage = false)
        => ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetImagesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired, primaryImage);

    public IReadOnlyList<IImageCrossReference> GetImageCrossReferences(DataSource? imageSource = null, ImageEntityType? imageType = null, DataSource? xrefSource = null, bool? isEnabled = null, bool? isDesired = null)
        => ISystemService.StaticServices.GetRequiredService<IImageManager>()
            .GetImageCrossReferencesForEntity(this, imageSource, imageType, xrefSource, isEnabled, isDesired);

    #endregion

    #region IWithPrimaryImage Implementation

    public IImage? PrimaryImage
    {
        get
        {
            if (GetPreferredImageForType(ImageEntityType.Primary) is { } preferredImage)
                return preferredImage;

            var mainSeries = (this as IShokoGroup).MainSeries;
            if (mainSeries.GetPreferredImageForType(ImageEntityType.Primary) is { } mainSeriesPreferredImage)
                return mainSeriesPreferredImage;

            var groupImage = GetImages(imageType: ImageEntityType.Primary, primaryImage: true) is { Count: > 0 } images ? (
                images.FirstOrDefault(i => i is { IsEnabled: true, IsAvailable: true }) ??
                images.FirstOrDefault(i => i is { IsEnabled: true }) ??
                images.FirstOrDefault()
            ) : null;
            if (groupImage is not null)
                return groupImage;

            return mainSeries.DefaultPrimaryImage;
        }
    }

    public IImageCrossReference? PrimaryImageCrossReference
    {
        get
        {
            if (GetPreferredImageCrossReferenceForType(ImageEntityType.Primary) is { } preferredImage)
                return preferredImage;

            var mainSeries = (this as IShokoGroup).MainSeries;
            if (mainSeries.GetPreferredImageCrossReferenceForType(ImageEntityType.Primary) is { } mainSeriesPreferredImage)
                return mainSeriesPreferredImage;

            var groupXref = GetImageCrossReferences(imageType: ImageEntityType.Primary) is { Count: > 0 } xrefs ? (
                xrefs.FirstOrDefault(xref => xref.GetPrimaryImage() is { IsEnabled: true, IsAvailable: true }) ??
                xrefs.FirstOrDefault(xref => xref is { IsEnabled: true }) ??
                xrefs.FirstOrDefault()
            ) : null;
            if (groupXref is not null)
                return groupXref;

            return mainSeries.DefaultPrimaryImageCrossReference;
        }
    }

    #endregion

    #region IWithBackdropImage Implementation

    public IImage? BackdropImage
    {
        get
        {
            if (GetPreferredImageForType(ImageEntityType.Backdrop) is { } preferredImage)
                return preferredImage;

            var mainSeries = (this as IShokoGroup).MainSeries;
            if (mainSeries.GetPreferredImageForType(ImageEntityType.Backdrop) is { } mainSeriesPreferredImage)
                return mainSeriesPreferredImage;

            var groupImage = GetImages(imageType: ImageEntityType.Backdrop) is { Count: > 0 } images ? (
                images.FirstOrDefault(i => i is { IsEnabled: true, IsAvailable: true }) ??
                images.FirstOrDefault(i => i is { IsEnabled: true }) ??
                images.FirstOrDefault()
            ) : null;
            if (groupImage is not null)
                return groupImage;

            return mainSeries.DefaultBackdropImage;
        }
    }

    public IImageCrossReference? BackdropImageCrossReference
    {
        get
        {
            if (GetPreferredImageCrossReferenceForType(ImageEntityType.Backdrop) is { } preferredImage)
                return preferredImage;

            var mainSeries = (this as IShokoGroup).MainSeries;
            if (mainSeries.GetPreferredImageCrossReferenceForType(ImageEntityType.Backdrop) is { } mainSeriesPreferredImage)
                return mainSeriesPreferredImage;

            var groupXref = GetImageCrossReferences(imageType: ImageEntityType.Backdrop) is { Count: > 0 } xrefs ? (
                xrefs.FirstOrDefault(xref => xref.GetPrimaryImage() is { IsEnabled: true, IsAvailable: true }) ??
                xrefs.FirstOrDefault(xref => xref is { IsEnabled: true }) ??
                xrefs.FirstOrDefault()
            ) : null;
            if (groupXref is not null)
                return groupXref;

            return mainSeries.DefaultBackdropImageCrossReference;
        }
    }

    #endregion

    #region IWithLogoImage Implementation

    public IImage? LogoImage
    {
        get
        {
            if (GetPreferredImageForType(ImageEntityType.Logo) is { } preferredImage)
                return preferredImage;

            var mainSeries = (this as IShokoGroup).MainSeries;
            if (mainSeries.GetPreferredImageForType(ImageEntityType.Logo) is { } mainSeriesPreferredImage)
                return mainSeriesPreferredImage;

            var groupImage = GetImages(imageType: ImageEntityType.Logo) is { Count: > 0 } images ? (
                images.FirstOrDefault(i => i is { IsEnabled: true, IsAvailable: true }) ??
                images.FirstOrDefault(i => i is { IsEnabled: true }) ??
                images.FirstOrDefault()
            ) : null;
            if (groupImage is not null)
                return groupImage;

            return mainSeries.DefaultLogoImage;
        }
    }

    public IImageCrossReference? LogoImageCrossReference
    {
        get
        {
            if (GetPreferredImageCrossReferenceForType(ImageEntityType.Logo) is { } preferredImage)
                return preferredImage;

            var mainSeries = (this as IShokoGroup).MainSeries;
            if (mainSeries.GetPreferredImageCrossReferenceForType(ImageEntityType.Logo) is { } mainSeriesPreferredImage)
                return mainSeriesPreferredImage;

            var groupXref = GetImageCrossReferences(imageType: ImageEntityType.Logo) is { Count: > 0 } xrefs ? (
                xrefs.FirstOrDefault(xref => xref.GetPrimaryImage() is { IsEnabled: true, IsAvailable: true }) ??
                xrefs.FirstOrDefault(xref => xref is { IsEnabled: true }) ??
                xrefs.FirstOrDefault()
            ) : null;
            if (groupXref is not null)
                return groupXref;

            return mainSeries.DefaultLogoImageCrossReference;
        }
    }

    #endregion

    #region IWithBannerImage Implementation

    public IImage? BannerImage
    {
        get
        {
            if (GetPreferredImageForType(ImageEntityType.Banner) is { } preferredImage)
                return preferredImage;

            var mainSeries = (this as IShokoGroup).MainSeries;
            if (mainSeries.GetPreferredImageForType(ImageEntityType.Banner) is { } mainSeriesPreferredImage)
                return mainSeriesPreferredImage;

            var groupImage = GetImages(imageType: ImageEntityType.Banner) is { Count: > 0 } images ? (
                images.FirstOrDefault(i => i is { IsEnabled: true, IsAvailable: true }) ??
                images.FirstOrDefault(i => i is { IsEnabled: true }) ??
                images.FirstOrDefault()
            ) : null;
            if (groupImage is not null)
                return groupImage;

            return mainSeries.DefaultBannerImage;
        }
    }

    public IImageCrossReference? BannerImageCrossReference
    {
        get
        {
            if (GetPreferredImageCrossReferenceForType(ImageEntityType.Banner) is { } preferredImage)
                return preferredImage;

            var mainSeries = (this as IShokoGroup).MainSeries;
            if (mainSeries.GetPreferredImageCrossReferenceForType(ImageEntityType.Banner) is { } mainSeriesPreferredImage)
                return mainSeriesPreferredImage;

            var groupXref = GetImageCrossReferences(imageType: ImageEntityType.Banner) is { Count: > 0 } xrefs ? (
                xrefs.FirstOrDefault(xref => xref.GetPrimaryImage() is { IsEnabled: true, IsAvailable: true }) ??
                xrefs.FirstOrDefault(xref => xref is { IsEnabled: true }) ??
                xrefs.FirstOrDefault()
            ) : null;
            if (groupXref is not null)
                return groupXref;

            return mainSeries.DefaultBannerImageCrossReference;
        }
    }

    #endregion

    #region IWithDiscImage Implementation

    public IImage? DiscImage
    {
        get
        {
            if (GetPreferredImageForType(ImageEntityType.Disc) is { } preferredImage)
                return preferredImage;

            var mainSeries = (this as IShokoGroup).MainSeries;
            if (mainSeries.GetPreferredImageForType(ImageEntityType.Disc) is { } mainSeriesPreferredImage)
                return mainSeriesPreferredImage;

            var groupImage = GetImages(imageType: ImageEntityType.Disc) is { Count: > 0 } images ? (
                images.FirstOrDefault(i => i is { IsEnabled: true, IsAvailable: true }) ??
                images.FirstOrDefault(i => i is { IsEnabled: true }) ??
                images.FirstOrDefault()
            ) : null;
            if (groupImage is not null)
                return groupImage;

            return mainSeries.DefaultDiscImage;
        }
    }

    public IImageCrossReference? DiscImageCrossReference
    {
        get
        {
            if (GetPreferredImageCrossReferenceForType(ImageEntityType.Disc) is { } preferredImage)
                return preferredImage;

            var mainSeries = (this as IShokoGroup).MainSeries;
            if (mainSeries.GetPreferredImageCrossReferenceForType(ImageEntityType.Disc) is { } mainSeriesPreferredImage)
                return mainSeriesPreferredImage;

            var groupXref = GetImageCrossReferences(imageType: ImageEntityType.Disc) is { Count: > 0 } xrefs ? (
                xrefs.FirstOrDefault(xref => xref.GetPrimaryImage() is { IsEnabled: true, IsAvailable: true }) ??
                xrefs.FirstOrDefault(xref => xref is { IsEnabled: true }) ??
                xrefs.FirstOrDefault()
            ) : null;
            if (groupXref is not null)
                return groupXref;

            return mainSeries.DefaultDiscImageCrossReference;
        }
    }

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => DateTimeCreated.ToUniversalTime();

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => DateTimeUpdated.ToUniversalTime();

    #endregion

    #region IShokoGroup Implementation

    int IShokoGroup.ID => AnimeGroupID;

    int? IShokoGroup.ParentGroupID => AnimeGroupParentID;

    int IShokoGroup.TopLevelGroupID => TopLevelAnimeGroup.AnimeGroupID;

    int IShokoGroup.MainSeriesID => (this as IShokoGroup).MainSeries.ID;

    bool IShokoGroup.HasConfiguredMainSeries => DefaultAnimeSeriesID.HasValue;

    bool IShokoGroup.HasCustomTitle => IsManuallyNamed == 1;

    bool IShokoGroup.HasCustomDescription => OverrideDescription == 1;

    IShokoGroup? IShokoGroup.ParentGroup => Parent;

    IShokoGroup IShokoGroup.TopLevelGroup => TopLevelAnimeGroup;

    IReadOnlyList<IShokoGroup> IShokoGroup.Groups => Children;

    IReadOnlyList<IShokoGroup> IShokoGroup.AllGroups => AllChildren.ToList();

    IShokoSeries IShokoGroup.MainSeries => MainSeries ?? AllSeries.FirstOrDefault() ??
        throw new NullReferenceException($"Unable to get main series for group {AnimeGroupID} when accessed through IShokoGroup.MainSeries");

    IReadOnlyList<IShokoSeries> IShokoGroup.Series => Series;

    IReadOnlyList<IShokoSeries> IShokoGroup.AllSeries => AllSeries;

    #endregion
}
