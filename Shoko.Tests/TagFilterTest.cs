using System.Collections.Generic;
using Shoko.Server;
using Xunit;

// ReSharper disable StringLiteralTypo

namespace Shoko.Tests
{
    public class TagFilterTest
    {
        private readonly ITestOutputHelper _console;

        public TagFilterTest(ITestOutputHelper console)
        {
            _console = console;
        }

        private static IEnumerable<string> Input =>
            new[]
            {
                "comedy",
                "Comedy",
                "horror",
                "18 restricted",
                "large breasts",
                "japan",
                "violence",
                "source material",
                "manga",
                "fantasy",
                "shounen",
                "Earth",
                "Asia",
                "noitamina",
                "cgi",
                "3DCG",
                "long episodes",
                "first girl wins",
                "alternative past",
                "past",
            };

        private static IEnumerable<string> InputNoSource =>
            new[]
            {
                "horror",
                "Horror",
                "18 restricted",
                "large breasts",
                "japan",
                "violence",
                "fantasy",
                "shounen",
                "Earth",
                "Asia",
                "noitamina",
                "cgi",
                "3DCG",
                "long episodes",
                "first girl wins",
            };

        public static TheoryData<TagFilter.Filter, string[], string[]> FullListCases() => new()
        {
            // Full Test
            {
                TagFilter.Filter.Genre,
                [.. Input],
                [
                    "large breasts", "japan", "source material", "manga", "Earth", "Asia", "noitamina", "cgi", "3DCG", "long episodes",
                    "first girl wins", "alternative past", "past",
                ]
            },
            // Full Test w/ 'original work'
            {
                TagFilter.Filter.Genre,
                [.. Input, "original work"],
                [
                    "large breasts", "japan", "source material", "manga", "Earth", "Asia", "noitamina", "cgi", "3DCG", "long episodes",
                    "first girl wins", "alternative past", "past",
                ]
            },
            // Full Test w/o Source
            {
                TagFilter.Filter.Genre,
                [.. InputNoSource],
                ["large breasts", "japan", "Earth", "Asia", "noitamina", "cgi", "3DCG", "long episodes", "first girl wins", "original work"]
            },
            // Full Test Inverted
            {
                TagFilter.Filter.Invert | TagFilter.Filter.Source | TagFilter.Filter.Genre | TagFilter.Filter.Setting,
                [.. Input],
                ["comedy", "horror", "18 restricted", "japan", "violence", "manga", "fantasy", "shounen", "alternative past", "past"]
            },
            // Source Exclusion with Full List
            {
                TagFilter.Filter.Source,
                [.. Input],
                [
                    "comedy", "horror", "18 restricted", "large breasts", "japan", "violence", "source material", "fantasy", "shounen", "Earth",
                    "Asia", "noitamina", "cgi", "3DCG", "long episodes", "first girl wins", "alternative past", "past",
                ]
            },
            // Source Exclusion with Full List w/o Source
            {
                TagFilter.Filter.Source,
                [.. InputNoSource],
                [
                    "horror", "18 restricted", "large breasts", "japan", "violence", "fantasy", "shounen", "Earth", "Asia", "noitamina",
                    "cgi", "3DCG", "long episodes", "first girl wins",
                ]
            },
        };

        [Theory]
        [MemberData(nameof(FullListCases))]
        public void TestFullLists(TagFilter.Filter filter, string[] input, string[] expected)
        {
            var actual = TagFilter.String.ProcessTags(filter, input);

            _console.WriteLine("Filter: {0}", filter);
            _console.WriteLine("Expected: [{0}]", string.Join(", ", expected));
            _console.WriteLine("Actual: [{0}]", string.Join(", ", actual));
            Assert.Equal(expected, actual);
        }

        [Theory]
        // Source Exclusion
        [InlineData(TagFilter.Filter.Source, new[] { "original work" }, new string[] { })]
        // Source Exclusion with No Source
        [InlineData(TagFilter.Filter.Genre, new string[] { }, new[] { "original work" })]
        // Source Inclusion with Source and Original Work
        [InlineData(TagFilter.Filter.Source | TagFilter.Filter.Invert, new[] { "manga", "original work" }, new[] { "manga" })]
        // Source Inclusion w/o Source
        [InlineData(TagFilter.Filter.Source | TagFilter.Filter.Invert, new[] { "action" }, new[] { "original work" })]
        // Inverted w/o Source
        [InlineData(TagFilter.Filter.Genre | TagFilter.Filter.Invert, new[] { "action" }, new[] { "action" })]
        // AniDB Internal with Replacement Source
        [InlineData(TagFilter.Filter.AnidbInternal, new[] { "source material", "original work" }, new[] { "original work" })]
        // AniDB Internal with Source
        [InlineData(
            TagFilter.Filter.AnidbInternal | TagFilter.Filter.Source,
            new[] { "source material", "original work" },
            new string[] { }
        )]
        // AniDB Internal with Overlapping Source
        [InlineData(TagFilter.Filter.AnidbInternal, new[] { "action", "manga", "original work" }, new[] { "action", "manga" })]
        // Art Style Exclusion
        [InlineData(TagFilter.Filter.ArtStyle | TagFilter.Filter.Source, new[] { "censored", "cgi" }, new string[] { })]
        // Plot Exclusion
        [InlineData(TagFilter.Filter.Plot | TagFilter.Filter.Source, new[] { "everybody dies", "first girl wins" }, new string[] { })]
        // Settings Exclusion
        [InlineData(
            TagFilter.Filter.Setting | TagFilter.Filter.Source,
            new[] { "meiji period", "meiji era", "japan", "high school" },
            new string[] { }
        )]
        // Misc Exclusion
        [InlineData(TagFilter.Filter.Misc | TagFilter.Filter.Source, new[] { "previews suck" }, new string[] { })]
        // AniDB Internal Exclusion
        [InlineData(
            TagFilter.Filter.AnidbInternal | TagFilter.Filter.Source,
            new[]
            {
                "old animetags", "missing frogs", "predominantly gay", "adapted into live action", "weekly monster", "needs removed", "to be merged",
                "to be improved", "to be split and deleted", "to be moved", "to be split"
            },
            new string[] { }
        )]
        public void TestShortLists(TagFilter.Filter filter, string[] input, string[] expected)
            => Assert.Equal(expected, TagFilter.String.ProcessTags(filter, input));
    }
}
