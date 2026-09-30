using System.Linq;
using System.Text.Json.Serialization.Metadata;
using Newtonsoft.Json.Serialization;
using Shoko.Abstractions.User;
using Xunit;

namespace Shoko.Tests.Models;

/// <summary>
/// Covers that an <see cref="ApiToken"/> never serializes its key.
/// </summary>
public class ApiTokenTests
{
    [Fact]
    public void SystemTextJson_LeavesTheKeyOut()
    {
        var typeInfo = new DefaultJsonTypeInfoResolver().GetTypeInfo(typeof(ApiToken), new());

        // The property stays for the constructor to bind, but without a getter nothing writes it.
        Assert.Null(typeInfo.Properties.Single(property => property.Name == nameof(ApiToken.Token)).Get);
        Assert.NotNull(typeInfo.Properties.Single(property => property.Name == nameof(ApiToken.Device)).Get);
    }

    [Fact]
    public void NewtonsoftJson_LeavesTheKeyOut()
    {
        var contract = (JsonObjectContract)new DefaultContractResolver().ResolveContract(typeof(ApiToken));

        Assert.True(contract.Properties.Single(property => property.UnderlyingName == nameof(ApiToken.Token)).Ignored);
        Assert.False(contract.Properties.Single(property => property.UnderlyingName == nameof(ApiToken.Device)).Ignored);
    }
}
