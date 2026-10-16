using Microsoft.AspNetCore.Cors.Infrastructure;
using Shoko.Server.API;
using Xunit;

namespace Shoko.Tests.API;

public class CorsPolicyTests
{
    private static CorsPolicy Build(params string[] origins)
    {
        var builder = new CorsPolicyBuilder();
        APIExtensions.ConfigureCors(builder, origins);
        return builder.Build();
    }

    [Fact]
    public void NoOriginOrAWildcardAllowsAnyOrigin()
    {
        Assert.True(Build().AllowAnyOrigin);
        Assert.True(Build(" ", "*").AllowAnyOrigin);
    }

    [Fact]
    public void ListedOriginsAreTheOnlyOnesAllowedAndMatchWithoutATrailingSlash()
    {
        var policy = Build("http://host:3316/", " https://web.example ");

        Assert.False(policy.AllowAnyOrigin);
        Assert.Equal(["http://host:3316", "https://web.example"], policy.Origins);
        Assert.True(policy.AllowAnyMethod);
        Assert.True(policy.AllowAnyHeader);
    }
}
