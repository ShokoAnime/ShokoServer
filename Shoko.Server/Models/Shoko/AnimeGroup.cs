using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories;

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
                .OrderBy(a => a.AirDate ?? DateTime.MaxValue)
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
                .OrderBy(a => a.AirDate ?? DateTime.MaxValue)
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
        .SelectMany(ser => ser.GetAvailableImageTypes())
        .ToHashSet();

    public HashSet<ImageEntityType> PreferredImageTypes => AllSeries
        .SelectMany(ser => ser.GetPreferredImageTypes())
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
