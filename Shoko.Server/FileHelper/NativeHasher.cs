using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using File = Pri.LongPath.File;

namespace Shoko.Server.FileHelper
{
    class NativeHasher
    {
        public static (string e2dk, string crc32, string md5, string sha1) GetHash(string filename)
        {
            StringBuilder sb = new StringBuilder();
            Native.rhash_library_init();
            IntPtr ctx = Native.rhash_init(RHashIds.RHASH_ED2K | RHashIds.RHASH_CRC32 | RHashIds.RHASH_MD5 |
                                           RHashIds.RHASH_SHA1);

            string e2dk = "", crc32 = "", md5 = "", sha1 = "";

            using (Stream source = File.OpenRead(filename))
            {
                byte[] buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    var buf = Marshal.AllocHGlobal(bytesRead);
                    Marshal.Copy(buffer, 0, buf, bytesRead);
                    Native.rhash_update(ctx, buf, bytesRead);
                    Marshal.FreeHGlobal(buf);
                }
            }

            IntPtr output = Marshal.AllocHGlobal(200);

            Native.rhash_print(output, ctx, RHashIds.RHASH_ED2K, RhashPrintSumFlags.RHPR_DEFAULT);
            e2dk = Marshal.PtrToStringAnsi(output);

            Native.rhash_print(output, ctx, RHashIds.RHASH_CRC32, RhashPrintSumFlags.RHPR_DEFAULT);
            crc32 = Marshal.PtrToStringAnsi(output);

            Native.rhash_print(output, ctx, RHashIds.RHASH_MD5, RhashPrintSumFlags.RHPR_DEFAULT);
            md5 = Marshal.PtrToStringAnsi(output);

            Native.rhash_print(output, ctx, RHashIds.RHASH_SHA1, RhashPrintSumFlags.RHPR_DEFAULT);
            sha1 = Marshal.PtrToStringAnsi(output);

            Marshal.FreeHGlobal(output);

            Native.rhash_final(ctx, IntPtr.Zero);
            Native.rhash_free(ctx);

            return (e2dk, crc32, md5, sha1);
        }


        private static class Native
        {
            private const string Lib = "librhash";

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void rhash_library_init();

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern IntPtr rhash_init(RHashIds ids);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int rhash_update(IntPtr ctx, IntPtr data, int count);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int rhash_final(IntPtr ctx, IntPtr result);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            internal static extern int rhash_print(IntPtr outStr, IntPtr ctx, RHashIds hashId, RhashPrintSumFlags flags);

            [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void rhash_free(IntPtr ctx);
        }

        [Flags]
        private enum RhashPrintSumFlags
        {
            /** print in a default format */
            RHPR_DEFAULT = 0x0,
            /** output as binary message digest */
            RHPR_RAW = 0x1,
            /** print as a hexadecimal string */
            RHPR_HEX = 0x2,
            /** print as a base32-encoded string */
            RHPR_BASE32 = 0x3,
            /** print as a base64-encoded string */
            RHPR_BASE64 = 0x4,

            /**
             * Print as an uppercase string. Can be used
             * for base32 or hexadecimal format only.
             */
            RHPR_UPPERCASE = 0x8,

            /**
             * Reverse hash bytes. Can be used for GOST hash.
             */
            RHPR_REVERSE = 0x10,

            /** don't print 'magnet:?' prefix in rhash_print_magnet */
            RHPR_NO_MAGNET = 0x20,
            /** print file size in rhash_print_magnet */
            RHPR_FILESIZE = 0x40,
        };

        [Flags]
        private enum RHashIds
        {
            RHASH_CRC32 = 0x01,
            RHASH_MD4 = 0x02,
            RHASH_MD5 = 0x04,
            RHASH_SHA1 = 0x08,
            RHASH_TIGER = 0x10,
            RHASH_TTH = 0x20,
            RHASH_BTIH = 0x40,
            RHASH_ED2K = 0x80,
            RHASH_AICH = 0x100,
            RHASH_WHIRLPOOL = 0x200,
            RHASH_RIPEMD160 = 0x400,
            RHASH_GOST = 0x800,
            RHASH_GOST_CRYPTOPRO = 0x1000,
            RHASH_HAS160 = 0x2000,
            RHASH_SNEFRU128 = 0x4000,
            RHASH_SNEFRU256 = 0x8000,
            RHASH_SHA224 = 0x10000,
            RHASH_SHA256 = 0x20000,
            RHASH_SHA384 = 0x40000,
            RHASH_SHA512 = 0x80000,
            RHASH_EDONR256 = 0x0100000,
            RHASH_EDONR512 = 0x0200000,
            RHASH_SHA3_224 = 0x0400000,
            RHASH_SHA3_256 = 0x0800000,
            RHASH_SHA3_384 = 0x1000000,
            RHASH_SHA3_512 = 0x2000000,

            /** The bit-mask containing all supported hashe functions */
            RHASH_ALL_HASHES = RHASH_CRC32 | RHASH_MD4 | RHASH_MD5 | RHASH_ED2K | RHASH_SHA1 |
                               RHASH_TIGER | RHASH_TTH | RHASH_GOST | RHASH_GOST_CRYPTOPRO |
                               RHASH_BTIH | RHASH_AICH | RHASH_WHIRLPOOL | RHASH_RIPEMD160 |
                               RHASH_HAS160 | RHASH_SNEFRU128 | RHASH_SNEFRU256 |
                               RHASH_SHA224 | RHASH_SHA256 | RHASH_SHA384 | RHASH_SHA512 |
                               RHASH_SHA3_224 | RHASH_SHA3_256 | RHASH_SHA3_384 | RHASH_SHA3_512 |
                               RHASH_EDONR256 | RHASH_EDONR512,

            /** The number of supported hash functions */
            RHASH_HASH_COUNT = 26
        };
    }
}
