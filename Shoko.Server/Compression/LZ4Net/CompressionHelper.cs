using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
#if UNSAFE
using Shoko.Server.Compression.LZ4pn;

#else

#endif

namespace Shoko.Server.Compression.LZ4
{
    public static class CompressionHelper
    {
        private static bool is64bit;

        static CompressionHelper()
        {
            is64bit = Environment.Is64BitProcess;
        }
        public static byte[] SerializeString(string obj, out int originalsize)
        {
            if (obj == null)
            {
                originalsize = 0;
                return null;
            }
            byte[] data = Encoding.UTF8.GetBytes(obj);
            originalsize = data.Length;
            return Encode(data, 0, data.Length);
        }
        public static byte[] SerializeObject(object obj, out int originalsize, bool multiinheritance = false)
        {
            if (obj == null)
            {
                originalsize = 0;
                return null;
            }
            byte[] data =
                Encoding.UTF8.GetBytes(multiinheritance
                    ? JsonConvert.SerializeObject(obj,
                        new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.All})
                    : JsonConvert.SerializeObject(obj));
            originalsize = data.Length;
            return Encode(data, 0, data.Length);
        }
        public static string DeserializeString(byte[] data, int originalsize)
        {
            if (data == null || data.Length == 0)
                return null;
            try
            {
                return Encoding.UTF8.GetString(Decode(data, 0, data.Length, originalsize));
            }
            catch
            {
                return null;
            }
        }
        public static T DeserializeObject<T>(byte[] data, int originalsize) where T : class
        {
            if (data == null || data.Length == 0)
                return null;
            try
            {
                return JsonConvert.DeserializeObject<T>(
                    Encoding.UTF8.GetString(Decode(data, 0, data.Length, originalsize)), new JsonSerializerSettings
                    {
                        Error = HandleDeserializationError
                    });
            }
            catch
            {
                return null;
            }
        }
        public static void PopulateObject<T>(T def,byte[] data, int originalsize) where T : class
        {
            if (data == null || data.Length == 0)
                return;
            try
            {

                JsonConvert.PopulateObject(
                    Encoding.UTF8.GetString(Decode(data, 0, data.Length, originalsize)), def, new JsonSerializerSettings
                    {
                        Error = HandleDeserializationError
                    });
            }
            catch
            {
            }
        }

        public static void HandleDeserializationError(object sender, ErrorEventArgs errorArgs)
        {
            var currentError = errorArgs.ErrorContext.Error.Message;
            errorArgs.ErrorContext.Handled = true;
        }

        public static byte[] Encode(byte[] input, int inputOffset, int inputLength)
        {
            if (is64bit)
                return LZ4ps.LZ4Codec.Encode64(input, inputOffset, inputLength);
            return LZ4ps.LZ4Codec.Encode32(input, inputOffset, inputLength);
        }

        public static int Encode(byte[] input, int inputOffset, int inputLength, byte[] output, int outputOffset,
            int outputLength)
        {
            return is64bit 
                ? LZ4ps.LZ4Codec.Encode64(input, inputOffset, inputLength, output, outputOffset, outputLength) 
                : LZ4ps.LZ4Codec.Encode32(input, inputOffset, inputLength, output, outputOffset, outputLength);
        }

        public static byte[] Decode(byte[] input, int inputOffset, int inputLength, int outputlength)
        {
            return is64bit 
                ? LZ4ps.LZ4Codec.Decode64(input, inputOffset, inputLength, outputlength) 
                : LZ4ps.LZ4Codec.Decode32(input, inputOffset, inputLength, outputlength);
        }

        public static int Decode(byte[] input, int inputOffset, int inputLength, byte[] output, int outputOffset,
            int outputLength, bool knowouputlength)
        {
            return is64bit
                ? LZ4ps.LZ4Codec.Decode64(input, inputOffset, inputLength, output, outputOffset, outputLength, knowouputlength)
                : LZ4ps.LZ4Codec.Decode32(input, inputOffset, inputLength, output, outputOffset, outputLength, knowouputlength);
        }
    }
}