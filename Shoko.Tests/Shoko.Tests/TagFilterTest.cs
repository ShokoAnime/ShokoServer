using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Server;
using Xunit;
using Xunit.Abstractions;

namespace Shoko.Tests
{
    public class TagFilterTest
    {
        private readonly ITestOutputHelper _console;

        public TagFilterTest(ITestOutputHelper console)
        {
            _console = console;
        }
        
        public static string[] Input => new[] { "comedy", "Comedy", "horror", "18 restricted", "large breasts", "japan", "violence",
            "original work", "manga", "fantasy", "shounen", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins", "alternative past", "past"};
        public static string[] InputNoSource => new[] { "horror", "Horror", "18 restricted", "large breasts", "japan", "violence",
            "fantasy", "shounen", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins"};
        public static IEnumerable<object[]> Data => new[]
        {
            new object[] {TagFilter.Filter.Genre, Input, new[] { "large breasts", "japan", "original work", "manga", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins", "alternative past" }.ToList() },
            new object[] {TagFilter.Filter.Genre, Input.Concat(new[] {"new"}).ToArray(), new[] { "large breasts", "japan", "original work", "manga", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins", "alternative past" }.ToList() },
            new object[] {TagFilter.Filter.Genre, InputNoSource, new[] { "large breasts", "japan", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins", "original work" }.ToList() },
            new object[] {TagFilter.Filter.Source, Input, new[] { "comedy", "horror", "18 restricted", "large breasts", "japan", "violence", "fantasy", "shounen", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins", "alternative past" }.ToList() },
            new object[] {TagFilter.Filter.Source, InputNoSource, new[] { "horror", "18 restricted", "large breasts", "japan", "violence", "fantasy", "shounen", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins" }.ToList() },
            new object[] {TagFilter.Filter.Programming | TagFilter.Filter.Genre, Array.Empty<string>(), new[] {"original work"}.ToList()},
            new object[] {TagFilter.Filter.Genre, new[] {"new"}, new[] {"original work"}.ToList()},
            new object[] {TagFilter.Filter.Source, new[] {"new"}, new List<string>()},
            new object[] {TagFilter.Filter.ArtStyle | TagFilter.Filter.Source, new[] {"new", "censored", "cgi"}, new List<string>()},
            new object[] {TagFilter.Filter.Plot | TagFilter.Filter.Source, new[] {"everybody dies"}, new List<string>()},
            new object[] {TagFilter.Filter.Setting | TagFilter.Filter.Source, new[] {"meiji period"}, new List<string>()},
            new object[] {TagFilter.Filter.Setting | TagFilter.Filter.Source, new[] {"meiji era"}, new List<string>()},
            new object[] {TagFilter.Filter.Source, new[] {"original work"}, new List<string>()},
            new object[] {TagFilter.Filter.Setting | TagFilter.Filter.Source, new[] {"japan", "high school"}, new List<string>()},
            new object[] {TagFilter.Filter.Setting | TagFilter.Filter.Source, new[] {"meiji period"}, new List<string>()},
            new object[] {TagFilter.Filter.Plot | TagFilter.Filter.Source, new[] {"first girl wins"}, new List<string>()},
            new object[] {TagFilter.Filter.Misc | TagFilter.Filter.Source, new[] {"previews suck"}, new List<string>()},
            new object[] {TagFilter.Filter.AnidbInternal | TagFilter.Filter.Source, new[] {"predominantly gay", "adapted into live action", "weekly monster", "needs removed", "to be merged", "to be improved", "to be split and deleted", "to be moved", "to be split"}, new List<string>()},
            new object[] {TagFilter.Filter.AnidbInternal | TagFilter.Filter.Source, new[] {"old animetags", "missing frogs"}, new List<string>()},
            new object[] {TagFilter.Filter.Invert | TagFilter.Filter.Source | TagFilter.Filter.Genre | TagFilter.Filter.Setting, Input, new[] { "comedy", "horror", "18 restricted", "japan", "violence", "manga", "fantasy", "shounen", "alternative past" }.ToList()},
            new object[] {TagFilter.Filter.AnidbInternal, Array.Empty<string>(), new[] {"original work"}.ToList()},
            new object[] {TagFilter.Filter.AnidbInternal, new[] {"new"}, new[] {"original work"}.ToList()},
            new object[] {TagFilter.Filter.AnidbInternal | TagFilter.Filter.Source, new[] {"original work", "new"}, new List<string>()},
            new object[] {TagFilter.Filter.AnidbInternal, new[] {"action", "manga", "original work"}, new[] {"action", "manga"}.ToList()},
            new object[] {TagFilter.Filter.Source | TagFilter.Filter.Invert, new[] {"manga", "original work"}, new[] {"manga"}.ToList()},
            new object[] {TagFilter.Filter.Source | TagFilter.Filter.Invert, new string[] {"action"}, new[] {"original work"}.ToList()},
            new object[] {TagFilter.Filter.Genre | TagFilter.Filter.Invert, new string[] {"action"}, new[] {"action"}.ToList()},
            new object[] {TagFilter.Filter.AnidbInternal, new[] {"original work"}, new[] {"original work"}.ToList()},
            new object[] {TagFilter.Filter.AnidbInternal, new[] {"new", "original work"}, new[] {"original work"}.ToList()},
        };

        public static IEnumerable<object[]> InputData => new[] { new object[] { Input } };

        [Theory, MemberData(nameof(Data))]
        public void Test(TagFilter.Filter filter, string[] input, List<string> expected)
        {
            var actual = TagFilter.String.ProcessTags(filter, input);
            Assert.Equal(expected, actual);
        }
        
        [Theory, MemberData(nameof(InputData))]
        public void TestSpeed(string[] input)
        {
            int count = 4;
            long[] times = new long[count];
            for (int i = 0; i < count; i++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                Parallel.ForEach(
                    Enumerable.Range(0, 100000), new ParallelOptions() { MaxDegreeOfParallelism = 2 },
                    _ => TagFilter.String.ProcessTags(TagFilter.Filter.Genre | TagFilter.Filter.AnidbInternal | TagFilter.Filter.Programming | TagFilter.Filter.Misc, input)
                );
                stopwatch.Stop();
                times[i] = stopwatch.ElapsedMilliseconds;
            }

            _console.WriteLine("Average time is {0}ms", times.Average());
            Assert.True(times.Average() < 2000);
        }
    }
}
