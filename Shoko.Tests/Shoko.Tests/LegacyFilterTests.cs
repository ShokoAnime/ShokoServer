using System.Collections.Generic;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Filters;
using Shoko.Server.Filters.Info;
using Shoko.Server.Filters.Legacy;
using Shoko.Server.Filters.Logic;
using Shoko.Server.Filters.User;
using Shoko.Server.Models;
using Xunit;

namespace Shoko.Tests;

public class LegacyFilterTests
{
    [Fact]
    public void TryConvertToConditions_InvalidFilter_ExpectsNull()
    {
        var top = new OrExpression(new AndExpression(new HasTagExpression("comedy"), 
                new NotExpression(new HasTagExpression("18 restricted"))),
            new HasWatchedEpisodesExpression());
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        Assert.False(success);
        Assert.Equal(GroupFilterBaseCondition.Include, baseCondition);
        Assert.Null(conditions);
    }

    [Fact]
    public void TryConvertToConditions_FilterWithTagIncludeAndExclude()
    {
        var top = new AndExpression(new AndExpression(new AndExpression(new HasTagExpression("comedy"),
                new NotExpression(new HasTagExpression("18 restricted"))),
            new HasWatchedEpisodesExpression()), new NotExpression(new HasUnwatchedEpisodesExpression()));
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.In,
                ConditionType = (int)GroupFilterConditionType.Tag,
                ConditionParameter = "comedy"
            },
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.NotIn,
                ConditionType = (int)GroupFilterConditionType.Tag,
                ConditionParameter = "18 restricted"
            },
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.Include,
                ConditionType = (int)GroupFilterConditionType.HasWatchedEpisodes,
            },
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.Exclude,
                ConditionType = (int)GroupFilterConditionType.HasUnwatchedEpisodes,
            }
        };
        Assert.True(success);
        Assert.Equal(GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void TryConvertToConditions_FilterWithTagInOperator_IncludeBaseCondition()
    {
        var top = new OrExpression(new OrExpression(new HasTagExpression("comedy"), new HasTagExpression("shounen")), new HasTagExpression("action"));
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.In,
                ConditionType = (int)GroupFilterConditionType.Tag,
                ConditionParameter = "comedy,shounen,action"
            }
        };
        Assert.True(success);
        Assert.Equal(GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void TryConvertToConditions_FilterWithTagInOperator_ExcludeBaseCondition()
    {
        var top = new NotExpression(new OrExpression(new OrExpression(new HasTagExpression("comedy"), new HasTagExpression("shounen")),
            new HasTagExpression("action")));
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.NotIn,
                ConditionType = (int)GroupFilterConditionType.Tag,
                ConditionParameter = "comedy,shounen,action"
            }
        };
        Assert.True(success);
        Assert.Equal(GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void TryConvertToConditions_FilterWithMultipleConditions_IncludeBaseCondition()
    {
        var top = new AndExpression(new AndExpression(new HasTagExpression("comedy"), new InSeasonExpression(2023, AnimeSeason.Winter)),
            new IsFinishedExpression());
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.In,
                ConditionType = (int)GroupFilterConditionType.Tag,
                ConditionParameter = "comedy"
            },
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.In,
                ConditionType = (int)GroupFilterConditionType.Season,
                ConditionParameter = "Winter 2023"
            },
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.Include,
                ConditionType = (int)GroupFilterConditionType.FinishedAiring
            }
        };
        Assert.True(success);
        Assert.Equal(GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void TryConvertToConditions_FilterWithMultipleConditions_ExcludeBaseCondition()
    {
        var top = new NotExpression(new OrExpression(new OrExpression(new HasTagExpression("comedy"), new InSeasonExpression(2023, AnimeSeason.Winter)),
            new IsFinishedExpression()));
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.In,
                ConditionType = (int)GroupFilterConditionType.Tag,
                ConditionParameter = "comedy"
            },
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.In,
                ConditionType = (int)GroupFilterConditionType.Season,
                ConditionParameter = "Winter 2023"
            },
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.Include,
                ConditionType = (int)GroupFilterConditionType.FinishedAiring
            }
        };
        Assert.True(success);
        Assert.Equal(GroupFilterBaseCondition.Exclude, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }
}
