using System;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Server.Utilities;
using Xunit;

namespace Shoko.Tests.Utilities;

public class StringPoolTests
{
    #region Pooling

    [Fact]
    public void EqualValuesShareOneInstance()
    {
        var first = new string("image/jpeg".AsSpan());
        var second = new string("image/jpeg".AsSpan());

        var pooledFirst = StringPool.Get(first);
        var pooledSecond = StringPool.Get(second);

        Assert.Equal("image/jpeg", pooledSecond);
        Assert.Same(pooledFirst, pooledSecond);
    }

    [Fact]
    public void NullAndEmptyStayAsTheyAre()
    {
        Assert.Null(StringPool.Get(null));
        Assert.Same(string.Empty, StringPool.Get(new string([])));
    }

    [Fact]
    public void LongValuesAreNotPooled()
    {
        var value = new string('x', StringPool.MaxLength + 1);

        Assert.Same(value, StringPool.Get(value));
        Assert.Same(value, StringPool.Get(value));
        Assert.NotSame(StringPool.Get(new string(value.AsSpan())), value);
    }

    [Fact]
    public void CharactersShareTheInstanceOfAnEqualString()
    {
        var pooled = StringPool.Get(new string("V_MPEG4/ISO/AVC".AsSpan()));

        Assert.Same(pooled, StringPool.Get("V_MPEG4/ISO/AVC".AsSpan()));
        Assert.Same(pooled, StringPool.Get(new string("V_MPEG4/ISO/AVC".AsSpan())));
    }

    [Fact]
    public void CharactersAreCopiedWhenEmptyOrLong()
    {
        var value = new string('x', StringPool.MaxLength + 1);

        Assert.Same(string.Empty, StringPool.Get(ReadOnlySpan<char>.Empty));
        Assert.Equal(value, StringPool.Get(value.AsSpan()));
        Assert.NotSame(StringPool.Get(value.AsSpan()), StringPool.Get(value.AsSpan()));
    }

    [Fact]
    public void ConcurrentCallersAlwaysGetAnEqualValue()
    {
        var values = Enumerable.Range(0, 400_000).Select(i => (i % 5_000).ToString()).ToArray();

        Parallel.For(0, values.Length, i => Assert.Equal(values[i], StringPool.Get(new string(values[i].AsSpan()))));
    }

    #endregion
}
