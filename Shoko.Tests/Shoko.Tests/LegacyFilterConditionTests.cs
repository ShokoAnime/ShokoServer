using System.Collections.Generic;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Filters.Info;
using Shoko.Server.Filters.Legacy;
using Shoko.Server.Filters.Logic;
using Shoko.Server.Filters.Logic.Expressions;
using Shoko.Server.Models;
using Xunit;

namespace Shoko.Tests;

public class LegacyFilterConditionTests
{
    [Fact]
    public void ToConditions_Tags_NotIn_Single()
    {
        var top = new NotExpression(new HasTagExpression("comedy"));
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.NotIn,
                ConditionType = (int)GroupFilterConditionType.Tag,
                ConditionParameter = "comedy"
            }
        };
        Assert.True(success);
        Assert.Equal(GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void ToConditions_Tags_In_Multiple()
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
    public void ToConditions_Tags_NotIn_Multiple()
    {
        var top = new NotExpression(new OrExpression(new OrExpression(new HasTagExpression("comedy"), new HasTagExpression("shounen")), new HasTagExpression("action")));
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
    public void ToConditions_AnimeType_In_Single()
    {
        var top = new HasAnimeTypeExpression("TVSeries");
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.In,
                ConditionType = (int)GroupFilterConditionType.AnimeType,
                ConditionParameter = "TVSeries"
            }
        };
        Assert.True(success);
        Assert.Equal(GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void ToConditions_AnimeType_NotIn_Single()
    {
        var top = new NotExpression(new HasAnimeTypeExpression("TVSeries"));
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.NotIn,
                ConditionType = (int)GroupFilterConditionType.AnimeType,
                ConditionParameter = "TVSeries"
            }
        };
        Assert.True(success);
        Assert.Equal(GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void ToConditions_AnimeType_In_Multiple()
    {
        var top = new OrExpression(new OrExpression(new HasAnimeTypeExpression("TVSeries"), new HasAnimeTypeExpression("Movie")), new HasAnimeTypeExpression("OVA"));
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.In,
                ConditionType = (int)GroupFilterConditionType.AnimeType,
                ConditionParameter = "TVSeries,Movie,OVA"
            }
        };
        Assert.True(success);
        Assert.Equal(GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void ToConditions_AnimeType_NotIn_Multiple()
    {
        var top = new NotExpression(new OrExpression(new OrExpression(new HasAnimeTypeExpression("TVSeries"), new HasAnimeTypeExpression("Movie")), new HasAnimeTypeExpression("OVA")));
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.NotIn,
                ConditionType = (int)GroupFilterConditionType.AnimeType,
                ConditionParameter = "TVSeries,Movie,OVA"
            }
        };
        Assert.True(success);
        Assert.Equal(GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void ToExpression_Tags_In_Single()
    {
        var conditions = new List<GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.In,
                ConditionType = (int)GroupFilterConditionType.Tag,
                ConditionParameter = "comedy"
            }
        };
        var expression = LegacyConditionConverter.GetExpression(conditions, GroupFilterBaseCondition.Include);
        var expected = new HasTagExpression("comedy");
        Assert.Equivalent(expected, expression);
    }
    
    [Fact]
    public void ToExpression_Tags_NotIn_Single()
    {
        var conditions = new List<GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)GroupFilterOperator.NotIn,
                ConditionType = (int)GroupFilterConditionType.Tag,
                ConditionParameter = "comedy"
            }
        };
        var expression = LegacyConditionConverter.GetExpression(conditions, GroupFilterBaseCondition.Include);
        var expected = new NotExpression(new HasTagExpression("comedy"));
        Assert.Equivalent(expected, expression);
    }
}
