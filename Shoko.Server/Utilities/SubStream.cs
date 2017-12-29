using System;
using System.IO;

namespace Shoko.Server.Utilities
{
    internal class SubStream : Stream
    {
        private long _bytesLeft;
        private Stream _stream;
        private long _pos;
        private long _original;
        public long CrossPosition { get; set; }

#if DEBUG_STREAM
        private Stream _fstream;
        private StreamWriter _writer;
#endif

        public delegate void CrossPositionHandler(long position);

        public event CrossPositionHandler CrossPositionCrossed;


        public override void Close()
        {
#if DEBUG_STREAM
        _writer.Close();
        _fstream.Close();
#endif
            _stream.Close();
        }

        public SubStream(Stream stream, long offset, long bytesToRead)
        {
#if DEBUG_STREAM
            _fstream = File.OpenWrite(@"C:\Users\mpiva\Desktop\" + offset.ToString("X8") + ".txt");
            _writer = new StreamWriter(_fstream);
#endif
            _stream = stream;
            _pos = 0;
            _stream.Seek(offset, SeekOrigin.Begin);
            _bytesLeft = bytesToRead;
            _original = bytesToRead;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream.Dispose();
            }
        }


        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Length => _original;

        public override long Position
        {
            get { return _pos; }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count > _bytesLeft)
            {
                count = (int) _bytesLeft;
            }

            long oldpos = _stream.Position;
#if DEBUG_STREAM
            _writer.WriteLine("POSITION: " + oldpos.ToString("X8") + " OFFSET: " + offset.ToString("X8") + " NeedCount: " + count.ToString("X8"));
            _writer.Flush();
#endif
            int read = _stream.Read(buffer, offset, count);
#if DEBUG_STREAM
            string hash;
            using (MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider())
            {
                StringBuilder sb = new StringBuilder();
                byte[] r = md5.ComputeHash(buffer,offset,read);
                for (int i = 0; i < r.Length; i++)
                {
                    sb.Append(r[i].ToString("X2"));
                }
                hash = sb.ToString();
            }
            _writer.WriteLine("ReadCount: " + read.ToString("X8")+" Hash: "+hash);
            _writer.Flush();

#endif
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
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}