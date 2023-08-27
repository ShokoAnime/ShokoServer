using Shoko.Server.Models.Filters.Info;
using Shoko.Server.Models.Filters.Logic;
using Shoko.Server.Models.Filters.User;
using Xunit;

namespace Shoko.Tests;

public class FilterTests
{
    [Fact]
    public void UserDependentInitTest()
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
        Assert.False(top.TimeDependent);
    }
}
