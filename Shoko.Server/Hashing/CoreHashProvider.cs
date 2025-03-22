using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Hashing;
using Shoko.Server.Hashing.HashAlgorithms;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Hashing;

/// <summary>
///    Responsible for providing the core hashes for a video. Among them, only
///    ED2K is required to be calculated, but not necessarily by this provider.
/// </summary>
public class CoreHashProvider(ILogger<CoreHashProvider> logger, ConfigurationProvider<CoreHashProvider.CoreHasherConfiguration> configurationProvider) : IHashProvider
{
    /// <inheritdoc/>
    public string Name => "Built-In Hasher";

    /// <inheritdoc/>
    public Version Version => Assembly.GetExecutingAssembly().GetName().Version!;

    /// <inheritdoc/>
    public IReadOnlySet<string> AvailableHashTypes => new HashSet<string>() { "ED2K", "MD5", "CRC32", "SHA1", "SHA256", "SHA512" };

    /// <inheritdoc/>
    public IReadOnlySet<string> DefaultEnabledHashTypes => new HashSet<string>() { "ED2K" };

    static CoreHashProvider()
    {
        if (Utils.IsLinux)
            return;

        try
        {
            var dllPath = Assembly.GetEntryAssembly()?.Location;
            if (dllPath != null)
            {
                var fi = new FileInfo(dllPath);
                dllPath = Path.Combine(fi.Directory!.FullName, Environment.Is64BitProcess ? "x64" : "x86", "librhash.dll");
                _rhashModule.ModuleHandle = LoadLibraryEx(dllPath, IntPtr.Zero, 0);
            }
        }
        catch (Exception)
        {
            _rhashModule.ModuleHandle = IntPtr.Zero;
        }
    }

    public static string? GetRhashVersion()
    {
        if (Utils.IsLinux)
            return null;

        try
        {
            var dllPath = Assembly.GetEntryAssembly()!.Location;
            var fi = new FileInfo(dllPath);
            dllPath = Path.Combine(fi.Directory!.FullName, Environment.Is64BitProcess ? "x64" : "x86", "librhash.dll");

            if (!File.Exists(dllPath)) return null;

            var fvi = FileVersionInfo.GetVersionInfo(dllPath);
            return $"RHash {fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}.{fvi.FilePrivatePart} ({dllPath})";
        }
        catch
        {
            return null;
        }
    }

    #region IHashProvider Implementation

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<HashDigest>> GetHashesForVideo(HashingRequest request, CancellationToken cancellationToken = default)
    {
        var (file, existingHashes, enabledHashTypes) = request;
        var config = configurationProvider.Load();
        var coreRequest = new CoreHashingRequest()
        {
            Path = file.FullName,
            ED2K = !existingHashes.Any(h => h.Type is "ED2K") && enabledHashTypes.Contains("ED2K"),
            CRC32 = !existingHashes.Any(h => h.Type is "CRC32") && enabledHashTypes.Contains("CRC32"),
            MD5 = !existingHashes.Any(h => h.Type is "MD5") && enabledHashTypes.Contains("MD5"),
            SHA1 = !existingHashes.Any(h => h.Type is "SHA1") && enabledHashTypes.Contains("SHA1"),
            SHA256 = !existingHashes.Any(h => h.Type is "SHA256") && enabledHashTypes.Contains("SHA256"),
            SHA512 = !existingHashes.Any(h => h.Type is "SHA512") && enabledHashTypes.Contains("SHA512"),
            ForceSharpHasher = config.AlwaysUseSharpHasher,
        };

        logger.LogDebug("Calculating {Count} hashes for {File}", coreRequest.Count, file.Name);

        var time = Stopwatch.StartNew();
        var calculatedHashes = await CalculateHashes(coreRequest, cancellationToken).ConfigureAwait(false);
        time.Stop();

        logger.LogDebug("Calculated {Count} hashes for {File} in {Time}", calculatedHashes.Count, file.Name, time.Elapsed);

        var hashes = calculatedHashes
            .Concat(existingHashes.Select(h => new HashDigest() { Type = h.Type, Value = h.Value, Metadata = h.Metadata }))
            .DistinctBy(h => h.Type)
            .OrderBy(h => (h.Type, h.Value, h.Metadata))
            .ToList();
        return hashes;
    }

    #endregion

    #region Shared

    private async Task<List<HashDigest>> CalculateHashes(CoreHashingRequest request, CancellationToken cancellationToken = default)
    {
        // Short circuit if no hashes are requested.
        if (request.IsEmpty)
            return [];

        if (request.ForceSharpHasher || (_rhashModule.ModuleHandle == IntPtr.Zero && !Utils.IsLinux))
            return await CalculateHashesSharp(request, cancellationToken).ConfigureAwait(false);

        try
        {
            var path = request.Path;
            if (!Utils.IsLinux)
                path = path.StartsWith(@"\\")
                    ? path
                    : @"\\?\" + path; //only prepend non-UNC paths (or paths that have this already)

            return await CalculateHashesRhash(path, request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while attempting to run the RHash hasher. Trying C# hasher instead: {Path}", request.Path);
        }

        return await CalculateHashesSharp(request, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region C# Hasher

    private const int ChunkSize = 9728000;

    private async Task<List<HashDigest>> CalculateHashesSharp(CoreHashingRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("Using C# hasher on file: {Path}", request.Path);

        using var stream = File.OpenRead(request.Path);
        using var md4 = request.ED2K ? MD4.Create() : null;
        using var md5 = request.MD5 ? MD5.Create() : null;
        using var sha1 = request.SHA1 ? SHA1.Create() : null;
        using var sha256 = request.SHA256 ? SHA256.Create() : null;
        using var sha512 = request.SHA512 ? SHA512.Create() : null;
        using var crc32 = request.CRC32 ? new CRC32() : null;
        var totalBytes = stream.Length;
        var numberOfBlocks = (int)Math.DivRem(totalBytes, ChunkSize, out var remainder);
        if (remainder > 0)
            numberOfBlocks++;

        var bytesRemaining = stream.Length;
        var bytesToRead = totalBytes > ChunkSize
            ? ChunkSize
            : (int)bytesRemaining;
        var ed2kHash = new byte[16 * numberOfBlocks];
        var workBuffer = new byte[bytesToRead];
        var byteOffset = 0L;
        var chunkCount = 0L;
        while (bytesRemaining > 0)
        {
            if (cancellationToken.IsCancellationRequested)
                return [];

            chunkCount++;

            logger.LogTrace("Hashing Chunk: {ChunkSize}", chunkCount.ToString());

            var bytesRead = await stream.ReadAsync(workBuffer.AsMemory(0, bytesToRead), cancellationToken).ConfigureAwait(false);
            if (md4 is not null)
            {
                var md4Hash = md4.ComputeHash(workBuffer, 0, bytesRead);
                var chunkOffset = (int)((chunkCount - 1) * 16);
                for (var index = 0; index < 16; index++)
                    ed2kHash[chunkOffset + index] = md4Hash[index];
            }
            md5?.TransformBlock(workBuffer, 0, bytesRead, workBuffer, 0);
            sha1?.TransformBlock(workBuffer, 0, bytesRead, workBuffer, 0);
            crc32?.TransformBlock(workBuffer, 0, bytesRead, workBuffer, 0);
            sha256?.TransformBlock(workBuffer, 0, bytesRead, workBuffer, 0);
            sha512?.TransformBlock(workBuffer, 0, bytesRead, workBuffer, 0);
            byteOffset += ChunkSize;
            bytesRemaining = totalBytes - byteOffset;
            if (bytesRemaining < ChunkSize)
                bytesToRead = (int)bytesRemaining;
        }

        if (cancellationToken.IsCancellationRequested)
            return [];

        md5?.TransformFinalBlock(workBuffer, 0, 0);
        sha1?.TransformFinalBlock(workBuffer, 0, 0);
        crc32?.TransformFinalBlock(workBuffer, 0, 0);
        sha256?.TransformFinalBlock(workBuffer, 0, 0);
        sha512?.TransformFinalBlock(workBuffer, 0, 0);

        var hashes = new List<HashDigest>();
        if (md4 is not null)
        {
            hashes.Add(new()
            {
                Type = "ED2K",
                Value = numberOfBlocks > 1
                    ? BitConverter.ToString(md4.ComputeHash(ed2kHash)).Replace("-", string.Empty).ToUpper()
                    : BitConverter.ToString(ed2kHash).Replace("-", string.Empty).ToUpper()
            });
        }
        if (crc32 is not null)
            hashes.Add(new() { Type = "CRC32", Value = BitConverter.ToString(crc32.Hash!).Replace("-", string.Empty).ToUpper() });
        if (md5 is not null)
            hashes.Add(new() { Type = "MD5", Value = BitConverter.ToString(md5.Hash!).Replace("-", string.Empty).ToUpper() });
        if (sha1 is not null)
            hashes.Add(new() { Type = "SHA1", Value = BitConverter.ToString(sha1.Hash!).Replace("-", string.Empty).ToUpper() });
        if (sha256 is not null)
            hashes.Add(new() { Type = "SHA256", Value = BitConverter.ToString(sha256.Hash!).Replace("-", string.Empty).ToUpper() });
        if (sha512 is not null)
            hashes.Add(new() { Type = "SHA512", Value = BitConverter.ToString(sha512.Hash!).Replace("-", string.Empty).ToUpper() });
        return hashes;
    }

    #endregion

    #region RHash Hasher

    private async Task<List<HashDigest>> CalculateHashesRhash(string path, CoreHashingRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("Using RHash hasher on file: {Path}", request.Path);

        var ids = (RHashIds)0;
        if (request.ED2K) ids |= RHashIds.RHASH_ED2K;
        if (request.CRC32) ids |= RHashIds.RHASH_CRC32;
        if (request.MD5) ids |= RHashIds.RHASH_MD5;
        if (request.SHA1) ids |= RHashIds.RHASH_SHA1;
        if (request.SHA256) ids |= RHashIds.RHASH_SHA256;
        if (request.SHA512) ids |= RHashIds.RHASH_SHA512;
        if (ids == 0) return [];
        Native.rhash_library_init();
        var ctx = Native.rhash_init(ids);

        using (var source = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                    return [];
                var buf = Marshal.AllocHGlobal(bytesRead);
                Marshal.Copy(buffer, 0, buf, bytesRead);
                Native.rhash_update(ctx, buf, bytesRead);
                Marshal.FreeHGlobal(buf);
            }
        }

        if (cancellationToken.IsCancellationRequested)
            return [];

        var hashes = new List<HashDigest>();
        var output = Marshal.AllocHGlobal(200);
        if (request.ED2K)
        {
            Native.rhash_print(output, ctx, RHashIds.RHASH_ED2K, RhashPrintSumFlags.RHPR_DEFAULT);
            var e2dk = Marshal.PtrToStringAnsi(output)!;
            hashes.Add(new HashDigest() { Type = "ED2K", Value = e2dk.ToUpperInvariant() });
        }

        if (request.CRC32)
        {
            Native.rhash_print(output, ctx, RHashIds.RHASH_CRC32, RhashPrintSumFlags.RHPR_DEFAULT);
            var crc32 = Marshal.PtrToStringAnsi(output)!;
            hashes.Add(new HashDigest() { Type = "CRC32", Value = crc32.ToUpperInvariant() });
        }

        if (request.MD5)
        {
            Native.rhash_print(output, ctx, RHashIds.RHASH_MD5, RhashPrintSumFlags.RHPR_DEFAULT);
            var md5 = Marshal.PtrToStringAnsi(output)!;
            hashes.Add(new HashDigest() { Type = "MD5", Value = md5.ToUpperInvariant() });
        }

        if (request.SHA1)
        {
            Native.rhash_print(output, ctx, RHashIds.RHASH_SHA1, RhashPrintSumFlags.RHPR_DEFAULT);
            var sha1 = Marshal.PtrToStringAnsi(output)!;
            hashes.Add(new HashDigest() { Type = "SHA1", Value = sha1.ToUpperInvariant() });
        }

        if (request.SHA256)
        {
            Native.rhash_print(output, ctx, RHashIds.RHASH_SHA256, RhashPrintSumFlags.RHPR_DEFAULT);
            var sha256 = Marshal.PtrToStringAnsi(output)!;
            hashes.Add(new HashDigest() { Type = "SHA256", Value = sha256.ToUpperInvariant() });
        }

        if (request.SHA512)
        {
            Native.rhash_print(output, ctx, RHashIds.RHASH_SHA512, RhashPrintSumFlags.RHPR_DEFAULT);
            var sha512 = Marshal.PtrToStringAnsi(output)!;
            hashes.Add(new HashDigest() { Type = "SHA512", Value = sha512 });
        }

        Marshal.FreeHGlobal(output);

        Native.rhash_final(ctx, IntPtr.Zero);
        Native.rhash_free(ctx);

        return hashes;
    }

    #region DLL Loading

    private static readonly Destructor _rhashModule = new(); //static Destructor hack

    [Flags]
    internal enum LoadLibraryFlags : uint
    {
        DONT_RESOLVE_DLL_REFERENCES = 0x00000001,
        LOAD_IGNORE_CODE_AUTHZ_LEVEL = 0x00000010,
        LOAD_LIBRARY_AS_DATAFILE = 0x00000002,
        LOAD_LIBRARY_AS_DATAFILE_EXCLUSIVE = 0x00000040,
        LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020,
        LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008
    }

    [DllImport("kernel32.dll")]
    internal static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, LoadLibraryFlags dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FreeLibrary(IntPtr hModule);

    private sealed class Destructor : IDisposable
    {
        public IntPtr ModuleHandle { get; set; } = IntPtr.Zero;

        ~Destructor()
        {
            if (ModuleHandle != IntPtr.Zero)
            {
                FreeLibrary(ModuleHandle);
                ModuleHandle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (ModuleHandle != IntPtr.Zero)
            {
                FreeLibrary(ModuleHandle);
                ModuleHandle = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }
    }

    #endregion

    #region RHash Constants

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
        RHPR_FILESIZE = 0x40
    }

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
    }

    #endregion

    #endregion

    #region Helper Classes

    /// <summary>
    /// Internal request for passing hashing parameters.
    /// </summary>
    private class CoreHashingRequest
    {
        /// <summary>
        /// The absolute path to the file to hash.
        /// </summary>
        public required string Path { get; init; }

        /// <summary>
        /// Indicates that we want a ED2K hash digest.
        /// </summary>
        public bool ED2K { get; init; }

        /// <summary>
        /// Indicates that we want a CRC32 hash digest.
        /// </summary>
        public bool CRC32 { get; init; }

        /// <summary>
        /// Indicates that we want a MD5 hash digest.
        /// </summary>
        public bool MD5 { get; init; }

        /// <summary>
        /// Indicates that we want a SHA1 hash digest.
        /// </summary>
        public bool SHA1 { get; init; }

        /// <summary>
        /// Indicates that we want a SHA256 hash digest.
        /// </summary>
        public bool SHA256 { get; init; }

        /// <summary>
        /// Indicates that we want a SHA512 hash digest.
        /// </summary>
        public bool SHA512 { get; init; }

        /// <summary>
        /// Indicates that only the C# hasher should be used.
        /// </summary>
        public bool ForceSharpHasher { get; init; }

        /// <summary>
        /// Indicates that no hashes should be calculated.
        /// </summary>
        public bool IsEmpty => !ED2K && !CRC32 && !MD5 && !SHA1 && !SHA256 && !SHA512;

        /// <summary>
        /// The number of hashes to calculate.
        /// </summary>
        public int Count
        {
            get
            {
                var count = 0;
                if (ED2K) count++;
                if (CRC32) count++;
                if (MD5) count++;
                if (SHA1) count++;
                if (SHA256) count++;
                if (SHA512) count++;
                return count;
            }
        }
    }

    /// <summary>
    /// Configure how hashes are calculated in the built-in hasher.
    /// </summary>
    [Display(Name = "Built-In Hasher")]
    public class CoreHasherConfiguration : INewtonsoftJsonConfiguration
    {
        /// <summary>
        /// Indicates that only the C# hasher should be used. This will
        /// disable the use of the native hasher until the setting is changed
        /// again.
        /// </summary>
        [Required]
        [Display(Name = "Always use C# Hasher")]
        public bool AlwaysUseSharpHasher { get; set; }
    }

    #endregion
}
