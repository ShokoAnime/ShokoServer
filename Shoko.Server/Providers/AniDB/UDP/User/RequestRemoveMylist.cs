using System;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

#nullable enable
namespace Shoko.Server.Providers.AniDB.UDP.User;

/// <summary>
/// Remove an entry from the User's AniDB MyList.
/// </summary>
public class RequestRemoveMylist : UDPRequest<Void>
{
    #region Mylist Entry Identification

    #region By List ID (lid)

    /// <summary>
    /// The ID of the AniDB MyList Entry (lid).
    /// </summary>
    public ulong? MylistID { get; set; }

    #endregion

    #region By File ID (fid)

    /// <summary>
    /// The ID of the AniDB File (fid).
    /// </summary>
    public ulong? FileID { get; set; }

    #endregion

    #region By ED2K & Size

    /// <summary>
    /// The ED2K hash of the file.
    /// </summary>
    public string? ED2K { get; set; }

    /// <summary>
    /// The size of the file.
    /// </summary>
    public long? Size { get; set; }

    #endregion

    #region By Anime ID (aid), Episode Type, & Episode Number

    /// <summary>
    /// The ID of the AniDB Anime (aid).
    /// </summary>
    public int? AnimeID { get; set; }

    /// <summary>
    /// The type of episode.
    /// </summary>
    public EpisodeType? EpisodeType { get; set; }

    /// <summary>
    /// The episode number.
    /// </summary>
    public int? EpisodeNumber { get; set; }

    #endregion

    #endregion

    protected override string BaseCommand
    {
        get
        {
            var command = "MYLISTDEL ";
            if (MylistID is > 0)
            {
                command += $"lid={MylistID.Value}";
            }
            else if (FileID is > 0)
            {
                command += $"fid={FileID.Value}";
            }
            else if (!string.IsNullOrEmpty(ED2K))
            {
                if (Size is not > 0)
                    throw new ArgumentException($"{nameof(Size)} is required if {nameof(ED2K)} is specified");
                command += $"ed2k={ED2K}&size={Size.Value}";
            }
            else if (AnimeID is > 0)
            {
                if (EpisodeType is null)
                    throw new ArgumentException($"{nameof(EpisodeType)} is required if {nameof(AnimeID)} is specified");
                if (EpisodeNumber is not > 0)
                    throw new ArgumentException($"{nameof(EpisodeNumber)} is required if {nameof(AnimeID)} is specified");
                var type = EpisodeType is not (EpisodeType)1 ? EpisodeType.Value.ToString()[..1] : "";
                command += $"aid={AnimeID.Value}&epno={type + EpisodeNumber.Value.ToString()}";
            }
            else
            {
                throw new ArgumentException($"{nameof(MylistID)}, {nameof(FileID)}, {nameof(ED2K)}, or {nameof(AnimeID)} must be specified");
            }

            return command;
        }
    }

    protected internal override UDPResponse<Void> ParseResponse(UDPResponse<string> response) => response.Code switch
    {
        UDPReturnCode.MYLIST_ENTRY_DELETED or
        UDPReturnCode.NO_SUCH_MYLIST_ENTRY or
        UDPReturnCode.NO_SUCH_FILE or
        UDPReturnCode.NO_SUCH_ANIME or
        UDPReturnCode.NO_SUCH_EPISODE => new UDPResponse<Void> { Code = response.Code },
        _ => throw new UnexpectedUDPResponseException(response.Code, response.Response, Command),
    };

    public RequestRemoveMylist(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
