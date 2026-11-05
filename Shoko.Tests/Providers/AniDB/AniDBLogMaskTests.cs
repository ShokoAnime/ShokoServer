using Shoko.Server.Providers.AniDB;
using Xunit;

namespace Shoko.Tests.Providers.AniDB;

public class AniDBLogMaskTests
{
    [Theory]
    [InlineData("AUTH user=me&pass=hunter2&protover=3", "AUTH user=me&pass=****&protover=3")]
    [InlineData("httpapi?client=animeplugin&request=mylist&user=me&pass=hunter2", "httpapi?client=animeplugin&request=mylist&user=me&pass=****")]
    [InlineData("FILE size=1&ed2k=abc&s=session1", "FILE size=1&ed2k=abc&s=****")]
    [InlineData("PING s=session1", "PING s=****")]
    [InlineData("MYLISTADD s=session1&fid=2", "MYLISTADD s=****&fid=2")]
    public void HidesThePasswordAndTheSession(string input, string expected)
        => Assert.Equal(expected, AniDBLogMask.Mask(input));

    [Theory]
    [InlineData("ANIME aid=1&tags=abc")]
    [InlineData("bypass")]
    [InlineData("httpapi?request=anime&aid=1")]
    public void LeavesEverythingElseAlone(string input)
        => Assert.Equal(input, AniDBLogMask.Mask(input));
}
