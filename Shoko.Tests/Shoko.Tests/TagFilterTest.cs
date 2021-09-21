using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Server;
using Xunit;
using Xunit.Abstractions;
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
                "comedy", "Comedy", "horror", "18 restricted", "large breasts", "japan", "violence", "original work", "manga", "fantasy", "shounen", "earth", "asia", "noitamina", "cgi", "long episodes",
                "first girl wins", "alternative past", "past"
            };

        private static IEnumerable<string> InputNoSource =>
            new[] { "horror", "Horror", "18 restricted", "large breasts", "japan", "violence", "fantasy", "shounen", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins" };
        public static IEnumerable<object[]> Data => new[]
        {
            new object[] {TagFilter.Filter.AnidbInternal, new[] {"new"}, new List<string> {"original work"}},
            new object[] {TagFilter.Filter.AnidbInternal | TagFilter.Filter.Source, new[] {"original work", "new"}, new List<string>()},
            new object[] {TagFilter.Filter.AnidbInternal, new[] {"action", "manga", "original work"}, new List<string> {"action", "manga"}},
            new object[] {TagFilter.Filter.Source | TagFilter.Filter.Invert, new[] {"manga", "original work"}, new List<string> {"manga"}},
            new object[] {TagFilter.Filter.Source | TagFilter.Filter.Invert, new[] {"action"}, new List<string> {"original work"}},
            new object[] {TagFilter.Filter.Genre | TagFilter.Filter.Invert, new[] {"action"}, new List<string> {"action"}},
            new object[] {TagFilter.Filter.AnidbInternal | TagFilter.Filter.Genre, new[] {"original work"}, new List<string> {"original work"}},
            new object[] {TagFilter.Filter.AnidbInternal, new[] {"new", "original work"}, new List<string> {"original work"}},
        };

        [Theory, MemberData(nameof(Data))]
        public void Test(TagFilter.Filter filter, string[] input, List<string> expected)
        {
            var actual = TagFilter.String.ProcessTags(filter, input);
            Assert.Equal(expected, actual);
        }
        
        [Fact(DisplayName = "Full Test")]
        public void TestFullList()
        {
            var actual = TagFilter.String.ProcessTags(TagFilter.Filter.Genre, Input);
            var expected = new List<string>
            {
                "large breasts",
                "japan",
                "original work",
                "manga",
                "earth",
                "asia",
                "noitamina",
                "cgi",
                "long episodes",
                "first girl wins",
                "alternative past"
            };
            
            _console.WriteLine("Expected: [{0}]", string.Join(", ", expected));
            _console.WriteLine("Actual: [{0}]", string.Join(", ", actual));
            Assert.Equal(expected, actual);
        }

        [Fact(DisplayName = "Full Test w/ 'new'")]
        public void TestFullListWithNew()
        {
            var actual = TagFilter.String.ProcessTags(TagFilter.Filter.Genre, Input.Concat(new[] { "new" }));
            var expected = new List<string>
            {
                "large breasts",
                "japan",
                "original work",
                "manga",
                "earth",
                "asia",
                "noitamina",
                "cgi",
                "long episodes",
                "first girl wins",
                "alternative past",
            };

            _console.WriteLine("Expected: [{0}]", string.Join(", ", expected));
            _console.WriteLine("Actual: [{0}]", string.Join(", ", actual));
            Assert.Equal(expected, actual);
        }
        
        [Fact(DisplayName = "Full Test w/o Source")]
        public void TestFullListNoSource()
        {
            var actual = TagFilter.String.ProcessTags(TagFilter.Filter.Genre, InputNoSource);
            var expected = new List<string>
            {
                "large breasts",
                "japan",
                "earth",
                "asia",
                "noitamina",
                "cgi",
                "long episodes",
                "first girl wins",
                "original work",
            };

            _console.WriteLine("Expected: [{0}]", string.Join(", ", expected));
            _console.WriteLine("Actual: [{0}]", string.Join(", ", actual));
            Assert.Equal(expected, actual);
        }
        
        [Fact(DisplayName = "Full Test Inverted")]
        public void TestFullListInverted()
        {
            TagFilter.Filter filter = TagFilter.Filter.Invert | TagFilter.Filter.Source | TagFilter.Filter.Genre | TagFilter.Filter.Setting;
            var actual = TagFilter.String.ProcessTags(filter, Input);
            var expected = new List<string>
            {
                "comedy",
                "horror",
                "18 restricted",
                "japan",
                "violence",
                "manga",
                "fantasy",
                "shounen",
                "alternative past",
            };

            _console.WriteLine(
                "AniDB Internal: {0}, Art Style: {1}, Genre: {2}, Inverted: {3}, Misc: {4}, Plot: {5}, Programming: {6}, Setting: {7}, Source: {8}", filter.HasFlag(TagFilter.Filter.AnidbInternal), filter.HasFlag(TagFilter.Filter.ArtStyle),
                filter.HasFlag(TagFilter.Filter.Genre), filter.HasFlag(TagFilter.Filter.Invert), filter.HasFlag(TagFilter.Filter.Misc), filter.HasFlag(TagFilter.Filter.Plot), filter.HasFlag(TagFilter.Filter.Programming),
                filter.HasFlag(TagFilter.Filter.Setting), filter.HasFlag(TagFilter.Filter.Source)
            );
            _console.WriteLine("Expected: [{0}]", string.Join(", ", expected));
            _console.WriteLine("Actual: [{0}]", string.Join(", ", actual));
            Assert.Equal(expected, actual);
        }

        [Fact(DisplayName = "Source Exclusion with Full List")]
        public void TestSourceFullList()
        {
            var actual = TagFilter.String.ProcessTags(TagFilter.Filter.Source, Input);
            var expected = new List<string>
            {
                "comedy",
                "horror",
                "18 restricted",
                "large breasts",
                "japan",
                "violence",
                "fantasy",
                "shounen",
                "earth",
                "asia",
                "noitamina",
                "cgi",
                "long episodes",
                "first girl wins",
                "alternative past",
            };

            _console.WriteLine("Expected: [{0}]", string.Join(", ", expected));
            _console.WriteLine("Actual: [{0}]", string.Join(", ", actual));
            Assert.Equal(expected, actual);
        }
        
        [Fact(DisplayName = "Source Exclusion with Full List Input w/o Source")]
        public void TestSourceFullListNoSource()
        {
            var actual = TagFilter.String.ProcessTags(TagFilter.Filter.Source, InputNoSource);
            var expected = new List<string>
            {
                "horror",
                "18 restricted",
                "large breasts",
                "japan",
                "violence",
                "fantasy",
                "shounen",
                "earth",
                "asia",
                "noitamina",
                "cgi",
                "long episodes",
                "first girl wins",
            };

            _console.WriteLine("Expected: [{0}]", string.Join(", ", expected));
            _console.WriteLine("Actual: [{0}]", string.Join(", ", actual));
            Assert.Equal(expected, actual);
        }

        [Fact(DisplayName = "Source Replacement")]
        public void TestSourceReplacement()
        {
            Assert.Equal(new List<string> { "original work" }, TagFilter.String.ProcessTags(TagFilter.Filter.Genre, new[] { "new" }));
        }
         
        [Fact(DisplayName = "Source Exclusion")]
        public void TestSource()
        {
            Assert.Equal(new List<string>(), TagFilter.String.ProcessTags(TagFilter.Filter.Source, new[] { "new" }));
        }

        [Fact(DisplayName = "Source Exclusion with No Source in Input")]
        public void TestSourceNoSource()
        {
            Assert.Equal(new List<string> { "original work" }, TagFilter.String.ProcessTags(TagFilter.Filter.Genre, new List<string>()));
        }

        [Fact(DisplayName = "Art Style Exclusion")]
        public void TestArtStyle()
        {
            Assert.Equal(new List<string>(), TagFilter.String.ProcessTags(TagFilter.Filter.ArtStyle | TagFilter.Filter.Source, new[] { "censored", "cgi" }));
        }

        [Fact(DisplayName = "Plot Exclusion")]
        public void TestPlot()
        {
            Assert.Equal(new List<string>(), TagFilter.String.ProcessTags(TagFilter.Filter.Plot | TagFilter.Filter.Source, new[] { "everybody dies", "first girl wins" }));
        }
        
        [Fact(DisplayName = "Settings Exclusion")]
        public void TestSettings()
        {
            Assert.Equal(new List<string>(), TagFilter.String.ProcessTags(TagFilter.Filter.Setting | TagFilter.Filter.Source, new[] { "meiji period", "meiji era", "japan", "high school" }));
        }

        [Fact(DisplayName = "Misc Exclusion")]
        public void TestMisc()
        {
            Assert.Equal(new List<string>(), TagFilter.String.ProcessTags(TagFilter.Filter.Misc | TagFilter.Filter.Source, new[] { "previews suck" }));
        }
        
        [Fact(DisplayName = "AniDB Internal Exclusion")]
        public void TestAniDBInternal()
        {
            Assert.Equal(
                new List<string>(),
                TagFilter.String.ProcessTags(
                    TagFilter.Filter.AnidbInternal | TagFilter.Filter.Source,
                    new[]
                    {
                        "old animetags", "missing frogs", "predominantly gay", "adapted into live action", "weekly monster", "needs removed", "to be merged", "to be improved", "to be split and deleted",
                        "to be moved", "to be split"
                    }
                )
            );
        }
        
        [Fact(DisplayName = "Speed Test (Ideally <600ms on a good CPU)")]
        public void TestSpeed()
        {
            int count = 4;
            long[] times = new long[count];
            for (int i = 0; i < count; i++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                Parallel.ForEach(
                    Enumerable.Range(0, 100000), new ParallelOptions() { MaxDegreeOfParallelism = 2 },
                    _ => TagFilter.String.ProcessTags(TagFilter.Filter.Genre | TagFilter.Filter.AnidbInternal | TagFilter.Filter.Programming | TagFilter.Filter.Misc, Input)
                );
                stopwatch.Stop();
                times[i] = stopwatch.ElapsedMilliseconds;
            }

            _console.WriteLine("Average time is {0}ms", times.Average());
            Assert.True(times.Average() < 2000);
        }
    }
}