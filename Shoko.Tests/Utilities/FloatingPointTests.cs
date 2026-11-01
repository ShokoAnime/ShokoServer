using Shoko.Server.Utilities;
using Xunit;

namespace Shoko.Tests.Utilities;

public class FloatingPointTests
{
    [Fact]
    public void ExactAndNearValuesAreEqual()
    {
        Assert.True(FloatingPoint.AreEqual(1.0, 1.0));
        Assert.True(FloatingPoint.AreEqual(0.1 + 0.2, 0.3));
    }

    [Fact]
    public void DistantValuesAreNotEqual()
        => Assert.False(FloatingPoint.AreEqual(1.0, 1.001));

    [Fact]
    public void SpecialValuesCompareLikeEquals()
    {
        Assert.True(FloatingPoint.AreEqual(double.NaN, double.NaN));
        Assert.True(FloatingPoint.AreEqual(double.PositiveInfinity, double.PositiveInfinity));
        Assert.False(FloatingPoint.AreEqual(double.PositiveInfinity, double.NegativeInfinity));
    }

    [Fact]
    public void NullableValuesCompareNullsAsEqualToEachOther()
    {
        Assert.True(FloatingPoint.AreEqual((double?)null, null));
        Assert.False(FloatingPoint.AreEqual(1.0, (double?)null));
        Assert.True(FloatingPoint.AreEqual((double?)5.0, 5.0));
    }
}
