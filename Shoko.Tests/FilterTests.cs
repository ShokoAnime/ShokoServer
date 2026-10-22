using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Shoko.Abstractions.Filtering.Expressions.Files;
using Shoko.Abstractions.Filtering.Expressions.Functions;
using Shoko.Abstractions.Filtering.Expressions.Info;
using Shoko.Abstractions.Filtering.Expressions.Logic.DateTimes;
using Shoko.Abstractions.Filtering.Expressions.Logic.Expressions;
using Shoko.Abstractions.Filtering.Expressions.Logic.Numbers;
using Shoko.Abstractions.Filtering.Expressions.Logic.Strings;
using Shoko.Abstractions.Filtering.Expressions.Selectors.DateSelectors;
using Shoko.Abstractions.Filtering.Expressions.Selectors.NumberSelectors;
using Shoko.Abstractions.Filtering.Expressions.Selectors.StringSelectors;
using Shoko.Abstractions.Filtering.Expressions.User;
using Xunit;

namespace Shoko.Tests;

public class FilterTests
{
    #region TestData

    private const string GroupFilterableString =
        "{\"MissingEpisodes\":0,\"MissingEpisodesCollecting\":0,\"AnidbTags\":[\"Earth\",\"Japan\",\"Asia\",\"friendship\",\"daily life\",\"high school\",\"school life\",\"comedy\",\"Kyoto\",\"dynamic\",\"themes\",\"source material\",\"setting\",\"elements\",\"place\",\"manga\",\"funny expressions\",\"storytelling\",\"character driven\",\"facial distortion\",\"narration\",\"origin\",\"episodic\",\"Japanese production\"],\"CustomTags\":[],\"Years\":[2022],\"Seasons\":[{\"Item1\":2022,\"Item2\":1}],\"HasTvDBLink\":false,\"HasMissingTvDbLink\":false,\"HasTmdbLink\":true,\"HasMissingTmdbLink\":false,\"HasTraktLink\":false,\"HasMissingTraktLink\":true,\"IsFinished\":true,\"AirDate\":\"2022-04-06T20:00:00\",\"LastAirDate\":\"2022-06-22T20:00:00\",\"AddedDate\":\"2023-05-05T14:42:24.4477733\",\"LastAddedDate\":\"2023-05-05T13:37:50.32298\",\"EpisodeCount\":12,\"TotalEpisodeCount\":12,\"LowestAniDBRating\":7.4,\"HighestAniDBRating\":7.4,\"VideoSources\":[\"Web\"],\"AnimeTypes\":[\"TVSeries\"],\"AudioLanguages\":[\"japanese\"],\"SubtitleLanguages\":[\"english\"]}";
    private const string GroupUserFilterableString =
        "{\"IsFavorite\":false,\"WatchedEpisodes\":12,\"UnwatchedEpisodes\":0,\"HasVotes\":false,\"HasPermanentVotes\":false,\"MissingPermanentVotes\":false,\"WatchedDate\":\"2023-05-05T13:42:13.3933582\",\"LastWatchedDate\":\"2023-05-05T13:43:21.3729042\",\"LowestUserRating\":0.0,\"HighestUserRating\":0.0}";

    #endregion

    public static readonly IEnumerable<object[]> GroupFilterable = new[] { new[] { JsonConvert.DeserializeObject<TestFilterable>(GroupFilterableString, new IReadOnlySetConverter())! } };
    public static readonly IEnumerable<object[]> GroupUserFilterable = new[] { new object[] { JsonConvert.DeserializeObject<TestFilterable>(GroupFilterableString, new IReadOnlySetConverter())!, JsonConvert.DeserializeObject<TestFilterableUserInfo>(GroupUserFilterableString, new IReadOnlySetConverter())! } };

    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithUserFilter_ExpectsException(TestFilterable group)
    {
        var top = new AndExpression(new AndExpression(new HasTagExpression("comedy"),
                new NotExpression(new HasTagExpression("18 restricted"))),
            new HasWatchedEpisodesExpression());

        Assert.True(top.UserDependent);
        Assert.Throws<ArgumentNullException>(() => top.Evaluate(group, null, DateTime.Now));
    }

    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithoutUserFilter_ExpectsTrue(TestFilterable group)
    {
        var top = new AndExpression(new AndExpression(new HasTagExpression("comedy"), new NotExpression(new HasTagExpression("18 restricted"))),
            new HasVideoSourceExpression("Web"));

        Assert.False(top.UserDependent);
        Assert.True(top.Evaluate(group, null, DateTime.Now));
    }

    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithScope_AnswersAsItsContents(TestFilterable group)
    {
        var scoped = new ScopeExpression(new AndExpression(new NotExpression(new HasTagExpression("18 restricted")), new HasWatchedEpisodesExpression()));

        Assert.True(scoped.UserDependent);
        Assert.True(new ScopeExpression(new HasTagExpression("comedy")).Evaluate(group, null, DateTime.Now));
        Assert.False(new ScopeExpression(new HasTagExpression("18 restricted")).Evaluate(group, null, DateTime.Now));
        Assert.False(new ScopeExpression().Evaluate(group, null, DateTime.Now));
        Assert.True(new ScopeExpression(new HasTagExpression("comedy"), "Funny ones").Evaluate(group, null, DateTime.Now));
    }

    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithSwitch_AnswersAsItsStateAndContents(TestFilterable group)
    {
        Assert.False(new SwitchExpression().Evaluate(group, null, DateTime.Now));
        Assert.False(new SwitchExpression(new HasTagExpression("comedy"), false).Evaluate(group, null, DateTime.Now));
        Assert.False(new SwitchExpression(null, true).Evaluate(group, null, DateTime.Now));
        Assert.True(new SwitchExpression(new HasTagExpression("comedy"), true, "Funny ones").Evaluate(group, null, DateTime.Now));
        Assert.False(new SwitchExpression(new HasTagExpression("18 restricted"), true).Evaluate(group, null, DateTime.Now));
        Assert.Null(new SwitchExpression(null, true, string.Empty).Label);
    }

    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithSwitchedOffOrMissingSide_LeavesItOut(TestFilterable group)
    {
        var off = new SwitchExpression(new HasTagExpression("comedy"), false);
        var offAroundUserData = new SwitchExpression(new HasWatchedEpisodesExpression(), false);

        Assert.True(new AndExpression(new HasTagExpression("comedy"), off).Evaluate(group, null, DateTime.Now));
        Assert.True(new AndExpression(new HasTagExpression("comedy"), new SwitchExpression(null, true)).Evaluate(group, null, DateTime.Now));
        Assert.True(new AndExpression(offAroundUserData, new HasTagExpression("comedy")).Evaluate(group, null, DateTime.Now));
        Assert.False(new AndExpression(new HasTagExpression("comedy"), offAroundUserData).UserDependent);
        Assert.False(new OrExpression(new HasTagExpression("18 restricted"), new NotExpression(off)).Evaluate(group, null, DateTime.Now));
        Assert.True(new EqualsExpression(new HasTagExpression("comedy"), off).Evaluate(group, null, DateTime.Now));
        Assert.True(new AndExpression(new HasTagExpression("comedy"), new ScopeExpression(new SwitchExpression(off, true))).Evaluate(group, null, DateTime.Now));
        Assert.False(new NotExpression(off).Evaluate(group, null, DateTime.Now));
        Assert.False(new AndExpression(off, off).Evaluate(group, null, DateTime.Now));
        Assert.True(new AndExpression { Left = new HasTagExpression("comedy") }.Evaluate(group, null, DateTime.Now));
        Assert.True(new XorExpression { Left = new HasTagExpression("comedy") }.Evaluate(group, null, DateTime.Now));
        Assert.True(new AndExpression(new HasTagExpression("comedy"), new NotExpression()).Evaluate(group, null, DateTime.Now));
    }

    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithUnsetParameter_LeavesItOut(TestFilterable group)
    {
        var comedy = new HasTagExpression("comedy");

        Assert.True(new AndExpression(comedy, new NumberEqualsExpression { Left = new EpisodeCountSelector() }).Evaluate(group, null, DateTime.Now));
        Assert.False(new AndExpression(comedy, new NumberEqualsExpression { Left = new EpisodeCountSelector(), Parameter = 0 }).Evaluate(group, null, DateTime.Now));
        Assert.True(new StringContainsExpression { Left = new NameSelector(), Parameter = string.Empty }.IsApplicable);
        Assert.True(new AndExpression(comedy, new ConstantExpression()).Evaluate(group, null, DateTime.Now));
        Assert.False(new ConstantExpression().Evaluate(group, null, DateTime.Now));
        Assert.True(new EqualsExpression { Left = comedy }.Evaluate(group, null, DateTime.Now));
        Assert.False(new EqualsExpression { Left = new HasTagExpression("18 restricted") }.Evaluate(group, null, DateTime.Now));
        Assert.True(new AndExpression(comedy, new InSeasonExpression { Year = 2022 }).Evaluate(group, null, DateTime.Now));
        Assert.True(new AndExpression(comedy, new DateGreaterThanExpression { Left = new DateAddFunction { Left = new AirDateSelector() }, Parameter = DateTime.MaxValue }).Evaluate(group, null, DateTime.Now));
    }

    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithDateFunctionFilter_ExpectsFalse(TestFilterable group)
    {
        var top = new AndExpression(new AndExpression(new HasTagExpression("comedy"), new NotExpression(new HasTagExpression("18 restricted"))),
            new DateGreaterThanEqualsExpression(new LastAddedDateSelector(),
                new DateDiffFunction(new DateAddFunction(new TodayFunction(), TimeSpan.FromDays(1) - TimeSpan.FromMilliseconds(1)), TimeSpan.FromDays(30))));

        Assert.False(top.UserDependent);
        Assert.False(top.Evaluate(group, null, DateTime.Now));
    }

    [Theory, MemberData(nameof(GroupFilterable))]
    public void GroupFilterable_WithDateFunctionFilter_ExpectsTrue(TestFilterable group)
    {
        var top = new AndExpression(new AndExpression(new HasTagExpression("comedy"), new NotExpression(new HasTagExpression("18 restricted"))),
            new DateGreaterThanEqualsExpression(new LastAddedDateSelector(), DateTime.Parse("2023-4-15")));

        Assert.False(top.UserDependent);
        Assert.True(top.Evaluate(group, null, DateTime.Now));
    }

    [Theory, MemberData(nameof(GroupUserFilterable))]
    public void GroupUserFilterable_WithUserFilter_ExpectsTrue(TestFilterable group, TestFilterableUserInfo groupUserInfo)
    {
        var top = new AndExpression(new AndExpression(new HasTagExpression("comedy"), new NotExpression(new HasTagExpression("18 restricted"))),
            new HasWatchedEpisodesExpression());

        Assert.True(top.UserDependent);
        Assert.True(top.Evaluate(group, groupUserInfo, DateTime.Now));
    }
}
