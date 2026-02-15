using System.Collections.Generic;
using Shoko.Server.API.v1.Models;
using Shoko.Abstractions.Enums;
using Shoko.Server.Filters.Info;
using Shoko.Server.Filters.Legacy;
using Shoko.Server.Filters.Logic.Expressions;
using Shoko.Server.Filters.User;
using Shoko.Server.Models.Shoko;
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
        Assert.Equal(CL_GroupFilterBaseCondition.Include, baseCondition);
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
        var expectedConditions = new List<CL_GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.In,
                ConditionType = (int)CL_GroupFilterConditionType.Tag,
                ConditionParameter = "comedy"
            },
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.NotIn,
                ConditionType = (int)CL_GroupFilterConditionType.Tag,
                ConditionParameter = "18 restricted"
            },
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.Include,
                ConditionType = (int)CL_GroupFilterConditionType.HasWatchedEpisodes,
            },
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.Exclude,
                ConditionType = (int)CL_GroupFilterConditionType.HasUnwatchedEpisodes,
            }
        };
        Assert.True(success);
        Assert.Equal(CL_GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void TryConvertToConditions_FilterWithTagInOperator_IncludeBaseCondition()
    {
        var top = new OrExpression(new OrExpression(new HasTagExpression("comedy"), new HasTagExpression("shounen")), new HasTagExpression("action"));
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<CL_GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.In,
                ConditionType = (int)CL_GroupFilterConditionType.Tag,
                ConditionParameter = "comedy,shounen,action"
            }
        };
        Assert.True(success);
        Assert.Equal(CL_GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void TryConvertToConditions_FilterWithTagInOperator_ExcludeBaseCondition()
    {
        var top = new NotExpression(new OrExpression(new OrExpression(new HasTagExpression("comedy"), new HasTagExpression("shounen")),
            new HasTagExpression("action")));
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<CL_GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.NotIn,
                ConditionType = (int)CL_GroupFilterConditionType.Tag,
                ConditionParameter = "comedy,shounen,action"
            }
        };
        Assert.True(success);
        Assert.Equal(CL_GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void TryConvertToConditions_FilterWithMultipleConditions_IncludeBaseCondition()
    {
        var top = new AndExpression(new AndExpression(new HasTagExpression("comedy"), new InSeasonExpression(2023, YearlySeason.Winter)),
            new IsFinishedExpression());
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<CL_GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.In,
                ConditionType = (int)CL_GroupFilterConditionType.Tag,
                ConditionParameter = "comedy"
            },
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.In,
                ConditionType = (int)CL_GroupFilterConditionType.Season,
                ConditionParameter = "Winter 2023"
            },
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.Include,
                ConditionType = (int)CL_GroupFilterConditionType.FinishedAiring
            }
        };
        Assert.True(success);
        Assert.Equal(CL_GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void TryConvertToConditions_FilterWithMultipleConditions_ExcludeBaseCondition()
    {
        var top = new NotExpression(new OrExpression(new OrExpression(new HasTagExpression("comedy"), new InSeasonExpression(2023, YearlySeason.Winter)),
            new IsFinishedExpression()));
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<CL_GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.In,
                ConditionType = (int)CL_GroupFilterConditionType.Tag,
                ConditionParameter = "comedy"
            },
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.In,
                ConditionType = (int)CL_GroupFilterConditionType.Season,
                ConditionParameter = "Winter 2023"
            },
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.Include,
                ConditionType = (int)CL_GroupFilterConditionType.FinishedAiring
            }
        };
        Assert.True(success);
        Assert.Equal(CL_GroupFilterBaseCondition.Exclude, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }
}
