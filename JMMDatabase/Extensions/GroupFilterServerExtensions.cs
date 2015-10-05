using System;
using System.Globalization;
using System.Linq;
using JMMModels;
using JMMModels.Childs;
using JMMModels.Extensions;

namespace JMMDatabase.Extensions
{
    public static class GroupFilterServerExtensions
    {

        public static bool EvaluateGroup(this GroupFilter gf, AnimeGroup grp, JMMUser curUser)
        {
            // sub groups don't count
            if (grp.ParentId != null) return false;

            // make sure the user has not filtered this out
            if (curUser.RestrictedTagsIds != null && curUser.RestrictedTagsIds.Count > 0)
            {

                foreach (string c in curUser.RestrictedTagsIds)
                {
                    if (grp.HasTagId(c))
                        return false;
                }
            }

            // first check for anime groups which are included exluded every time
            foreach (GroupFilterCondition gfc in gf.Conditions)
            {
                if (gfc.Type != GroupFilterConditionType.AnimeGroup) continue;
                if (gfc.Operator == GroupFilterOperator.Equals)
                    if (gfc.Parameter == grp.Id) return true;

                if (gfc.Operator == GroupFilterOperator.NotEquals)
                    if (gfc.Parameter == grp.Id) return false;
            }

            NumberStyles style = NumberStyles.Number;
            CultureInfo culture = CultureInfo.CreateSpecificCulture("en-GB");

            if (gf.BaseCondition == GroupFilterBaseCondition.Exclude) return false;

            //Contract_AnimeGroup contractGroup = grp.ToContract(userRec);
            GroupUserStats ustats = grp.UsersStats.FirstOrDefault(a => a.JMMUserId == curUser.Id);
            // now check other conditions
            foreach (GroupFilterCondition gfc in gf.Conditions)
            {
                switch (gfc.Type)
                {
                    case GroupFilterConditionType.Favourite:
                        if (ustats == null) return false;
                        if (gfc.Operator == GroupFilterOperator.Include && !ustats.IsFave) return false;
                        if (gfc.Operator == GroupFilterOperator.Exclude && ustats.IsFave) return false;
                        break;

                    case GroupFilterConditionType.MissingEpisodes:
                        if (gfc.Operator == GroupFilterOperator.Include && !grp.HasMissingEpisodesAny()) return false;
                        if (gfc.Operator == GroupFilterOperator.Exclude && grp.HasMissingEpisodesAny()) return false;
                        break;

                    case GroupFilterConditionType.MissingEpisodesCollecting:
                        if (gfc.Operator == GroupFilterOperator.Include && !grp.HasMissingEpisodesGroups()) return false;
                        if (gfc.Operator == GroupFilterOperator.Exclude && grp.HasMissingEpisodesGroups()) return false;
                        break;

                    case GroupFilterConditionType.HasWatchedEpisodes:
                        if (ustats == null) return false;
                        if (gfc.Operator == GroupFilterOperator.Include && !ustats.AnyEpisodeWatched()) return false;
                        if (gfc.Operator == GroupFilterOperator.Exclude && ustats.AnyEpisodeWatched()) return false;
                        break;

                    case GroupFilterConditionType.HasUnwatchedEpisodes:
                        if (ustats == null) return false;
                        if (gfc.Operator == GroupFilterOperator.Include && !ustats.HasUnwatchedEpisodes()) return false;
                        if (gfc.Operator == GroupFilterOperator.Exclude && ustats.HasUnwatchedEpisodes()) return false;
                        break;

                    case GroupFilterConditionType.AssignedTvDBInfo:
                        if (gfc.Operator == GroupFilterOperator.Include && !grp.HasTvDB) return false;
                        if (gfc.Operator == GroupFilterOperator.Exclude && grp.HasTvDB) return false;
                        break;

                    case GroupFilterConditionType.AssignedMALInfo:
                        if (gfc.Operator == GroupFilterOperator.Include && !grp.HasMAL) return false;
                        if (gfc.Operator == GroupFilterOperator.Exclude && grp.HasMAL) return false;
                        break;

                    case GroupFilterConditionType.AssignedMovieDBInfo:
                        if (gfc.Operator == GroupFilterOperator.Include && !grp.HasMovieDB) return false;
                        if (gfc.Operator == GroupFilterOperator.Exclude && grp.HasMovieDB) return false;
                        break;

                    case GroupFilterConditionType.AssignedTvDBOrMovieDBInfo:
                        if (gfc.Operator == GroupFilterOperator.Include && !(grp.HasMovieDB || grp.HasTvDB)) return false;
                        if (gfc.Operator == GroupFilterOperator.Exclude && (grp.HasMovieDB || grp.HasTvDB)) return false;
                        break;

                    case GroupFilterConditionType.CompletedSeries:
                        if (gfc.Operator == GroupFilterOperator.Include && !grp.HasCompletedSeries) return false;
                        if (gfc.Operator == GroupFilterOperator.Exclude && grp.HasCompletedSeries) return false;
                        break;

                    case GroupFilterConditionType.FinishedAiring:
                        if (gfc.Operator == GroupFilterOperator.Include && !grp.HasSeriesFinishingAiring) return false;
                        if (gfc.Operator == GroupFilterOperator.Exclude && !grp.HasSeriesCurrentlyAiring) return false;
                        break;

                    case GroupFilterConditionType.UserVoted:
                        if (gfc.Operator == GroupFilterOperator.Include && !ustats.UserVotesPermanent.HasValue) return false;
                        if (gfc.Operator == GroupFilterOperator.Exclude && ustats.UserVotesPermanent.HasValue) return false;
                        break;

                    case GroupFilterConditionType.UserVotedAny:
                        if (gfc.Operator == GroupFilterOperator.Include && !ustats.HasVotes) return false;
                        if (gfc.Operator == GroupFilterOperator.Exclude && ustats.HasVotes) return false;
                        break;

                    case GroupFilterConditionType.AirDate:
                        DateTime filterDate;
                        if (gfc.Operator == GroupFilterOperator.LastXDays)
                        {
                            int days;
                            int.TryParse(gfc.Parameter, out days);
                            filterDate = DateTime.Today.AddDays(0 - days);
                        }
                        else
                            filterDate = gfc.Parameter.FromYYYYMMDDDate();

                        if (gfc.Operator == GroupFilterOperator.GreaterThan || gfc.Operator == GroupFilterOperator.LastXDays)
                        {
                            if (!grp.FirstSerieAirDate.HasValue || !grp.LastSerieAirDate.HasValue) return false;
                            if (grp.LastSerieAirDate.Value < filterDate) return false;
                        }
                        if (gfc.Operator == GroupFilterOperator.LessThan)
                        {
                            if (!grp.FirstSerieAirDate.HasValue || !grp.LastSerieAirDate.HasValue) return false;
                            if (grp.FirstSerieAirDate.Value > filterDate) return false;
                        }
                        break;

                    case GroupFilterConditionType.SeriesCreatedDate:
                        DateTime filterDateSeries;
                        if (gfc.Operator == GroupFilterOperator.LastXDays)
                        {
                            int days;
                            int.TryParse(gfc.Parameter, out days);
                            filterDateSeries = DateTime.Today.AddDays(0 - days);
                        }
                        else
                            filterDateSeries = gfc.Parameter.FromYYYYMMDDDate();

                        if (gfc.Operator == GroupFilterOperator.GreaterThan || gfc.Operator == GroupFilterOperator.LastXDays)
                        {
                            if (!grp.FirstSerieCreationDate.HasValue) return false;
                            if (grp.FirstSerieCreationDate.Value < filterDateSeries) return false;
                        }
                        if (gfc.Operator == GroupFilterOperator.LessThan)
                        {
                            if (!grp.FirstSerieCreationDate.HasValue) return false;
                            if (grp.FirstSerieCreationDate.Value > filterDateSeries) return false;
                        }
                        break;

                    case GroupFilterConditionType.EpisodeWatchedDate:
                        DateTime filterDateEpsiodeWatched;
                        if (gfc.Operator == GroupFilterOperator.LastXDays)
                        {
                            int days;
                            int.TryParse(gfc.Parameter, out days);
                            filterDateEpsiodeWatched = DateTime.Today.AddDays(0 - days);
                        }
                        else
                            filterDateEpsiodeWatched = gfc.Parameter.FromYYYYMMDDDate();

                        if (gfc.Operator == GroupFilterOperator.GreaterThan || gfc.Operator == GroupFilterOperator.LastXDays)
                        {
                            if (ustats?.WatchedDate == null) return false;
                            if (ustats.WatchedDate.Value < filterDateEpsiodeWatched) return false;
                        }
                        if (gfc.Operator == GroupFilterOperator.LessThan)
                        {
                            if (ustats?.WatchedDate == null) return false;
                            if (ustats.WatchedDate.Value > filterDateEpsiodeWatched) return false;
                        }
                        break;

                    case GroupFilterConditionType.EpisodeAddedDate:
                        DateTime filterDateEpisodeAdded;
                        if (gfc.Operator == GroupFilterOperator.LastXDays)
                        {
                            int days;
                            int.TryParse(gfc.Parameter, out days);
                            filterDateEpisodeAdded = DateTime.Today.AddDays(0 - days);
                        }
                        else
                            filterDateEpisodeAdded = gfc.Parameter.FromYYYYMMDDDate();

                        if (gfc.Operator == GroupFilterOperator.GreaterThan || gfc.Operator == GroupFilterOperator.LastXDays)
                        {
                            if (!grp.EpisodeAddedDate.HasValue) return false;
                            if (grp.EpisodeAddedDate.Value < filterDateEpisodeAdded) return false;
                        }
                        if (gfc.Operator == GroupFilterOperator.LessThan)
                        {
                            if (!grp.EpisodeAddedDate.HasValue) return false;
                            if (grp.EpisodeAddedDate.Value > filterDateEpisodeAdded) return false;
                        }
                        break;

                    case GroupFilterConditionType.EpisodeCount:

                        int epCount;
                        int.TryParse(gfc.Parameter, out epCount);

                        if (gfc.Operator == GroupFilterOperator.GreaterThan && grp.NormalEpisodeCount < epCount) return false;
                        if (gfc.Operator == GroupFilterOperator.LessThan && grp.NormalEpisodeCount > epCount) return false;
                        break;

                    case GroupFilterConditionType.AniDBRating:

                        float dRating;
                        float.TryParse(gfc.Parameter, style, culture, out dRating);

                        if (gfc.Operator == GroupFilterOperator.GreaterThan && grp.Rating() < dRating) return false;
                        if (gfc.Operator == GroupFilterOperator.LessThan && grp.Rating() > dRating) return false;
                        break;

                    case GroupFilterConditionType.UserRating:

                        if (!ustats.UserVotes.HasValue) return false;

                        float dUserRating;
                        float.TryParse(gfc.Parameter, style, culture, out dUserRating);

                        if (gfc.Operator == GroupFilterOperator.GreaterThan && ustats.UserVotes.Value < dUserRating) return false;
                        if (gfc.Operator == GroupFilterOperator.LessThan && ustats.UserVotes.Value > dUserRating) return false;
                        break;

                    case GroupFilterConditionType.Category:

                        string filterParm = gfc.Parameter.Trim();

                        string[] cats = filterParm.Split(',');
                        bool foundCat = false;
                        foreach (string cat in cats)
                        {
                            if (cat.Trim().Length == 0) continue;
                            if (cat.Trim() == ",") continue;
                            if (grp.HasTagName(cat))
                            {
                                foundCat = true;
                                break;
                            }
                        }

                        if (gfc.Operator == GroupFilterOperator.In)
                            if (!foundCat) return false;

                        if (gfc.Operator == GroupFilterOperator.NotIn)
                            if (foundCat) return false;
                        break;

                    case GroupFilterConditionType.CustomTags:

                        filterParm = gfc.Parameter.Trim();

                        string[] tags = filterParm.Split(',');
                        bool foundTag = false;
                        foreach (string tag in tags)
                        {
                            if (tag.Trim().Length == 0) continue;
                            if (tag.Trim() == ",") continue;
                            if (grp.HasCustomTagName(tag))
                            {
                                foundTag = true;
                                break;
                            }
                        }

                        if (gfc.Operator == GroupFilterOperator.In)
                            if (!foundTag) return false;

                        if (gfc.Operator == GroupFilterOperator.NotIn)
                            if (foundTag) return false;
                        break;

                    case GroupFilterConditionType.AnimeType:

                        filterParm = gfc.Parameter.Trim();
                        string[] atypes = filterParm.Split(',');
                        bool foundAnimeType = false;
                        foreach (string atype in atypes)
                        {
                            if (atype.Trim().Length == 0) continue;
                            if (atype.Trim() == ",") continue;
                            if (grp.HasAniDBType(atype))
                            {
                                foundAnimeType = true;
                                break;
                            }
                        }

                        if (gfc.Operator == GroupFilterOperator.In)
                            if (!foundAnimeType) return false;

                        if (gfc.Operator == GroupFilterOperator.NotIn)
                            if (foundAnimeType) return false;
                        break;



                    case GroupFilterConditionType.VideoQuality:

                        filterParm = gfc.Parameter.Trim();

                        string[] vidQuals = filterParm.Split(',');
                        bool foundVid = false;
                        bool foundVidAllEps = false;
                        foreach (string vidq in vidQuals)
                        {
                            if (vidq.Trim().Length == 0) continue;
                            if (vidq.Trim() == ",") continue;
                            if (grp.HasVideoQuality(vidq))
                                foundVid = true;
                            if (grp.HasReleaseQuality(vidq))
                                foundVidAllEps = true;
                        }

                        if (gfc.Operator == GroupFilterOperator.In)
                            if (!foundVid) return false;

                        if (gfc.Operator == GroupFilterOperator.NotIn)
                            if (foundVid) return false;

                        if (gfc.Operator == GroupFilterOperator.InAllEpisodes)
                            if (!foundVidAllEps) return false;

                        if (gfc.Operator == GroupFilterOperator.NotInAllEpisodes)
                            if (foundVidAllEps) return false;

                        break;

                    case GroupFilterConditionType.AudioLanguage:
                    case GroupFilterConditionType.SubtitleLanguage:

                        filterParm = gfc.Parameter.Trim();

                        string[] languages = filterParm.Split(',');
                        bool foundLan = false;
                        foreach (string lanName in languages)
                        {
                            if (lanName.Trim().Length == 0) continue;
                            if (lanName.Trim() == ",") continue;

                            if (gfc.Type == GroupFilterConditionType.AudioLanguage)
                                foundLan = grp.HasAudioLanguage(lanName);
                            if (gfc.Type == GroupFilterConditionType.SubtitleLanguage)
                                foundLan = grp.HasSubtitleLanguage(lanName);
                        }

                        if (gfc.Operator == GroupFilterOperator.In)
                            if (!foundLan) return false;

                        if (gfc.Operator == GroupFilterOperator.NotIn)
                            if (foundLan) return false;

                        break;
                }
            }

            return true;
        }
    }
}
