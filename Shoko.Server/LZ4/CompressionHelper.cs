﻿using System;
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

            var data =
                Encoding.UTF8.GetBytes(multiinheritance
                    ? JsonConvert.SerializeObject(obj,
                        new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All })
                    : JsonConvert.SerializeObject(obj));
            originalsize = data.Length;
            return Encode(data, 0, data.Length);
        }

        public static T DeserializeObject<T>(byte[] data, int originalsize, JsonConverter[] converters = null)
            where T : class
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            try
            {
                var settings = converters == null
                    ? new JsonSerializerSettings { Error = HandleDeserializationError }
                    : new JsonSerializerSettings { Error = HandleDeserializationError, Converters = converters };
                var obj = Encoding.UTF8.GetString(Decode(data, 0, data.Length, originalsize));
                return JsonConvert.DeserializeObject<T>(obj, settings);
            }
            catch
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
            {
                return LZ4Codec.Encode64(input, inputOffset, inputLength);
            }

            return LZ4Codec.Encode32(input, inputOffset, inputLength);
        }

        public static int Encode(byte[] input, int inputOffset, int inputLength, byte[] output, int outputOffset,
            int outputLength)
        {
            return is64bit
                ? LZ4Codec.Encode64(input, inputOffset, inputLength, output, outputOffset, outputLength)
                : LZ4Codec.Encode32(input, inputOffset, inputLength, output, outputOffset, outputLength);
        }

        public static byte[] Decode(byte[] input, int inputOffset, int inputLength, int outputlength)
        {
            return is64bit
                ? LZ4Codec.Decode64(input, inputOffset, inputLength, outputlength)
                : LZ4Codec.Decode32(input, inputOffset, inputLength, outputlength);
        }

        public static int Decode(byte[] input, int inputOffset, int inputLength, byte[] output, int outputOffset,
            int outputLength, bool knowouputlength)
        {
            return is64bit
                ? LZ4Codec.Decode64(input, inputOffset, inputLength, output, outputOffset, outputLength,
                    knowouputlength)
                : LZ4Codec.Decode32(input, inputOffset, inputLength, output, outputOffset, outputLength,
                    knowouputlength);
        }
    }
}
