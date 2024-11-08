using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Shoko.Server.Filters.Files;
using Shoko.Server.Filters.Functions;
using Shoko.Server.Filters.Info;
using Shoko.Server.Filters.Logic.DateTimes;
using Shoko.Server.Filters.Logic.Expressions;
using Shoko.Server.Filters.Selectors;
using Shoko.Server.Filters.Selectors.DateSelectors;
using Shoko.Server.Filters.User;
using Xunit;

namespace Shoko.Tests;

public class FilterTests
{
    #region TestData

    private const string GroupFilterableString =
        "{\"MissingEpisodes\":0,\"MissingEpisodesCollecting\":0,\"Tags\":[\"Earth\",\"Japan\",\"Asia\",\"friendship\",\"daily life\",\"high school\",\"school life\",\"comedy\",\"Kyoto\",\"dynamic\",\"themes\",\"source material\",\"setting\",\"elements\",\"place\",\"manga\",\"funny expressions\",\"storytelling\",\"character driven\",\"facial distortion\",\"narration\",\"origin\",\"episodic\",\"Japanese production\"],\"CustomTags\":[],\"Years\":[2022],\"Seasons\":[{\"Item1\":2022,\"Item2\":1}],\"HasTvDBLink\":false,\"HasMissingTvDbLink\":false,\"HasTmdbLink\":true,\"HasMissingTmdbLink\":false,\"HasTraktLink\":false,\"HasMissingTraktLink\":true,\"IsFinished\":true,\"AirDate\":\"2022-04-06T20:00:00\",\"LastAirDate\":\"2022-06-22T20:00:00\",\"AddedDate\":\"2023-05-05T14:42:24.4477733\",\"LastAddedDate\":\"2023-05-05T13:37:50.32298\",\"EpisodeCount\":12,\"TotalEpisodeCount\":12,\"LowestAniDBRating\":7.4,\"HighestAniDBRating\":7.4,\"VideoSources\":[\"Web\"],\"AnimeTypes\":[\"TVSeries\"],\"AudioLanguages\":[\"japanese\"],\"SubtitleLanguages\":[\"english\"]}";
    private const string GroupUserFilterableString =
        "{\"IsFavorite\":false,\"WatchedEpisodes\":12,\"UnwatchedEpisodes\":0,\"HasVotes\":false,\"HasPermanentVotes\":false,\"MissingPermanentVotes\":false,\"WatchedDate\":\"2023-05-05T13:42:13.3933582\",\"LastWatchedDate\":\"2023-05-05T13:43:21.3729042\",\"LowestUserRating\":0.0,\"HighestUserRating\":0.0}";
    private const string SeriesFilterableString =
        "{\"MissingEpisodes\":0,\"MissingEpisodesCollecting\":0,\"Tags\":[\"high school\",\"dynamic\",\"themes\",\"source material\",\"setting\",\"elements\",\"place\",\"Earth\",\"Japan\",\"Kyoto\",\"manga\",\"Asia\",\"comedy\",\"friendship\",\"daily life\",\"school life\",\"funny expressions\",\"storytelling\",\"character driven\",\"facial distortion\",\"narration\",\"origin\",\"episodic\",\"Japanese production\"],\"CustomTags\":[],\"Years\":[2022],\"Seasons\":[{\"Item1\":2022,\"Item2\":1}],\"HasTvDBLink\":false,\"HasMissingTvDbLink\":false,\"HasTmdbLink\":false,\"HasMissingTmdbLink\":false,\"HasTraktLink\":false,\"HasMissingTraktLink\":true,\"IsFinished\":true,\"AirDate\":\"2022-04-06T20:00:00\",\"LastAirDate\":\"2022-06-22T20:00:00\",\"AddedDate\":\"2023-05-05T14:42:24.3131538\",\"LastAddedDate\":\"2023-05-05T13:37:50.32298\",\"EpisodeCount\":12,\"TotalEpisodeCount\":12,\"LowestAniDBRating\":7.4,\"HighestAniDBRating\":7.4,\"VideoSources\":[\"Web\"],\"AnimeTypes\":[\"TVSeries\"],\"AudioLanguages\":[\"japanese\"],\"SubtitleLanguages\":[\"english\"]}";
    private const string SeriesUserFilterableString =
        "{\"IsFavorite\":false,\"WatchedEpisodes\":12,\"UnwatchedEpisodes\":0,\"HasVotes\":false,\"HasPermanentVotes\":false,\"MissingPermanentVotes\":false,\"WatchedDate\":\"2023-05-05T13:42:13.3933582\",\"LastWatchedDate\":\"2023-05-05T13:43:21.3729042\",\"LowestUserRating\":0.0,\"HighestUserRating\":0.0}";

    #endregion

    public static readonly IEnumerable<object[]> GroupFilterable = new[] { new[] { JsonConvert.DeserializeObject<TestFilterable>(GroupFilterableString, new IReadOnlySetConverter()) }};
    public static readonly IEnumerable<object[]> GroupUserFilterable = new[] { new object[] { JsonConvert.DeserializeObject<TestFilterable>(GroupFilterableString, new IReadOnlySetConverter()), JsonConvert.DeserializeObject<TestFilterableUserInfo>(GroupUserFilterableString, new IReadOnlySetConverter()) }};
    public static readonly IEnumerable<object[]> SeriesFilterable = new[] { new[] { JsonConvert.DeserializeObject<TestFilterable>(SeriesFilterableString, new IReadOnlySetConverter()) }};
    public static readonly IEnumerable<object[]> SeriesUserFilterable = new[] { new object[] { JsonConvert.DeserializeObject<TestFilterable>(SeriesFilterableString, new IReadOnlySetConverter()), JsonConvert.DeserializeObject<TestFilterableUserInfo>(SeriesUserFilterableString, new IReadOnlySetConverter()) }};

    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithUserFilter_ExpectsException(TestFilterable group)
    {
        var top = new AndExpression(new AndExpression(new HasTagExpression("comedy"), 
                new NotExpression(new HasTagExpression("18 restricted"))),
            new HasWatchedEpisodesExpression());

        Assert.True(top.UserDependent);
        Assert.Throws<ArgumentNullException>(() => top.Evaluate(group, null));
    }

    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithoutUserFilter_ExpectsTrue(TestFilterable group)
    {
        var top = new AndExpression(new AndExpression(new HasTagExpression("comedy"), new NotExpression(new HasTagExpression("18 restricted"))),
            new HasVideoSourceExpression("Web"));

        Assert.False(top.UserDependent);
        Assert.True(top.Evaluate(group, null));
    }
    
    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithDateFunctionFilter_ExpectsFalse(TestFilterable group)
    {
        var top = new AndExpression(new AndExpression(new HasTagExpression("comedy"), new NotExpression(new HasTagExpression("18 restricted"))),
            new DateGreaterThanEqualsExpression(new LastAddedDateSelector(),
                new DateDiffFunction(new DateAddFunction(new TodayFunction(), TimeSpan.FromDays(1) - TimeSpan.FromMilliseconds(1)), TimeSpan.FromDays(30))));

        Assert.False(top.UserDependent);
        Assert.False(top.Evaluate(group, null));
    }
    
    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithDateFunctionFilter_ExpectsTrue(TestFilterable group)
    {
        var top = new AndExpression(new AndExpression(new HasTagExpression("comedy"), new NotExpression(new HasTagExpression("18 restricted"))),
            new DateGreaterThanEqualsExpression(new LastAddedDateSelector(), DateTime.Parse("2023-4-15")));

        Assert.False(top.UserDependent);
        Assert.True(top.Evaluate(group, null));
    }

    [Theory, MemberData(nameof(GroupUserFilterable))]
    public void GroupUserFilterable_WithUserFilter_ExpectsTrue(TestFilterable group, TestFilterableUserInfo groupUserInfo)
    {
        var top = new AndExpression(new AndExpression(new HasTagExpression("comedy"), new NotExpression(new HasTagExpression("18 restricted"))),
            new HasWatchedEpisodesExpression());

        Assert.True(top.UserDependent);
        Assert.True(top.Evaluate(group, groupUserInfo));
    }

    [Theory, MemberData(nameof(SeriesFilterable))]
    public void SeriesFilterable_WithUserFilter_ExpectsException(TestFilterable series)
    {
        var top = new AndExpression(new AndExpression(new HasTagExpression("comedy"), new NotExpression(new HasTagExpression("18 restricted"))),
            new HasWatchedEpisodesExpression());

        Assert.True(top.UserDependent);
        Assert.Throws<ArgumentNullException>(() => top.Evaluate(series, null));
    }

    [Theory, MemberData(nameof(SeriesUserFilterable))]
    public void SeriesUserFilterable_WithUserFilter_ExpectsTrue(TestFilterable series, TestFilterableUserInfo seriesUserInfo)
    {
        var top = new AndExpression(new AndExpression(new HasTagExpression("comedy"), new NotExpression(new HasTagExpression("18 restricted"))),
            new HasWatchedEpisodesExpression());

        Assert.True(top.UserDependent);
        Assert.True(top.Evaluate(series, seriesUserInfo));
    }
}
