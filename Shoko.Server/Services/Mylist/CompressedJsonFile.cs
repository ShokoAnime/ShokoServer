using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;

namespace Shoko.Server.Services.Mylist;

/// <summary>
/// The compressed JSON the MyList caches persist to. Neither is read by eye and
/// both run to hundreds of thousands of entries, so they are written compact
/// and compressed.
/// </summary>
internal static class CompressedJsonFile
{
    public static T? Read<T>(string path) where T : class
    {
        using var fileStream = File.OpenRead(path);
        using var decompressed = new BrotliStream(fileStream, CompressionMode.Decompress);
        using var reader = new StreamReader(decompressed);
        return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
    }

    /// <summary>
    /// Writes through a temporary file and moves it into place, so an
    /// interrupted write cannot leave a truncated cache where a good one was.
    /// </summary>
    /// <param name="path">
    ///   Where to write it.
    /// </param>
    /// <param name="value">
    ///   The object to serialise.
    /// </param>
    /// <param name="level">
    ///   Pick this from how often the file is written, not from how much it
    ///   matters. <see cref="CompressionLevel.Optimal"/> is Brotli quality 4 and
    ///   costs a fifth of a second on the ten megabyte MyList cache;
    ///   <see cref="CompressionLevel.SmallestSize"/> is quality 11, another
    ///   fifth smaller and twenty-two seconds for the same file.
    /// </param>
    public static void Write<T>(string path, T value, CompressionLevel level)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory is not null) Directory.CreateDirectory(directory);

        var serialized = JsonConvert.SerializeObject(value, Formatting.None);
        var tempPath = path + ".tmp";
        using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var compressor = new BrotliStream(fileStream, level))
        using (var writer = new StreamWriter(compressor))
            writer.Write(serialized);
        File.Move(tempPath, path, true);
    }

    /// <summary>
    /// Removes a superseded cache file. Returns whether there was one to remove,
    /// so the caller can say so without checking twice.
    /// </summary>
    public static bool DeleteIfPresent(string path)
    {
        if (!File.Exists(path)) return false;

        File.Delete(path);
        return true;
    }
}
