using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MessagePack;
using Newtonsoft.Json;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.MediaInfo;
using Shoko.Server.Utilities;
using Xunit;

namespace Shoko.Tests.Databases;

public class PooledStringResolverTests
{
    #region Media Info

    [Fact]
    public void MediaInfoReadTwiceSharesItsStrings()
    {
        var converter = new MessagePackConverter<MediaContainer>();
        var bytes = (byte[])converter.ConvertTo(null, CultureInfo.InvariantCulture, Sample(), typeof(byte[]))!;

        var first = (MediaContainer)converter.ConvertFrom(null, CultureInfo.InvariantCulture, bytes)!;
        var second = (MediaContainer)converter.ConvertFrom(null, CultureInfo.InvariantCulture, bytes)!;

        Assert.Equal(JsonConvert.SerializeObject(Sample()), JsonConvert.SerializeObject(second));
        Assert.Same(first.VideoStream!.CodecID, second.VideoStream!.CodecID);
        Assert.Same(first.AudioStreams[0].LanguageName, second.AudioStreams[0].LanguageName);
        Assert.Same(first.TextStreams[0].Title, second.TextStreams[0].Title);
        Assert.Same(first.MenuStreams[0].extra!.Keys.First(), second.MenuStreams[0].extra!.Keys.First());
        Assert.Same(first.MenuStreams[0].extra!.Values.First(), second.MenuStreams[0].extra!.Values.First());
    }

    [Fact]
    public void MediaInfoWritesTheSameBytesAfterBeingRead()
    {
        var converter = new MessagePackConverter<MediaContainer>();
        var bytes = (byte[])converter.ConvertTo(null, CultureInfo.InvariantCulture, Sample(), typeof(byte[]))!;

        var read = (MediaContainer)converter.ConvertFrom(null, CultureInfo.InvariantCulture, bytes)!;

        Assert.Equal(bytes, converter.ConvertTo(null, CultureInfo.InvariantCulture, read, typeof(byte[])));
        Assert.Equal(bytes, MessagePackSerializer.Serialize(read, MessagePackSerializer.DefaultOptions.WithResolver(PooledStringResolver.Instance), TestContext.Current.CancellationToken));
    }

    #endregion

    #region Strings

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("S_TEXT/ASS")]
    [InlineData("Français, 日本語 🎌")]
    [InlineData("\n            ")]
    [InlineData("a value longer than the pool holds, which is read as it is without being pooled at all")]
    public void StringsReadAsTheStandardFormatterReadsThem(string? value)
    {
        var bytes = MessagePackSerializer.Serialize(value, cancellationToken: TestContext.Current.CancellationToken);
        var formatter = PooledStringResolver.Instance.GetFormatter<string>()!;

        var whole = new MessagePackReader(bytes);
        var split = new MessagePackReader(Split(bytes));

        Assert.Equal(value, formatter.Deserialize(ref whole, MessagePackSerializerOptions.Standard));
        Assert.Equal(value, formatter.Deserialize(ref split, MessagePackSerializerOptions.Standard));
        Assert.Equal(bytes.Length, whole.Consumed);
        Assert.Equal(bytes.Length, split.Consumed);
    }

    [Fact]
    public void ShortStringsReadAreThePooledInstances()
    {
        var bytes = MessagePackSerializer.Serialize("High 10", cancellationToken: TestContext.Current.CancellationToken);
        var formatter = PooledStringResolver.Instance.GetFormatter<string>()!;

        var whole = new MessagePackReader(bytes);
        var split = new MessagePackReader(Split(bytes));

        Assert.Same(StringPool.Get(new string("High 10".AsSpan())), formatter.Deserialize(ref whole, MessagePackSerializerOptions.Standard));
        Assert.Same(StringPool.Get(new string("High 10".AsSpan())), formatter.Deserialize(ref split, MessagePackSerializerOptions.Standard));
    }

    [Fact]
    public void OtherTypesResolveAsTheStandardResolverDoes()
        => Assert.Same(
            MessagePackSerializerOptions.Standard.Resolver.GetFormatter<MediaContainer>(),
            PooledStringResolver.Instance.GetFormatter<MediaContainer>()
        );

    #endregion

    #region Helpers

    private static MediaContainer Sample()
        => new()
        {
            media = new()
            {
                track =
                [
                    new GeneralStream { Format = "Matroska", FileExtension = "mkv", Duration = 1421.067, UniqueID = "233047645306169990888854847128758452642" },
                    new VideoStream { Format = "AVC", CodecID = "V_MPEG4/ISO/AVC", Format_Profile = "High 10", ScanType = "Progressive", colour_primaries = "BT.709" },
                    new AudioStream { Format = "FLAC", CodecID = "A_FLAC", Language = "ja", LanguageCode = "jpn", LanguageName = "Japanese", ChannelLayout = "L R" },
                    new TextStream { Format = "ASS", CodecID = "S_TEXT/ASS", Title = "English subs", Language = "en", LanguageName = "English" },
                    new MenuStream { extra = new Dictionary<string, string> { ["_00_00_00_000"] = "en:Prologue", ["_00_02_29_024"] = "en:Opening" } },
                ],
            },
        };

    private static ReadOnlySequence<byte> Split(byte[] bytes)
    {
        var first = new Segment(bytes.AsMemory(0, 1));
        var last = first;
        for (var index = 1; index < bytes.Length; index++)
            last = last.Append(bytes.AsMemory(index, 1));
        return new(first, 0, last, last.Memory.Length);
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory)
            => Memory = memory;

        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }

    #endregion
}
