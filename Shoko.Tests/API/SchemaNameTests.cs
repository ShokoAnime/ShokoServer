using System;
using System.Collections.Generic;
using Shoko.Server.API;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Xunit;

namespace Shoko.Tests.API;

public class SchemaNameTests
{
    [Theory]
    [InlineData(typeof(Episode), "APIv3.Shoko.Episode")]
    [InlineData(typeof(ListResult<Episode>), "APIv3.ListResultOfAPIv3.Shoko.Episode")]
    [InlineData(typeof(Dictionary<string, int>), "DictionaryOfStringAndInt32")]
    [InlineData(typeof(ListResult<Dictionary<string, Episode>>), "APIv3.ListResultOfDictionaryOfStringAndAPIv3.Shoko.Episode")]
    [InlineData(typeof(Dictionary<ListResult<Episode>, string>), "DictionaryOfAPIv3.ListResultOfAPIv3.Shoko.EpisodeEndAndString")]
    [InlineData(typeof(Tuple<List<int>, List<string>, bool>), "TupleOfListOfInt32EndAndListOfStringEndAndBoolean")]
    public void GenericsAreNamedWithOfAndAndEnd(Type type, string expected)
        => Assert.Equal(expected, APIExtensions.GetTypeName(type));
}
