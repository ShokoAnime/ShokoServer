using System;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.UI;
using Shoko.Server.API.Swagger;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
///   The API spec names an enum's values as the wire sends them.
/// </summary>
public class EnumSchemaFilterTests
{
    private enum Sample
    {
        [EnumMember(Value = "section-container")]
        SectionContainer,

        Plain,
    }

    [Fact]
    public void WireNames_TakeTheEnumMemberValue_ElseTheName()
        => Assert.Equal(["section-container", "Plain"], EnumSchemaFilter<Sample>.GetWireNames());

    [Fact]
    public void WireNames_MatchWhatTheApiSends()
    {
        var sent = Enum.GetValues<UiElementKind>()
            .Select(kind => JsonConvert.DeserializeObject<string>(JsonConvert.SerializeObject(kind, new StringEnumConverter()))!)
            .Order();

        Assert.Equal(sent, EnumSchemaFilter<UiElementKind>.GetWireNames().Order());
    }
}
