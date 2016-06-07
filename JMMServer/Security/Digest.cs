using System;
using System.Security.Cryptography;

namespace JMMServer
{
    /// <summary>
    ///     Simple wrapper class for generating message digests
    /// </summary>
    public class Digest
    {
        /// <summary>
        ///     The Digest algorithm type
        /// </summary>
        public enum Type
        {
            MD5 = 1,
            SHA1 = 2,
            SHA256 = 3,
            SHA512 = 4,
            CRAM_MD5 = 5
        }

        /// <summary>
        ///     Generate a message digest from the specified string using default SHA512 algo.
        /// </summary>
        /// <param name="source">source string to hash</param>
        /// <returns>message digest in hexadecimal form, or string.Empty if error occurs</returns>
        public static string Hash(string source)
        {
            return Hash(source, Type.SHA512);
        }

        /// <summary>
        ///     Generate a message digest from the specified string
        /// </summary>
        /// <param name="source">the message to hash</param>
        /// <param name="digestType">type of the hashing algorithm</param>
        /// <returns>message digest in hexadecimal form, or string.Empty if error occurs</returns>
        public static string Hash(string source, Type digestType)
        {
            if (source == null || source.Length <= 0)
                return string.Empty;

            var sourceBytes = StringTools.ConvertStringToByteArray(source);
            byte[] destBytes = null;

            switch (digestType)
            {
                case Type.CRAM_MD5:
                    throw new Exception("CRAM_MD5 not implemented");
                case Type.MD5:
                    destBytes = MD5.Create().ComputeHash(sourceBytes);
                    break;
                case Type.SHA1:
                    destBytes = SHA1.Create().ComputeHash(sourceBytes);
                    break;
                case Type.SHA256:
                    destBytes = SHA256.Create().ComputeHash(sourceBytes);
                    break;
                case Type.SHA512:
                    destBytes = SHA512.Create().ComputeHash(sourceBytes);
                    break;
                default:
                    break;
            }

            if (destBytes != null && destBytes.Length > 0)
                return StringTools.ConvertByteArrayToHex(destBytes);
            return string.Empty;
        }
    }
}