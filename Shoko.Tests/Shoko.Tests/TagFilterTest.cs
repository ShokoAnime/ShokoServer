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
            "manga", "fantasy", "shounen", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins", "alternative past", "past"};
        public static string[] InputNoSource => new[] { "horror", "Horror", "18 restricted", "large breasts", "japan", "violence",
            "fantasy", "shounen", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins"};
        public static IEnumerable<object[]> Data => new[]
        {
            new object[] {TagFilter.Filter.Genre, Input, new[] { "large breasts", "japan", "manga", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins", "alternative past" }.ToList() },
            new object[] {TagFilter.Filter.Genre, Input.Concat(new[] {"new"}).ToArray(), new[] { "large breasts", "japan", "manga", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins", "alternative past", "original work" }.ToList() },
            new object[] {TagFilter.Filter.Genre, InputNoSource, new[] { "large breasts", "japan", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins", "original work" }.ToList() },
            new object[] {TagFilter.Filter.Source, Input, new[] { "comedy", "horror", "18 restricted", "large breasts", "japan", "violence", "fantasy", "shounen", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins", "alternative past" }.ToList() },
            new object[] {TagFilter.Filter.Source, InputNoSource, new[] { "horror", "18 restricted", "large breasts", "japan", "violence", "fantasy", "shounen", "earth", "asia", "noitamina", "cgi", "long episodes", "first girl wins" }.ToList() },
            new object[] {TagFilter.Filter.Programming | TagFilter.Filter.Genre, Array.Empty<string>(), new[] {"original work"}.ToList()},
            new object[] {TagFilter.Filter.Genre, new[] {"new"}, new[] {"original work"}.ToList()},
            new object[] {TagFilter.Filter.Source, new[] {"new"}, new List<string>()},
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
