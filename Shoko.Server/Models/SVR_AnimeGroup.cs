using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models;

public class SVR_AnimeGroup : AnimeGroup, IGroup
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

    public SVR_AnimeGroup Parent => AnimeGroupParentID.HasValue ? RepoFactory.AnimeGroup.GetByID(AnimeGroupParentID.Value) : null;

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
            stack.Push(this);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;
                foreach (var childGroup in current.Children) stack.Push(childGroup);
            }
        }
    }

    // TODO probably figure out a different way to do this, for example, have this only have a getter, and the setter exists in the service
    public SVR_AnimeSeries MainSeries
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

            // Earliest airing series.
            return AllSeries.FirstOrDefault();
        }

        set
        {
            // Set the id before potentially reseting the fields, so the getter uses
            // the new id instead of the old.
            DefaultAnimeSeriesID = value?.AnimeSeriesID;

            ValidateMainSeries();

            // Reset the name/description if the group is not manually named.
            var series = value ?? (MainAniDBAnimeID.HasValue ? RepoFactory.AnimeSeries.GetByAnimeID(MainAniDBAnimeID.Value) : AllSeries.FirstOrDefault());
            if (IsManuallyNamed == 0 && series != null)
                GroupName = series!.SeriesName;
            if (OverrideDescription == 0 && series != null)
                Description = series!.AniDB_Anime.Description;

            // Save the changes for this group only.
            DateTimeUpdated = DateTime.Now;
            RepoFactory.AnimeGroup.Save(this, false);
        }
    }

    public bool ValidateMainSeries()
    {
        var changed = false;
        var allSeries = AllSeries;

        // User overridden main series.
        if (DefaultAnimeSeriesID.HasValue && !allSeries.Any(series => series.AnimeSeriesID == DefaultAnimeSeriesID.Value))
        {
            DefaultAnimeSeriesID = null;
            changed = true;
        }

        // Auto selected main series.
        if (MainAniDBAnimeID.HasValue && !allSeries.Any(series => series.AniDB_ID == MainAniDBAnimeID.Value))
        {
            MainAniDBAnimeID = null;
            changed = true;
        }

        return changed;
    }

    public List<SVR_AnimeSeries> Series
    {
        get
        {
            var seriesList = RepoFactory.AnimeSeries
                .GetByGroupID(AnimeGroupID)
                .OrderBy(a => a.AirDate)
                .ToList();

            // Make sure the default/main series is the first, if it's directly
            // within the group.
            if (!DefaultAnimeSeriesID.HasValue && !MainAniDBAnimeID.HasValue) return seriesList;

            var mainSeries = MainSeries;
            if (seriesList.Remove(mainSeries)) seriesList.Insert(0, mainSeries);

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
                .OrderBy(a => a.AirDate)
                .ToList();

            // Make sure the default/main series is the first if it's somewhere
            // within the group.
            if (DefaultAnimeSeriesID.HasValue || MainAniDBAnimeID.HasValue)
            {
                SVR_AnimeSeries mainSeries = null;
                if (DefaultAnimeSeriesID.HasValue)
                    mainSeries = seriesList.FirstOrDefault(ser => ser.AnimeSeriesID == DefaultAnimeSeriesID.Value);

                if (mainSeries == null && MainAniDBAnimeID.HasValue)
                    mainSeries = seriesList.FirstOrDefault(सर => सर.AniDB_ID == MainAniDBAnimeID.Value);

                if (mainSeries != null)
                {
                    seriesList.Remove(mainSeries);
                    seriesList.Insert(0, mainSeries);
                }
            }

            return seriesList;
        }
    }


    public List<AniDB_Tag> Tags
    {
        get
        {
            var animeTags = AllSeries.SelectMany(ser => ser.AniDB_Anime.AnimeTags).ToList();
            return animeTags.OrderByDescending(a => a.Weight).Select(animeTag => RepoFactory.AniDB_Tag.GetByTagID(animeTag.TagID)).WhereNotNull()
                .DistinctBy(a => a.TagID).ToList();
        }
    }

    public List<CustomTag> CustomTags
    {
        get
        {
            var tags = new List<CustomTag>();
            var tagIDs = new List<int>();


            // get a list of all the unique custom tags for all the series in this group
            foreach (var ser in AllSeries)
            foreach (var tag in RepoFactory.CustomTag.GetByAnimeID(ser.AniDB_ID))
            {
                if (!tagIDs.Contains(tag.CustomTagID))
                {
                    tagIDs.Add(tag.CustomTagID);
                    tags.Add(tag);
                }
            }

            return tags.OrderBy(a => a.TagName).ToList();
        }
    }

    public HashSet<int> Years => AllSeries.SelectMany(a => a.Years).ToHashSet();
    public HashSet<(int Year, AnimeSeason Season)> Seasons => AllSeries.SelectMany(a => a.AniDB_Anime.Seasons).ToHashSet();

    public List<SVR_AniDB_Anime_Title> Titles
    {
        get
        {
            var animeTitleIDs = new List<int>();
            var animeTitles = new List<SVR_AniDB_Anime_Title>();

            // get a list of all the unique titles for this all the series in this group
            foreach (var ser in AllSeries)
            foreach (var aat in ser.AniDB_Anime.Titles)
            {
                if (!animeTitleIDs.Contains(aat.AniDB_Anime_TitleID))
                {
                    animeTitleIDs.Add(aat.AniDB_Anime_TitleID);
                    animeTitles.Add(aat);
                }
            }

            return animeTitles;
        }
    }

    public override string ToString()
    {
        return $"Group: {GroupName} ({AnimeGroupID})";
    }

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

    string IGroup.Name => GroupName;
    IAnime IGroup.MainSeries => MainSeries.AniDB_Anime;

    IReadOnlyList<IAnime> IGroup.Series => AllSeries
        .Select(a => a.AniDB_Anime)
        .Where(a => a != null)
        .OrderBy(a => a.BeginYear)
        .ThenBy(a => a.AirDate ?? DateTime.MaxValue)
        .ThenBy(a => a.MainTitle)
        .Cast<IAnime>()
        .ToList();
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
