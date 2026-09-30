using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Xunit;

namespace Shoko.Tests.Metadata;

#pragma warning disable CS0618 // The released description members are what these tests check.

/// <summary>
/// The released description members keep working as forwarders to the
/// overview members that replaced them.
/// </summary>
public class DescriptionForwarderTests
{
    #region Helpers

    private sealed class WithOverviews : IWithOverviews
    {
        public IText? DefaultOverview { get; init; }

        public IText? PreferredOverview { get; init; }

        public IReadOnlyList<IText> Overviews { get; init; } = [];
    }

    #endregion

    #region Tests

    [Fact]
    public void TheOldContainerReadsTheOverviews()
    {
        var first = new TextStub { Source = MetadataSource.Shoko, Value = "First", Language = TitleLanguage.English, LanguageCode = "en" };
        var second = new TextStub { Source = MetadataSource.Shoko, Value = "Second", Language = TitleLanguage.English, LanguageCode = "en" };
        IWithDescriptions container = new WithOverviews { DefaultOverview = first, PreferredOverview = second, Overviews = [first, second] };

        Assert.Same(first, container.DefaultDescription);
        Assert.Same(second, container.PreferredDescription);
        Assert.Equal([first, second], container.Descriptions);
    }

    #endregion
}
