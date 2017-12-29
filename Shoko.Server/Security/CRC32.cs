using System;
using System.Collections;
using System.Security.Cryptography;

namespace Shoko.Server.Security
{
    public class CRC32 : HashAlgorithm
    {
        protected static uint AllOnes = 0xffffffff;
        protected static Hashtable cachedCRC32Tables;
        protected static bool autoCache;

        protected uint[] crc32Table;
        private uint m_crc;

        /// <summary>
        /// Returns the default polynomial (used in WinZip, Ethernet, etc)
        /// </summary>
        public static uint DefaultPolynomial
        {
            get { return 0x04C11DB7; }
        }

        /// <summary>
        /// Gets or sets the auto-cache setting of this class.
        /// </summary>
        public static bool AutoCache
        {
            get { return autoCache; }
            set { autoCache = value; }
        }

        /// <summary>
        /// Initialize the cache
        /// </summary>
        static CRC32()
        {
            cachedCRC32Tables = Hashtable.Synchronized(new Hashtable());
            autoCache = true;
        }

        public static void ClearCache()
        {
            cachedCRC32Tables.Clear();
        }


        private static uint Reflect(uint val)
        {
            uint oval = 0;
            for (int i = 0; i < 32; i++)
            {
                oval = (oval << 1) + (val & 1);
                val >>= 1;
            }
            return oval;
        }

        protected static uint[] BuildCRC32Table(uint ulPolynomial)
        {
            uint dwCrc;
            uint[] table = new uint[256];

            ulPolynomial = Reflect(ulPolynomial);

            // 256 values representing ASCII character codes. 
            for (int i = 0; i < 256; i++)
            {
                dwCrc = (uint) i;
                for (int j = 8; j > 0; j--)
                {
                    if ((dwCrc & 1) == 1)
                        dwCrc = (dwCrc >> 1) ^ ulPolynomial;
                    else
                        dwCrc >>= 1;
                }
                table[i] = dwCrc;
            }
            return table;
        }


        /// <summary>
        /// Creates a CRC32 object using the DefaultPolynomial
        /// </summary>
        public CRC32()
            : this(DefaultPolynomial)
        {
        }

        /// <summary>
        /// Creates a CRC32 object using the specified Creates a CRC32 object 
        /// </summary>
        public CRC32(uint aPolynomial)
            : this(aPolynomial, CRC32.AutoCache)
        {
        }

        /// <summary>
        /// Construct the 
        /// </summary>
        public CRC32(uint aPolynomial, bool cacheTable)
        {
            this.HashSizeValue = 32;

            crc32Table = (uint[]) cachedCRC32Tables[aPolynomial];
            if (crc32Table == null)
            {
                crc32Table = CRC32.BuildCRC32Table(aPolynomial);
                if (cacheTable)
                    cachedCRC32Tables.Add(aPolynomial, crc32Table);
            }
            Initialize();
        }

        /// <summary>
        /// Initializes an implementation of HashAlgorithm.
        /// </summary>
        public sealed override void Initialize()
        {
            m_crc = AllOnes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        protected override void HashCore(byte[] buffer, int offset, int count)
        {
            Initialize();

            // Save the text in the buffer. 
            for (int i = offset; i < offset + count; i++)
            {
                ulong tabPtr = (m_crc & 0xFF) ^ buffer[i];
                m_crc >>= 8;
                m_crc ^= crc32Table[tabPtr];
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected override byte[] HashFinal()
        {
            byte[] finalHash = new byte[4];
            ulong finalCRC = m_crc ^ AllOnes;

            finalHash[0] = (byte) ((finalCRC >> 24) & 0xFF);
            finalHash[1] = (byte) ((finalCRC >> 16) & 0xFF);
            finalHash[2] = (byte) ((finalCRC >> 8) & 0xFF);
            finalHash[3] = (byte) ((finalCRC >> 0) & 0xFF);

            return finalHash;
        }

        /// <summary>
        /// Overloaded. Computes the hash value for the input data.
        /// </summary>
        new public byte[] ComputeHash(byte[] buffer)
        {
            return ComputeHash(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Overloaded. Computes the hash value for the input data.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        new public byte[] ComputeHash(byte[] buffer, int offset, int count)
        {
            HashCore(buffer, offset, count);
            return HashFinal();
        }

        public uint ComputeHash(string buffer)
        {
            byte[] array = StringTools.ConvertStringToByteArray(buffer);

            if (array != null)
            {
                HashCore(array, 0, array.Length);
                byte[] crc = HashFinal();

                return BitConverter.ToUInt32(crc, 0);
            }

            return 0;
        }
    }
}