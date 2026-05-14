using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Utilities;

public static partial class Utils
{
    public static IServiceProvider ServiceContainer { get; set; }

    public static ISettingsProvider SettingsProvider { get; set; }

    public static string GetDistinctPath(string fullPath)
    {
        var parent = Path.GetDirectoryName(fullPath);
        return string.IsNullOrEmpty(parent) ? fullPath : Path.Combine(Path.GetFileName(parent), Path.GetFileName(fullPath));
    }

    public static bool IsVideo(string fileName)
        => SettingsProvider.GetSettings().Import.VideoExtensions.Any(extName => fileName.EndsWith(extName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Determines an encoded string's encoding by analyzing its byte order mark (BOM).
    /// Defaults to ASCII when detection of the text file's endianness fails.
    /// </summary>
    /// <param name="data">Byte array of the encoded string</param>
    /// <returns>The detected encoding.</returns>
    public static Encoding GetEncoding(byte[] data)
    {
        if (data.Length < 4)
        {
            return Encoding.ASCII;
        }
        // Analyze the BOM
#pragma warning disable SYSLIB0001
        if (data[0] == 0x2b && data[1] == 0x2f && data[2] == 0x76)
        {
            return Encoding.UTF7;
        }

        if (data[0] == 0xef && data[1] == 0xbb && data[2] == 0xbf)
        {
            return Encoding.UTF8;
        }

        if (data[0] == 0xff && data[1] == 0xfe)
        {
            return Encoding.Unicode; //UTF-16LE
        }

        if (data[0] == 0xfe && data[1] == 0xff)
        {
            return Encoding.BigEndianUnicode; //UTF-16BE
        }

        if (data[0] == 0 && data[1] == 0 && data[2] == 0xfe && data[3] == 0xff)
        {
            return Encoding.UTF32;
        }

        return Encoding.ASCII;
#pragma warning restore SYSLIB0001
    }
}
