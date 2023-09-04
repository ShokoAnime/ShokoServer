using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Shoko.Server.Filters;
using Shoko.Server.Filters.Functions;
using Shoko.Server.Filters.Info;
using Shoko.Server.Filters.Logic;
using Shoko.Server.Filters.Logic.DateTimes;
using Shoko.Server.Filters.Selectors;
using Shoko.Server.Filters.User;
using Shoko.Server.Models.Filters;
using Xunit;

namespace Shoko.Tests;

public class FilterTests
{
    private const string GroupFilterableString =
        "{\"MissingEpisodes\":0,\"MissingEpisodesCollecting\":0,\"Tags\":[\"Earth\",\"Japan\",\"Asia\",\"friendship\",\"daily life\",\"high school\",\"school life\",\"comedy\",\"Kyoto\",\"dynamic\",\"themes\",\"source material\",\"setting\",\"elements\",\"place\",\"manga\",\"funny expressions\",\"storytelling\",\"character driven\",\"facial distortion\",\"narration\",\"origin\",\"episodic\",\"Japanese production\"],\"CustomTags\":[],\"Years\":[2022],\"Seasons\":[{\"Item1\":2022,\"Item2\":1}],\"HasTvDBLink\":true,\"HasMissingTvDbLink\":false,\"HasTMDbLink\":true,\"HasMissingTMDbLink\":false,\"HasTraktLink\":false,\"HasMissingTraktLink\":true,\"IsFinished\":true,\"AirDate\":\"2022-04-06T20:00:00\",\"LastAirDate\":\"2022-06-22T20:00:00\",\"AddedDate\":\"2023-05-05T14:42:24.4477733\",\"LastAddedDate\":\"2023-05-05T13:37:50.32298\",\"EpisodeCount\":12,\"TotalEpisodeCount\":12,\"LowestAniDBRating\":7.4,\"HighestAniDBRating\":7.4,\"VideoSources\":[\"Web\"],\"AnimeTypes\":[\"TVSeries\"],\"AudioLanguages\":[\"japanese\"],\"SubtitleLanguages\":[\"english\"]}";
    private const string GroupUserFilterableString =
        "{\"IsFavorite\":false,\"WatchedEpisodes\":12,\"UnwatchedEpisodes\":0,\"HasVotes\":false,\"HasPermanentVotes\":false,\"MissingPermanentVotes\":false,\"WatchedDate\":\"2023-05-05T13:42:13.3933582\",\"LastWatchedDate\":\"2023-05-05T13:43:21.3729042\",\"LowestUserRating\":0.0,\"HighestUserRating\":0.0,\"MissingEpisodes\":0,\"MissingEpisodesCollecting\":0,\"Tags\":[\"Earth\",\"Japan\",\"Asia\",\"friendship\",\"daily life\",\"high school\",\"school life\",\"comedy\",\"Kyoto\",\"dynamic\",\"themes\",\"source material\",\"setting\",\"elements\",\"place\",\"manga\",\"funny expressions\",\"storytelling\",\"character driven\",\"facial distortion\",\"narration\",\"origin\",\"episodic\",\"Japanese production\"],\"CustomTags\":[],\"Years\":[2022],\"Seasons\":[{\"Item1\":2022,\"Item2\":1}],\"HasTvDBLink\":true,\"HasMissingTvDbLink\":false,\"HasTMDbLink\":true,\"HasMissingTMDbLink\":false,\"HasTraktLink\":false,\"HasMissingTraktLink\":true,\"IsFinished\":true,\"AirDate\":\"2022-04-06T20:00:00\",\"LastAirDate\":\"2022-06-22T20:00:00\",\"AddedDate\":\"2023-05-05T14:42:24.4477733\",\"LastAddedDate\":\"2023-05-05T13:37:50.32298\",\"EpisodeCount\":12,\"TotalEpisodeCount\":12,\"LowestAniDBRating\":7.4,\"HighestAniDBRating\":7.4,\"VideoSources\":[\"Web\"],\"AnimeTypes\":[\"TVSeries\"],\"AudioLanguages\":[\"japanese\"],\"SubtitleLanguages\":[\"english\"]}";
    private const string SeriesFilterableString =
        "{\"MissingEpisodes\":0,\"MissingEpisodesCollecting\":0,\"Tags\":[\"high school\",\"dynamic\",\"themes\",\"source material\",\"setting\",\"elements\",\"place\",\"Earth\",\"Japan\",\"Kyoto\",\"manga\",\"Asia\",\"comedy\",\"friendship\",\"daily life\",\"school life\",\"funny expressions\",\"storytelling\",\"character driven\",\"facial distortion\",\"narration\",\"origin\",\"episodic\",\"Japanese production\"],\"CustomTags\":[],\"Years\":[2022],\"Seasons\":[{\"Item1\":2022,\"Item2\":1}],\"HasTvDBLink\":true,\"HasMissingTvDbLink\":false,\"HasTMDbLink\":false,\"HasMissingTMDbLink\":false,\"HasTraktLink\":false,\"HasMissingTraktLink\":true,\"IsFinished\":true,\"AirDate\":\"2022-04-06T20:00:00\",\"LastAirDate\":\"2022-06-22T20:00:00\",\"AddedDate\":\"2023-05-05T14:42:24.3131538\",\"LastAddedDate\":\"2023-05-05T13:37:50.32298\",\"EpisodeCount\":12,\"TotalEpisodeCount\":12,\"LowestAniDBRating\":7.4,\"HighestAniDBRating\":7.4,\"VideoSources\":[\"Web\"],\"AnimeTypes\":[\"TVSeries\"],\"AudioLanguages\":[\"japanese\"],\"SubtitleLanguages\":[\"english\"]}";
    private const string SeriesUserFilterableString =
        "{\"IsFavorite\":false,\"WatchedEpisodes\":12,\"UnwatchedEpisodes\":0,\"HasVotes\":false,\"HasPermanentVotes\":false,\"MissingPermanentVotes\":false,\"WatchedDate\":\"2023-05-05T13:42:13.3933582\",\"LastWatchedDate\":\"2023-05-05T13:43:21.3729042\",\"LowestUserRating\":0.0,\"HighestUserRating\":0.0,\"MissingEpisodes\":0,\"MissingEpisodesCollecting\":0,\"Tags\":[\"high school\",\"dynamic\",\"themes\",\"source material\",\"setting\",\"elements\",\"place\",\"Earth\",\"Japan\",\"Kyoto\",\"manga\",\"Asia\",\"comedy\",\"friendship\",\"daily life\",\"school life\",\"funny expressions\",\"storytelling\",\"character driven\",\"facial distortion\",\"narration\",\"origin\",\"episodic\",\"Japanese production\"],\"CustomTags\":[],\"Years\":[2022],\"Seasons\":[{\"Item1\":2022,\"Item2\":1}],\"HasTvDBLink\":true,\"HasMissingTvDbLink\":false,\"HasTMDbLink\":false,\"HasMissingTMDbLink\":false,\"HasTraktLink\":false,\"HasMissingTraktLink\":true,\"IsFinished\":true,\"AirDate\":\"2022-04-06T20:00:00\",\"LastAirDate\":\"2022-06-22T20:00:00\",\"AddedDate\":\"2023-05-05T14:42:24.3131538\",\"LastAddedDate\":\"2023-05-05T13:37:50.32298\",\"EpisodeCount\":12,\"TotalEpisodeCount\":12,\"LowestAniDBRating\":7.4,\"HighestAniDBRating\":7.4,\"VideoSources\":[\"Web\"],\"AnimeTypes\":[\"TVSeries\"],\"AudioLanguages\":[\"japanese\"],\"SubtitleLanguages\":[\"english\"]}";

    public static readonly IEnumerable<object[]> GroupFilterable = new[] { new[] { JsonConvert.DeserializeObject<Filterable>(GroupFilterableString) }};
    public static readonly IEnumerable<object[]> GroupUserFilterable = new[] { new[] { JsonConvert.DeserializeObject<UserDependentFilterable>(GroupUserFilterableString) }};
    public static readonly IEnumerable<object[]> SeriesFilterable = new[] { new[] { JsonConvert.DeserializeObject<Filterable>(SeriesFilterableString) }};
    public static readonly IEnumerable<object[]> SeriesUserFilterable = new[] { new[] { JsonConvert.DeserializeObject<UserDependentFilterable>(SeriesUserFilterableString) }};

    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithUserFilter_ExpectsException(Filterable group)
    {
        var top = new AndExpression
        {
            Left = new AndExpression
            {
                Left = new HasTagExpression
                {
                    Parameter = "comedy"
                },
                Right = new NotExpression
                {
                    Left = new HasTagExpression
                    {
                        Parameter = "18 restricted"
                    }
                }
            },
            Right = new HasWatchedEpisodesExpression()
        };

        Assert.True(top.UserDependent);
        Assert.Throws<ArgumentException>(() => top.Evaluate(group));
    }

    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithoutUserFilter_ExpectsTrue(Filterable group)
    {
        var top = new AndExpression
        {
            Left = new AndExpression
            {
                Left = new HasTagExpression
                {
                    Parameter = "comedy"
                },
                Right = new NotExpression
                {
                    Left = new HasTagExpression
                    {
                        Parameter = "18 restricted"
                    }
                }
            },
            Right = new HasVideoSourceExpression
            {
                Parameter = "Web"
            }
        };

        Assert.False(top.UserDependent);
        Assert.True(top.Evaluate(group));
    }
    
    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithDateFunctionFilter_ExpectsFalse(Filterable group)
    {
        var top = new AndExpression
        {
            Left = new AndExpression
            {
                Left = new HasTagExpression
                {
                    Parameter = "comedy"
                },
                Right = new NotExpression
                {
                    Left = new HasTagExpression
                    {
                        Parameter = "18 restricted"
                    }
                }
            },
            Right = new GreaterThanEqualExpression
            {
                Left = new LastAddedDateSelector(),
                Right = new DateDiffFunction
                {
                    Selector = new DateAddFunction
                    {
                        Selector = new TodayFunction(), Parameter = TimeSpan.FromDays(1) - TimeSpan.FromMilliseconds(1)
                    },
                    Parameter = TimeSpan.FromDays(30)
                },
            }
        };

        Assert.False(top.UserDependent);
        Assert.False(top.Evaluate(group));
    }
    
    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithDateFunctionFilter_ExpectsTrue(Filterable group)
    {
        var top = new AndExpression
        {
            Left = new AndExpression
            {
                Left = new HasTagExpression
                {
                    Parameter = "comedy"
                },
                Right = new NotExpression
                {
                    Left = new HasTagExpression
                    {
                        Parameter = "18 restricted"
                    }
                }
            },
            Right = new GreaterThanEqualExpression
            {
                Left = new LastAddedDateSelector(),
                Right = new DateDiffFunction
                {
                    Selector = new DateAddFunction
                    {
                        Selector = new TodayFunction(), Parameter = TimeSpan.FromDays(1) - TimeSpan.FromMilliseconds(1)
                    },
                    Parameter = TimeSpan.FromDays(120)
                },
            }
        };

        Assert.False(top.UserDependent);
        Assert.True(top.Evaluate(group));
    }

    [Theory, MemberData(nameof(GroupUserFilterable))]
    public void GroupUserFilterable_WithUserFilter_ExpectsTrue(UserDependentFilterable group)
    {
        var top = new AndExpression
        {
            Left = new AndExpression
            {
                Left = new HasTagExpression
                {
                    Parameter = "comedy"
                },
                Right = new NotExpression
                {
                    Left = new HasTagExpression
                    {
                        Parameter = "18 restricted"
                    }
                }
            },
            Right = new HasWatchedEpisodesExpression()
        };

        Assert.True(top.UserDependent);
        Assert.True(top.Evaluate(group));
    }

    [Theory, MemberData(nameof(SeriesFilterable))]
    public void SeriesFilterable_WithUserFilter_ExpectsException(Filterable series)
    {
        var top = new AndExpression
        {
            Left = new AndExpression
            {
                Left = new HasTagExpression
                {
                    Parameter = "comedy"
                },
                Right = new NotExpression
                {
                    Left = new HasTagExpression
                    {
                        Parameter = "18 restricted"
                    }
                }
            },
            Right = new HasWatchedEpisodesExpression()
        };

        Assert.True(top.UserDependent);
        Assert.Throws<ArgumentException>(() => top.Evaluate(series));
    }

    [Theory, MemberData(nameof(SeriesUserFilterable))]
    public void SeriesUserFilterable_WithUserFilter_ExpectsTrue(UserDependentFilterable series)
    {
        var top = new AndExpression
        {
            Left = new AndExpression
            {
                Left = new HasTagExpression
                {
                    Parameter = "comedy"
                },
                Right = new NotExpression
                {
                    Left = new HasTagExpression
                    {
                        Parameter = "18 restricted"
                    }
                }
            },
            Right = new HasWatchedEpisodesExpression()
        };

        Assert.True(top.UserDependent);
        Assert.True(top.Evaluate(series));
    }
}
