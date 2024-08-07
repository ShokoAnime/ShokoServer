using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

//AVDump OggFile parser, tweaked for our needs.

namespace Shoko.Server.Services.Ogg;

public class OggFile
{
    private readonly Dictionary<uint, OGGBitStream> _bitStreams = [];

    public long FileSize { get; private set; }

    public long Overhead { get; private set; }

    public IEnumerable<OGGBitStream> BitStreams => _bitStreams.Values;

    public double Duration => BitStreams.OfType<IDuration>().Max(a => a.Duration.TotalSeconds);

    public static OggFile ParseFile(string filename)
    {
        var parser = new OggParser(new Reader(File.OpenRead(filename)));
        parser.Process();
        return parser.Info;
    }

    public void ProcessOggPage(ref OggPage page)
    {
        Overhead += 27 + page.SegmentCount;

        if (_bitStreams.TryGetValue(page.StreamId, out var bitStream))
        {
            bitStream.ProcessPage(ref page);

        }
        else if (page.Flags.HasFlag(PageFlags.Header))
        {
            bitStream = OGGBitStream.ProcessBeginPage(ref page);
            _bitStreams.Add(bitStream.Id, bitStream);

        }
        else
        {
            Overhead += page.Data.Length;
        }
    }

    public class Reader
    {
        private readonly BufferedStream _stream;

        public static int SuggestedReadLength => 1 << 20;

        public Reader(Stream s)
        {
            _stream = new BufferedStream(s);
        }

        public ReadOnlySpan<byte> GetBlock(int length)
        {
            var pos = _stream.Position;
            var buffer = new Span<byte>(new byte[length]);
            var cnt = _stream.Read(buffer);
            _stream.Position = pos;
            return buffer[..cnt];
        }

        public bool Advance(int length)
        {
            _stream.Position += length;
            if (_stream.Position >= _stream.Length)
                return false;
            return true;
        }
    }

    public class OggParser
    {
        private readonly Reader _reader;

        public OggFile Info { get; private set; }

        public OggParser(Reader reader)
        {
            _reader = reader;
        }

        public void Process()
        {
            var info = new OggFile();

            var page = new OggPage();
            var stream = new OggBlockDataSource(_reader);

            if (!stream.SeekPastSyncBytes(false, 0)) return;

            while (stream.ReadOggPage(ref page))
            {
                info.ProcessOggPage(ref page);
                if (info.BitStreams.FirstOrDefault(a => a is VideoOGGBitStream) is VideoOGGBitStream bs && bs.Duration.TotalMilliseconds > 0)
                {
                    break;
                }
            }

            Info = info;
        }
    }

    public enum PageFlags
    {
        None = 0,
        SpanBefore = 1,
        Header = 2,
        Footer = 4,
        SpanAfter = 1 << 31,
    }

    public ref struct OggPage
    {
        //public long FilePosition;
        //public long DataPosition;
        public PageFlags Flags { get; set; }

        public byte Version { get; set; }

        public ReadOnlySpan<byte> GranulePosition { get; set; }

        public uint StreamId { get; set; }

        public uint PageIndex { get; set; }

        public ReadOnlySpan<byte> Checksum { get; set; }

        public byte SegmentCount { get; set; }

        public ReadOnlySpan<int> PacketOffsets { get; set; }

        public ReadOnlySpan<byte> Data { get; set; }
    }

    public class OggBlockDataSource
    {
        private static readonly ReadOnlyMemory<byte> _headerSignature = new("OggS"u8.ToArray());

        private readonly Reader _reader;

        public OggBlockDataSource(Reader reader)
        {
            _reader = reader;
        }


        public bool SeekPastSyncBytes(bool advanceReader, int maxBytesToSkip = 1 << 20)
        {
            var bytesSkipped = 0;
            var magicBytes = _headerSignature.Span;
            while (true)
            {
                var block = _reader.GetBlock(Reader.SuggestedReadLength);
                var offset = block.IndexOf(magicBytes);
                if (offset != -1 && offset <= maxBytesToSkip)
                {
                    if (advanceReader) _reader.Advance(offset + 4);
                    return true;
                }
                bytesSkipped += offset;

                if (bytesSkipped > maxBytesToSkip || block.Length < 4 || !_reader.Advance(block.Length - 3)) break;
            }
            return false;
        }

        public bool ReadOggPage(ref OggPage page)
        {
            if (!SeekPastSyncBytes(true)) return false;

            var block = _reader.GetBlock(23 + 256 * 256);

            //page.FilePosition = Position;
            page.Version = block[0];
            page.Flags = (PageFlags)block[1];
            page.GranulePosition = block.Slice(2, 8);
            page.StreamId = MemoryMarshal.Read<uint>(block[10..]);
            page.PageIndex = MemoryMarshal.Read<uint>(block[14..]);
            page.Checksum = block.Slice(18, 4);

            var segmentCount = page.SegmentCount = block[22];

            var offset = 0;
            var dataLength = 0;
            var packetOffsets = new List<int>();
            while (segmentCount != 0)
            {
                dataLength += block[23 + offset];

                if (block[23 + offset] != 255) packetOffsets.Add(dataLength);

                offset++;
                segmentCount--;
            }
            page.PacketOffsets = packetOffsets.ToArray();
            if (block[23 + offset - 1] == 255) page.Flags |= PageFlags.SpanAfter;

            //reader.BytesRead + 23 + offset;
            page.Data = block.Slice(23 + offset, Math.Min(dataLength, block.Length - 23 - offset));

            return true;
        }
    }

    public class UnknownOGGBitStream : OGGBitStream
    {
        public override string CodecName => "Unknown";

        public override string CodecVersion { get; protected set; }

        public UnknownOGGBitStream() : base(false) { }
    }

    public abstract class OGGBitStream
    {
        public uint Id { get; private set; }

        public long Size { get; private set; }

        public long LastGranulePosition { get; private set; }

        public abstract string CodecName { get; }

        public abstract string CodecVersion { get; protected set; }

        public bool IsOfficiallySupported { get; private set; }

        public OGGBitStream(bool isOfficiallySupported) { IsOfficiallySupported = isOfficiallySupported; }

        public static OGGBitStream ProcessBeginPage(ref OggPage page)
        {
            OGGBitStream bitStream = null;
            if (page.Data.Length >= 29 && Encoding.ASCII.GetString(page.Data.Slice(1, 5)).Equals("video"))
            {
                bitStream = new OGMVideoOGGBitStream(page.Data);
            }
            else if (page.Data.Length >= 46 && Encoding.ASCII.GetString(page.Data.Slice(1, 5)).Equals("audio"))
            {
                bitStream = new OGMAudioOGGBitStream(page.Data);
            }
            else if (page.Data.Length >= 0x39 && Encoding.ASCII.GetString(page.Data.Slice(1, 4)).Equals("text"))
            {
                bitStream = new OGMTextOGGBitStream(page.Data);
            }
            else if (page.Data.Length >= 42 && Encoding.ASCII.GetString(page.Data.Slice(1, 6)).Equals("theora"))
            {
                bitStream = new TheoraOGGBitStream(page.Data);
            }
            else if (page.Data.Length >= 30 && Encoding.ASCII.GetString(page.Data.Slice(1, 6)).Equals("vorbis"))
            {
                bitStream = new VorbisOGGBitStream(page.Data);
            }
            else if (page.Data.Length >= 79 && Encoding.ASCII.GetString(page.Data.Slice(1, 4)).Equals("FLAC"))
            {
                bitStream = new FlacOGGBitStream(page.Data);
            }

            bitStream ??= new UnknownOGGBitStream();
            bitStream.Id = page.StreamId;

            return bitStream;
        }

        public virtual void ProcessPage(ref OggPage page)
        {
            var granulePosition = MemoryMarshal.Read<long>(page.GranulePosition);
            LastGranulePosition = granulePosition > LastGranulePosition && granulePosition < LastGranulePosition + 10000000 ? granulePosition : LastGranulePosition;

            Size += page.Data.Length;
        }
    }

    public abstract class VideoOGGBitStream : OGGBitStream, IDuration
    {
        public abstract long FrameCount { get; }

        public abstract double FrameRate { get; }

        public int Width { get; protected set; }

        public int Height { get; protected set; }

        public virtual TimeSpan Duration => TimeSpan.FromSeconds(FrameCount / FrameRate);

        public VideoOGGBitStream(bool isOfficiallySupported) : base(isOfficiallySupported) { }
    }

    public class TheoraOGGBitStream : VideoOGGBitStream
    {
        public override string CodecName => "Theora";
        public override string CodecVersion { get; protected set; }
        public override long FrameCount => LastGranulePosition;
        public override double FrameRate { get; }

        public TheoraOGGBitStream(ReadOnlySpan<byte> header) : base(true)
        {
            var offset = 0;
            CodecVersion = header[offset + 7] + "." + header[offset + 8] + "." + header[offset + 9];
            Width = header[offset + 14] << 16 | header[offset + 15] << 8 | header[offset + 16];
            Height = header[offset + 17] << 16 | header[offset + 18] << 8 | header[offset + 19];
            FrameRate = (header[offset + 22] << 24 | header[offset + 23] << 16 | header[offset + 24] << 8 | header[offset + 25]) / (double)(header[offset + 26] << 24 | header[offset + 27] << 16 | header[offset + 28] << 8 | header[offset + 29]);
        }
    }

    public class OGMVideoOGGBitStream : VideoOGGBitStream
    {
        public override string CodecName => "OGMVideo";

        public override string CodecVersion { get; protected set; }

        public override long FrameCount => LastGranulePosition;

        public override double FrameRate { get; }

        public string ActualCodecName { get; private set; }

        public OGMVideoOGGBitStream(ReadOnlySpan<byte> header)
            : base(false)
        {
            var codecInfo = MemoryMarshal.Read<OGMVideoHeader>(header.Slice(1, 0x38));
            ActualCodecName = Encoding.ASCII.GetString(BitConverter.GetBytes(codecInfo.SubType));
            FrameRate = 10000000d / codecInfo.TimeUnit;
            Width = codecInfo.Width;
            Height = codecInfo.Height;

        }

        [StructLayout(LayoutKind.Sequential, Size = 52)]
        public struct OGMVideoHeader
        {
            public long StreamType { get; set; }

            public int SubType { get; set; }

            public int Size { get; set; }

            public long TimeUnit { get; set; }

            public long SamplesPerUnit { get; set; }

            public int DefaultLength { get; set; }

            public int BufferSize { get; set; }

            public short BitsPerSample { get; set; }

            public int Width { get; set; }

            public int Height { get; set; }
        }
    }

    public abstract class SubtitleOGGBitStream : OGGBitStream
    {
        public SubtitleOGGBitStream(bool isOfficiallySupported) : base(isOfficiallySupported) { }
    }

    public class OGMTextOGGBitStream : SubtitleOGGBitStream
    {
        public override string CodecName => "OGMText";

        public override string CodecVersion { get; protected set; }

        public string ActualCodecName { get; private set; }

        public OGMTextOGGBitStream(ReadOnlySpan<byte> header)
            : base(false)
        {
            var codecInfo = MemoryMarshal.Read<OGMTextHeader>(header.Slice(1, 0x38));
            ActualCodecName = Encoding.ASCII.GetString(BitConverter.GetBytes(codecInfo.SubType));
        }

        [StructLayout(LayoutKind.Sequential, Size = 52)]
        private struct OGMTextHeader
        {
            public long StreamType { get; set; }

            public int SubType { get; set; }

            public int Size { get; set; }

            public long TimeUnit { get; set; }

            public long SamplesPerUnit { get; set; }

            public int DefaultLength { get; set; }

            public int BufferSize { get; set; }

            public short BitsPerSample { get; set; }

            public long Unused { get; set; }
        }
    }

    public sealed class VorbisOGGBitStream : AudioOGGBitStream
    {
        public override string CodecName => "Vorbis";

        public override string CodecVersion { get; protected set; }

        public override long SampleCount => LastGranulePosition;

        public override double SampleRate { get; }

        public VorbisOGGBitStream(ReadOnlySpan<byte> header)
            : base(true)
        {
            var codecInfo = MemoryMarshal.Read<VorbisIdentHeader>(header.Slice(7, 23));
            ChannelCount = codecInfo.ChannelCount;
            SampleRate = codecInfo.SampleRate;
            CodecVersion = codecInfo.Version.ToString();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct VorbisIdentHeader
        {
            public uint Version { get; set; }

            public byte ChannelCount { get; set; }

            public uint SampleRate { get; set; }

            public int MaxBitrate { get; set; }

            public int NomBitrate { get; set; }

            public int MinBitrate { get; set; }

            public byte BlockSizes { get; set; }

            public bool Framing { get; set; }
        }
    }

    public class OGMAudioOGGBitStream : AudioOGGBitStream
    {
        public override string CodecName => "OGMAudio";

        public override string CodecVersion { get; protected set; }

        public string ActualCodecName { get; private set; }

        public override long SampleCount => LastGranulePosition;

        public override double SampleRate { get; }

        public OGMAudioOGGBitStream(ReadOnlySpan<byte> header)
            : base(false)
        {
            var codecInfo = MemoryMarshal.Read<OGMAudioHeader>(header.Slice(1, 56));
            ChannelCount = codecInfo.ChannelCount;
            SampleRate = codecInfo.SamplesPerUnit;
            ActualCodecName = Encoding.ASCII.GetString(BitConverter.GetBytes(codecInfo.SubType));
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OGMAudioHeader
        {
            public long StreamType { get; set; }

            public int SubType { get; set; }

            public int Size { get; set; }

            public long TimeUnit { get; set; }

            public long SamplesPerUnit { get; set; }

            public int DefaultLength { get; set; }

            public int BufferSize { get; set; }

            public short BitsPerSample { get; set; }

            public short Unknown { get; set; }

            public short ChannelCount { get; set; }

            public short BlockAlign { get; set; }

            public int ByteRate { get; set; }
        }
    }

    public class FlacOGGBitStream : AudioOGGBitStream
    {
        public override string CodecName => "Flac";

        public override string CodecVersion { get; protected set; }

        public override long SampleCount => LastGranulePosition;

        public override double SampleRate { get; }

        public FlacOGGBitStream(ReadOnlySpan<byte> header)
            : base(true)
        {
            //TODO: check offsets
            SampleRate = header[33] << 12 | header[34] << 4 | (header[35] & 0xF0) >> 4;
            ChannelCount = ((header[35] & 0x0E) >> 1) + 1;
        }
    }

    public interface IDuration
    {
        TimeSpan Duration { get; }
    }

    public abstract class AudioOGGBitStream : OGGBitStream, IDuration
    {
        public abstract long SampleCount { get; }

        public abstract double SampleRate { get; }

        public virtual TimeSpan Duration => TimeSpan.FromSeconds(SampleCount / SampleRate);

        public int ChannelCount { get; protected set; }

        public AudioOGGBitStream(bool isOfficiallySupported) : base(isOfficiallySupported) { }
    }
}
