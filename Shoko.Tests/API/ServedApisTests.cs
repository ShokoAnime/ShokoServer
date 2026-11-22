using Shoko.Abstractions.Core;
using Shoko.Server.API;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.API;

public class ServedApisTests
{
    [Theory]
    [InlineData(true, true, CoreApis.V2 | CoreApis.V3)]
    [InlineData(false, true, CoreApis.V3)]
    [InlineData(false, false, CoreApis.None)]
    public void FromSettings_ReadsBothFlags(bool v2, bool v3, CoreApis expected)
        => Assert.Equal(expected, ServedApis.FromSettings(new WebSettings { EnableAPIv2 = v2, EnableAPIv3 = v3 }));

    [Theory]
    [InlineData(CoreApis.V3, "APIv3")]
    [InlineData(CoreApis.V2 | CoreApis.V3, "APIv2 and APIv3")]
    public void Describe_NamesTheApis(CoreApis apis, string expected)
        => Assert.Equal(expected, ServedApis.Describe(apis));

    [Fact]
    public void Settle_KeepsTheFirstAnswer()
    {
        var served = new ServedApis();

        Assert.True(served.Settle(CoreApis.V3));
        Assert.False(served.Settle(CoreApis.V2));
        Assert.True(served.Serves(CoreApis.V3));
        Assert.False(served.Serves(CoreApis.V2));
    }
}
