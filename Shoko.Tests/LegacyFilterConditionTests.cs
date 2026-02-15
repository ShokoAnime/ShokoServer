using System.Collections.Generic;
using Shoko.Server.API.v1.Models;
using Shoko.Server.Filters.Info;
using Shoko.Server.Filters.Legacy;
using Shoko.Server.Filters.Logic.Expressions;
using Shoko.Server.Models.Shoko;
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
        var expectedConditions = new List<CL_GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.NotIn,
                ConditionType = (int)CL_GroupFilterConditionType.Tag,
                ConditionParameter = "comedy"
            }
        };
        Assert.True(success);
        Assert.Equal(CL_GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void ToConditions_Tags_In_Multiple()
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
    public void ToConditions_Tags_NotIn_Multiple()
    {
        var top = new NotExpression(new OrExpression(new OrExpression(new HasTagExpression("comedy"), new HasTagExpression("shounen")), new HasTagExpression("action")));
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
    public void ToConditions_AnimeType_In_Single()
    {
        var top = new HasAnimeTypeExpression("TVSeries");
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<CL_GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.In,
                ConditionType = (int)CL_GroupFilterConditionType.AnimeType,
                ConditionParameter = "TVSeries"
            }
        };
        Assert.True(success);
        Assert.Equal(CL_GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void ToConditions_AnimeType_NotIn_Single()
    {
        var top = new NotExpression(new HasAnimeTypeExpression("TVSeries"));
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<CL_GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.NotIn,
                ConditionType = (int)CL_GroupFilterConditionType.AnimeType,
                ConditionParameter = "TVSeries"
            }
        };
        Assert.True(success);
        Assert.Equal(CL_GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void ToConditions_AnimeType_In_Multiple()
    {
        var top = new OrExpression(new OrExpression(new HasAnimeTypeExpression("TVSeries"), new HasAnimeTypeExpression("Movie")), new HasAnimeTypeExpression("OVA"));
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<CL_GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.In,
                ConditionType = (int)CL_GroupFilterConditionType.AnimeType,
                ConditionParameter = "TVSeries,Movie,OVA"
            }
        };
        Assert.True(success);
        Assert.Equal(CL_GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void ToConditions_AnimeType_NotIn_Multiple()
    {
        var top = new NotExpression(new OrExpression(new OrExpression(new HasAnimeTypeExpression("TVSeries"), new HasAnimeTypeExpression("Movie")), new HasAnimeTypeExpression("OVA")));
        var filter = new FilterPreset { Name = "Test", Expression = top };

        var success = LegacyConditionConverter.TryConvertToConditions(filter, out var conditions, out var baseCondition);
        var expectedConditions = new List<CL_GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.NotIn,
                ConditionType = (int)CL_GroupFilterConditionType.AnimeType,
                ConditionParameter = "TVSeries,Movie,OVA"
            }
        };
        Assert.True(success);
        Assert.Equal(CL_GroupFilterBaseCondition.Include, baseCondition);
        Assert.Equivalent(expectedConditions, conditions);
    }

    [Fact]
    public void ToExpression_Tags_In_Single()
    {
        var conditions = new List<CL_GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.In,
                ConditionType = (int)CL_GroupFilterConditionType.Tag,
                ConditionParameter = "comedy"
            }
        };
        var expression = LegacyConditionConverter.GetExpression(conditions, CL_GroupFilterBaseCondition.Include);
        var expected = new HasTagExpression("comedy");
        Assert.Equivalent(expected, expression);
    }

    [Fact]
    public void ToExpression_Tags_NotIn_Single()
    {
        var conditions = new List<CL_GroupFilterCondition>
        {
            new()
            {
                ConditionOperator = (int)CL_GroupFilterOperator.NotIn,
                ConditionType = (int)CL_GroupFilterConditionType.Tag,
                ConditionParameter = "comedy"
            }
        };
        var expression = LegacyConditionConverter.GetExpression(conditions, CL_GroupFilterBaseCondition.Include);
        var expected = new NotExpression(new HasTagExpression("comedy"));
        Assert.Equivalent(expected, expression);
    }
}
