using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.ServiceModel.Channels;

namespace Shoko.Server.WCFCompression
{
    internal class CompressedMessageEncoderFactory : MessageEncoderFactory
    {
        MessageEncoder encoder;

        public CompressedMessageEncoderFactory(MessageEncoderFactory messageEncoderFactory)
        {
            if (messageEncoderFactory == null)
                throw new ArgumentNullException("messageEncoderFactory",
                    "A valid message encoder factory must be passed to the GZipEncoder");
            encoder = new CompressedMessageEncoder(messageEncoderFactory.Encoder);
        }

        public override MessageEncoder Encoder => encoder;

        public override MessageVersion MessageVersion => encoder.MessageVersion;

        class CompressedMessageEncoder : MessageEncoder
        {
            MessageEncoder innerEncoder;

            internal CompressedMessageEncoder(MessageEncoder messageEncoder)
                : base()
            {
                if (messageEncoder == null)
                    throw new ArgumentNullException("messageEncoder",
                        "A valid message encoder must be passed to the GZipEncoder");
                innerEncoder = messageEncoder;
            }

            public override string ContentType => innerEncoder.ContentType;

            public override string MediaType => innerEncoder.MediaType;

            public override bool IsContentTypeSupported(string contentType)
            {
                return true;
            }

            public override T GetProperty<T>()
            {
                return innerEncoder.GetProperty<T>();
            }

            public override MessageVersion MessageVersion => innerEncoder.MessageVersion;

            static ArraySegment<byte> CompressBuffer(ArraySegment<byte> buffer, BufferManager bufferManager,
                int messageOffset,
                CompressionType ctype)
            {
                MemoryStream memoryStream = new MemoryStream();

                using (
                    Stream stream = ctype == CompressionType.Gzip
                        ? (Stream) new GZipStream(memoryStream, CompressionLevel.Optimal, true)
                        : (Stream) new DeflateStream(memoryStream, CompressionMode.Compress, true))
                {
                    stream.Write(buffer.Array, buffer.Offset, buffer.Count);
                }
                byte[] compressedBytes = memoryStream.ToArray();
                byte[] bufferedBytes = bufferManager.TakeBuffer(compressedBytes.Length + messageOffset);
                Array.Copy(compressedBytes, 0, bufferedBytes, messageOffset, compressedBytes.Length);
                bufferManager.ReturnBuffer(buffer.Array);
                ArraySegment<byte> byteArray = new ArraySegment<byte>(bufferedBytes, messageOffset,
                    compressedBytes.Length);
                return byteArray;
            }


            //Helper method to decompress an array of bytes
            static ArraySegment<byte> DecompressBuffer(ArraySegment<byte> buffer, BufferManager bufferManager,
                CompressionType ctype)
            {
                MemoryStream memoryStream = new MemoryStream(buffer.Array, buffer.Offset, buffer.Count);
                MemoryStream decompressedStream = new MemoryStream();
                int totalRead = 0;
                int blockSize = 1024;
                byte[] tempBuffer = bufferManager.TakeBuffer(blockSize);
                using (
                    Stream gzStream = ctype == CompressionType.Gzip
                        ? (Stream) new GZipStream(memoryStream, CompressionMode.Decompress)
                        : (Stream) new DeflateStream(memoryStream, CompressionMode.Decompress))
                {
                    while (true)
                    {
                        int bytesRead = gzStream.Read(tempBuffer, 0, blockSize);
                        if (bytesRead == 0)
                            break;
                        decompressedStream.Write(tempBuffer, 0, bytesRead);
                        totalRead += bytesRead;
                    }
                }
                bufferManager.ReturnBuffer(tempBuffer);

                byte[] decompressedBytes = decompressedStream.ToArray();
                byte[] bufferManagerBuffer = bufferManager.TakeBuffer(decompressedBytes.Length + buffer.Offset);
                Array.Copy(buffer.Array, 0, bufferManagerBuffer, 0, buffer.Offset);
                Array.Copy(decompressedBytes, 0, bufferManagerBuffer, buffer.Offset, decompressedBytes.Length);

                ArraySegment<byte> byteArray = new ArraySegment<byte>(bufferManagerBuffer, buffer.Offset,
                    decompressedBytes.Length);
                bufferManager.ReturnBuffer(buffer.Array);

                return byteArray;
            }

            //One of the two main entry points into the encoder. Called by WCF to decode a buffered byte array into a Message.
            public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager,
                string contentType)
            {
                ArraySegment<byte> decompressedBuffer = buffer;

                if (buffer.Count >= 3 && buffer.Array[buffer.Offset] == 0x1F &&
                    buffer.Array[buffer.Offset + 1] == 0x8B && buffer.Array[buffer.Offset + 2] == 0x08)
                {
                    //Decompress the buffer
                    decompressedBuffer = DecompressBuffer(buffer, bufferManager, CompressionType.Gzip);
                }

                //Use the inner encoder to decode the decompressed buffer
                Message returnMessage = innerEncoder.ReadMessage(decompressedBuffer, bufferManager, contentType);
                returnMessage.Properties.Encoder = this;
                return returnMessage;
            }

            //One of the two main entry points into the encoder. Called by WCF to encode a Message into a buffered byte array.
            public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize,
                BufferManager bufferManager,
                int messageOffset)
            {
                //Use the inner encoder to encode a Message into a buffered byte array
                ArraySegment<byte> buffer = innerEncoder.WriteMessage(message, maxMessageSize, bufferManager, 0);

                object respObj;
                if (message.Properties.TryGetValue(HttpResponseMessageProperty.Name, out respObj))
                {
                    var resp = (HttpResponseMessageProperty) respObj;
                    if (resp.Headers[HttpResponseHeader.ContentEncoding] == "gzip")
                    {
                        // Need to compress the message
                        buffer = CompressBuffer(buffer, bufferManager, messageOffset, CompressionType.Gzip);
                    }
                }
                return buffer;
            }

            public override Message ReadMessage(Stream stream, int maxSizeOfHeaders, string contentType)
            {
                throw new NotSupportedException();
            }

            public override void WriteMessage(Message message, Stream stream)
            {
                throw new NotSupportedException();
            }
        }
    }
}