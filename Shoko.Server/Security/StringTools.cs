using System;
using System.Text;

namespace Shoko.Server
{
    public class StringTools
    {
        /// <summary>
        /// Convert a UTF8 string to its byte array representation
        /// </summary>
        /// <param name="stringToConvert"></param>
        /// <returns></returns>
        public static byte[] ConvertStringToByteArray(string stringToConvert)
        {
            System.Text.UTF8Encoding utf = new UTF8Encoding();
            return utf.GetBytes(stringToConvert);
        }

        /// <summary>
        /// Convert a Byte Array of Unicode values (UTF-8 encoded) to a complete String.
        /// </summary>
        /// <param name="characters">Unicode Byte Array to be converted to String</param>
        /// <returns>String converted from Unicode Byte Array</returns>
        private String ConvertByteArrayToString(Byte[] characters)
        {
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetString(characters);
        }

        /// <summary>
        /// Convert a byte array to a string in hexadecimal base
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string ConvertByteArrayToHex(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Truncates a string to the specified length
        /// </summary>
        /// <param name="sourceString">the string to truncate</param>
        /// <param name="maxLength">the max length of the string</param>
        /// <returns></returns>
        public static string TruncateString(string sourceString, int maxLength)
        {
            return TruncateString(sourceString, 0, maxLength);
        }

        /// <summary>
        /// Truncates a string to the specified length
        /// </summary>
        /// <param name="sourceString">the string to truncate</param>
        /// <param name="startIndex">The index of the start</param>
        /// <param name="maxLength">the max length of the string</param>
        /// <returns></returns>
        public static string TruncateString(string sourceString, int startIndex, int maxLength)
        {
            if (string.IsNullOrEmpty(sourceString))
                return sourceString;
            return startIndex > sourceString.Length 
                ? String.Empty 
                : sourceString.Substring(startIndex, Math.Min(maxLength, sourceString.Length - startIndex));
        }
    }
}