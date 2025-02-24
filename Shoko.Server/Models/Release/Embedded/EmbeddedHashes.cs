using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Server.Models.Release;

public class EmbeddedHashes : IHashes
{
    private string? _crc { get; set; }

    /// <inheritdoc/>
    public string? CRC
    {
        get => _crc;
        set
        {
            if (value is not { Length: 8 })
                return;
            _crc = value.ToUpper();
        }
    }

    private string? _md5 { get; set; }

    /// <inheritdoc/>
    public string? MD5
    {
        get => _md5;
        set
        {
            if (value is not { Length: 32 })
                return;
            _md5 = value.ToUpper();
        }
    }

    private string? _ed2k { get; set; }

    /// <inheritdoc/>
    public string ED2K
    {
        get => _ed2k ?? "00000000000000000000000000000000";
        set
        {
            if (value is not { Length: 32 })
                return;
            _ed2k = value.ToUpper();
        }
    }

    private string? _sha1 { get; set; }

    /// <inheritdoc/>
    public string? SHA1
    {
        get => _sha1;
        set
        {
            if (value is not { Length: 40 })
                return;
            _sha1 = value.ToUpper();
        }
    }

    /// <inheritdoc/>
    public string? this[HashAlgorithmName algorithm]
        => algorithm switch
        {
            HashAlgorithmName.ED2K => _ed2k,
            HashAlgorithmName.MD5 => _md5,
            HashAlgorithmName.SHA1 => _sha1,
            HashAlgorithmName.CRC32 => _crc,
            _ => null,
        };

    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedHashes"/>.
    /// </summary>
    public EmbeddedHashes() { }

    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedHashes"/> based on the given <paramref name="hashes"/>.
    /// </summary>
    public EmbeddedHashes(IHashes hashes)
    {
        CRC = hashes.CRC;
        MD5 = hashes.MD5;
        ED2K = hashes.ED2K;
        SHA1 = hashes.SHA1;
    }
}
