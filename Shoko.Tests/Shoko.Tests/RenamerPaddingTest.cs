using System.Collections.Generic;
using Shoko.Plugin.Abstractions;
using Xunit;

namespace Shoko.Tests
{
    public class RenamerPaddingTest
    {
        public static IEnumerable<object[]> Data => new List<object[]> 
        {
            new object[] {1, 10, "01"},
            new object[] {1, 100, "001"},
            new object[] {1, 1000, "0001"},
            new object[] {1, 10000, "00001"},
            new object[] {100, 100, "100"},
            new object[] {50, 100, "050"},
        };

        [Theory, MemberData(nameof(Data))]
        public void PaddingTest(int value, int maxCount, string expected)
        {
            Assert.Equal(expected, value.PadZeroes(maxCount));
        }
    }
}
