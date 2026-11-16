using Shoko.Server.API.Authentication;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
///   Covers the setup token: required only when set, and matched exactly
///   as the API key.
/// </summary>
public class SetupTokenTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoPassword_RequiresNone_AndMatchesNothing(string? token)
    {
        var setupToken = new SetupToken(token);

        Assert.False(setupToken.IsRequired);
        Assert.False(setupToken.Matches(token));
    }

    [Theory]
    [InlineData("hunter2", true)]
    [InlineData("Hunter2", false)]
    [InlineData(" hunter2", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TheKey_MatchesOnlyTheTokenExactly(string? key, bool expected)
        => Assert.Equal(expected, new SetupToken("hunter2").Matches(key));
}
