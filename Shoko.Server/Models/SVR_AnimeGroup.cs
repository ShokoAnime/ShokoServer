using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models;

public class SVR_AnimeGroup : AnimeGroup, IGroup, IShokoGroup
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

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

    public SVR_AnimeGroup? Parent => AnimeGroupParentID.HasValue ? RepoFactory.AnimeGroup.GetByID(AnimeGroupParentID.Value) : null;

    public List<SVR_AniDB_Anime> Anime =>
        RepoFactory.AnimeSeries.GetByGroupID(AnimeGroupID).Select(s => s.AniDB_Anime).Where(anime => anime != null).ToList();

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
                logger.Error($"Error in  AniDBRating: {ex}");
                return 0;
            }
        }
    }

    public List<SVR_AnimeGroup> Children => RepoFactory.AnimeGroup.GetByParentID(AnimeGroupID);

    public IEnumerable<SVR_AnimeGroup> AllChildren
    {
        get
        {
            var stack = new Stack<SVR_AnimeGroup>();
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

    public SVR_AnimeSeries? MainSeries
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

    public List<SVR_AnimeSeries> Series
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

    public List<SVR_AnimeSeries> AllSeries
    {
        get
        {
            var seriesList = new List<SVR_AnimeSeries>();
            var stack = new Stack<SVR_AnimeGroup>();
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
                SVR_AnimeSeries? mainSeries = null;
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
        .SelectMany(ser => ser.AniDB_Anime.AnimeTags)
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

    public HashSet<(int Year, AnimeSeason Season)> Seasons => AllSeries.SelectMany(a => a.AniDB_Anime.Seasons).ToHashSet();

    public List<SVR_AniDB_Anime_Title> Titles => AllSeries
        .SelectMany(ser => ser.AniDB_Anime.Titles)
        .DistinctBy(tit => tit.AniDB_Anime_TitleID)
        .ToList();

    public override string ToString()
        => $"Group: {GroupName} ({AnimeGroupID})";

    public SVR_AnimeGroup TopLevelAnimeGroup
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

    #region IGroup Implementation

    string IGroup.Name => GroupName;
    IAnime IGroup.MainSeries => (MainSeries ?? AllSeries.First()).AniDB_Anime;

    IReadOnlyList<IAnime> IGroup.Series => AllSeries
        .Select(a => a.AniDB_Anime)
        .Where(a => a != null)
        .OrderBy(a => a.BeginYear)
        .ThenBy(a => a.AirDate ?? DateTime.MaxValue)
        .ThenBy(a => a.MainTitle)
        .Cast<IAnime>()
        .ToList();

    #endregion

    #region IMetadata Implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.Shoko;

    int IMetadata<int>.ID => AnimeGroupID;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.DefaultTitle => IsManuallyNamed == 1 ? GroupName : (this as IShokoGroup).MainSeries.DefaultTitle ?? $"<Shoko Group {AnimeGroupID}>";

    string IWithTitles.PreferredTitle => GroupName;

    IReadOnlyList<AnimeTitle> IWithTitles.Titles
    {
        get
        {
            var titles = new List<AnimeTitle>();
            if (IsManuallyNamed == 1)
            {
                titles.Add(new()
                {
                    Source = DataSourceEnum.Shoko,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Title = GroupName,
                    Type = TitleType.Main,
                });
            }

            var mainSeriesId = (this as IShokoGroup).MainSeriesID;
            foreach (var series in (this as IShokoGroup).AllSeries)
            {
                foreach (var title in series.Titles)
                {
                    if ((IsManuallyNamed == 1 || series.ID != mainSeriesId) && title.Type == TitleType.Main)
                        title.Type = TitleType.Official;
                    titles.Add(title);
                }
            }

            return titles;
        }
    }

    #endregion

    #region IWithDescription Implementation

    string IWithDescriptions.DefaultDescription => OverrideDescription == 1 ? Description : (this as IShokoGroup).MainSeries.DefaultDescription ?? string.Empty;

    string IWithDescriptions.PreferredDescription => Description;

    IReadOnlyList<TextDescription> IWithDescriptions.Descriptions
    {
        get
        {
            var titles = new List<TextDescription>();
            if (OverrideDescription == 1)
            {
                titles.Add(new()
                {
                    Source = DataSourceEnum.Shoko,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = GroupName,
                });
            }

            foreach (var series in (this as IShokoGroup).AllSeries)
                titles.AddRange(series.Descriptions);

            return titles;
        }
    }

    #endregion

    #region IShokoGroup Implementation

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

public class GroupVotes
{
    public GroupVotes(decimal? allVotes = null, decimal? permanentVotes = null, decimal? temporaryVotes = null)
    {
        AllVotes = allVotes;
        PermanentVotes = permanentVotes;
        TemporaryVotes = temporaryVotes;
    }

    public decimal? AllVotes { get; }

    public decimal? PermanentVotes { get; }

    public decimal? TemporaryVotes { get; }
}
