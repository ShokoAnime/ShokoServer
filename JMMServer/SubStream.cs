using System;
using System.IO;

namespace JMMServer
{
    internal class SubStream : Stream
    {
        public delegate void CrossPositionHandler(long position);

        private long _bytesLeft;
        private long _pos;
        private readonly Stream _stream;

        public SubStream(Stream stream, long offset, long bytesToRead)
        {
            _stream = stream;
            _pos = 0;
            _stream.Seek(offset, SeekOrigin.Begin);
            _bytesLeft = bytesToRead;
            Length = bytesToRead;
        }


        public long CrossPosition { get; set; }


        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length { get; }

        public override long Position
        {
            get { return _pos; }
            set { throw new NotImplementedException(); }
        }

        public event CrossPositionHandler CrossPositionCrossed;


        public override void Close()
        {
            _stream.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream.Dispose();
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count > _bytesLeft)
            {
                count = (int)_bytesLeft;
            }
            var oldpos = _stream.Position;
            var read = _stream.Read(buffer, offset, count);
            _pos += read;
            _bytesLeft -= read;
            if (CrossPositionCrossed != null)
            {
                if ((oldpos <= CrossPosition) && (_stream.Position >= CrossPosition))
                {
                    CrossPositionCrossed(_stream.Position);
                }
            }
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}