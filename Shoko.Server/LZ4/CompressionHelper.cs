using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
#if UNSAFE
using LZ4pn;

#else
using LZ4ps;
#endif

namespace Shoko.Server.LZ4
{
    public static class CompressionHelper
    {
        private static bool is64bit;

        static CompressionHelper()
        {
            is64bit = Environment.Is64BitProcess;
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
            catch (Exception e)
            {
                return null;
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
                return LZ4Codec.Encode64(input, inputOffset, inputLength);
            return LZ4Codec.Encode32(input, inputOffset, inputLength);
        }

        public static int Encode(byte[] input, int inputOffset, int inputLength, byte[] output, int outputOffset,
            int outputLength)
        {
            if (is64bit)
                return LZ4Codec.Encode64(input, inputOffset, inputLength, output, outputOffset, outputLength);
            return LZ4Codec.Encode32(input, inputOffset, inputLength, output, outputOffset, outputLength);
        }

        public static byte[] Decode(byte[] input, int inputOffset, int inputLength, int outputlength)
        {
            if (is64bit)
                return LZ4Codec.Decode64(input, inputOffset, inputLength, outputlength);
            return LZ4Codec.Decode32(input, inputOffset, inputLength, outputlength);
        }

        public static int Decode(byte[] input, int inputOffset, int inputLength, byte[] output, int outputOffset,
            int outputLength, bool knowouputlength)
        {
            if (is64bit)
                return LZ4Codec.Decode64(input, inputOffset, inputLength, output, outputOffset, outputLength,
                    knowouputlength);
            return LZ4Codec.Decode32(input, inputOffset, inputLength, output, outputOffset, outputLength,
                knowouputlength);
        }
    }
}