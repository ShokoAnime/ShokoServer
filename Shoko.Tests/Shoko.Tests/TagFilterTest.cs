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
                "comedy", "Comedy", "horror", "18 restricted", "large breasts", "japan", "violence", "source material", "manga", "fantasy", "shounen", "Earth", "Asia", "noitamina", "cgi", "long episodes",
                "first girl wins", "alternative past", "past",
            };

        private static IEnumerable<string> InputNoSource =>
            new[] { "horror", "Horror", "18 restricted", "large breasts", "japan", "violence", "fantasy", "shounen", "Earth", "Asia", "noitamina", "cgi", "long episodes", "first girl wins", };

        [Fact(DisplayName = "Full Test")]
        public void TestFullList()
        {
            var actual = TagFilter.String.ProcessTags(TagFilter.Filter.Genre, Input);
            var expected = new List<string>
            {
                "large breasts",
                "japan",
                "source material",
                "manga",
                "Earth",
                "Asia",
                "noitamina",
                "cgi",
                "long episodes",
                "first girl wins",
                "alternative past",
                "past",
            };
            
            _console.WriteLine("Expected: [{0}]", string.Join(", ", expected));
            _console.WriteLine("Actual: [{0}]", string.Join(", ", actual));
            Assert.Equal(expected, actual);
        }

        [Fact(DisplayName = "Full Test w/ 'original work'")]
        public void TestFullListWithNew()
        {
            var actual = TagFilter.String.ProcessTags(TagFilter.Filter.Genre, Input.Concat(new[] { "original work" }));
            var expected = new List<string>
            {
                "large breasts",
                "japan",
                "source material",
                "manga",
                "Earth",
                "Asia",
                "noitamina",
                "cgi",
                "long episodes",
                "first girl wins",
                "alternative past",
                "past",
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
                "Earth",
                "Asia",
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
                "past",
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
                "source material",
                "fantasy",
                "shounen",
                "Earth",
                "Asia",
                "noitamina",
                "cgi",
                "long episodes",
                "first girl wins",
                "alternative past",
                "past",
            };

            _console.WriteLine("Expected: [{0}]", string.Join(", ", expected));
            _console.WriteLine("Actual: [{0}]", string.Join(", ", actual));
            Assert.Equal(expected, actual);
        }
        
        [Fact(DisplayName = "Source Exclusion with Full List w/o Source")]
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
                "Earth",
                "Asia",
                "noitamina",
                "cgi",
                "long episodes",
                "first girl wins",
            };

            _console.WriteLine("Expected: [{0}]", string.Join(", ", expected));
            _console.WriteLine("Actual: [{0}]", string.Join(", ", actual));
            Assert.Equal(expected, actual);
        }

        [Fact(DisplayName = "Source Exclusion")]
        public void TestSource()
        {
            Assert.Equal(new List<string>(), TagFilter.String.ProcessTags(TagFilter.Filter.Source, new[] { "original work" }));
        }

        [Fact(DisplayName = "Source Exclusion with No Source")]
        public void TestSourceNoSource()
        {
            Assert.Equal(new List<string> { "original work" }, TagFilter.String.ProcessTags(TagFilter.Filter.Genre, new List<string>()));
        }

        [Fact(DisplayName = "Source Inclusion with Source and Original Work")]
        public void TestSourceInvertedDupeSource()
        {
            Assert.Equal(new List<string> { "manga" }, TagFilter.String.ProcessTags(TagFilter.Filter.Source | TagFilter.Filter.Invert, new[] { "manga", "original work" }));
        }

        [Fact(DisplayName = "Source Inclusion w/o Source")]
        public void TestSourceInvertedNoSource()
        {
            Assert.Equal(new List<string> { "original work" }, TagFilter.String.ProcessTags(TagFilter.Filter.Source | TagFilter.Filter.Invert, new[] { "action" }));
        }

        [Fact(DisplayName = "Inverted w/o Source")]
        public void TestInvertedNoSource()
        {
            Assert.Equal(new List<string> { "action" }, TagFilter.String.ProcessTags(TagFilter.Filter.Genre | TagFilter.Filter.Invert, new[] { "action" }));
        }

        [Fact(DisplayName = "AniDB Internal with Replacement Source")]
        public void TestAniDBInternalWithSource()
        {
            Assert.Equal(new List<string> { "original work" }, TagFilter.String.ProcessTags(TagFilter.Filter.AnidbInternal, new[] { "source material", "original work" }));
        }

        [Fact(DisplayName = "AniDB Internal with Source")]
        public void TestAniDBInternalWithTag()
        {
            Assert.Equal(new List<string>(), TagFilter.String.ProcessTags(TagFilter.Filter.AnidbInternal | TagFilter.Filter.Source, new[] { "source material", "original work" }));
        }

        [Fact(DisplayName = "AniDB Internal with Overlapping Source")]
        public void TestAniDBInternalAndSourceWithOverlap()
        {
            Assert.Equal(new List<string> { "action", "manga" }, TagFilter.String.ProcessTags(TagFilter.Filter.AnidbInternal, new[] { "action", "manga", "original work" }));
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
