using System;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

#nullable enable
namespace Shoko.Server.Providers.AniDB.UDP.User;

/// <summary>
/// Update an entry in the User's AniDB MyList.
/// </summary>
public class RequestUpdateMyList : UDPRequest<Void>
{
    #region MyList Entry Identification

    #region By List ID (lid)

    /// <summary>
    /// The ID of the AniDB MyList Entry (lid).
    /// </summary>
    public ulong? MyListID { get; set; }

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

    #region Update Fields

    public MyListState? State { get; set; }

    public MyListFileState? FileState { get; set; }

    private bool? _isViewed;

    private DateTime? _viewedAt;

    public bool? IsViewed
    {
        get => _isViewed;
        set
        {
            _isViewed = value;
            _viewedAt = value == true ? _viewedAt ?? AniDBExtensions.TruncateToAniDBPrecision(DateTime.UtcNow) : null;
        }
    }

    public DateTime? ViewedAt
    {
        get => _viewedAt;
        set
        {
            // the wire carries whole seconds, so hold exactly what will be sent
            _viewedAt = AniDBExtensions.TruncateToAniDBPrecision(value);
            _isViewed = value is not null;
        }
    }

    public string? Storage { get; set; }

    public string? Source { get; set; }

    public string? Other { get; set; }

    #endregion

    protected override string BaseCommand
    {
        get
        {
            var command = $"MYLISTADD ";
            if (MyListID is > 0)
            {
                command += $"lid={MyListID.Value}";
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
                command += $"aid={AnimeID.Value}&epno={type + EpisodeNumber.Value.ToString()}&generic=1";
            }
            else
            {
                throw new ArgumentException($"{nameof(MyListID)}, {nameof(FileID)}, {nameof(ED2K)}, or {nameof(AnimeID)} must be specified");
            }

            command += "&edit=1";

            if (State.HasValue)
                command += $"&state={(int)State.Value}";

            if (FileState.HasValue)
                command += $"&filestate={(int)FileState.Value}";

            if (IsViewed is true)
                command += $"&viewed=1&viewdate={AniDBExtensions.GetAniDBDateAsSeconds(ViewedAt ?? DateTime.Now)}";
            else if (IsViewed is false)
                command += "&viewed=0";

            if (Storage is not null)
                command += $"&storage={Storage}";

            if (Source is not null)
                command += $"&source={Source}";

            if (Other is not null)
                command += $"&other={Other}";
            return command;
        }
    }

    protected internal override UDPResponse<Void> ParseResponse(UDPResponse<string> response) => response.Code switch
    {
        UDPReturnCode.NO_SUCH_FILE or
        UDPReturnCode.NO_SUCH_ANIME or
        UDPReturnCode.NO_SUCH_GROUP or
        UDPReturnCode.NO_SUCH_MYLIST_ENTRY or
        UDPReturnCode.MYLIST_ENTRY_EDITED => new UDPResponse<Void> { Code = response.Code },
        _ => throw new UnexpectedUDPResponseException(response.Code, response.Response, Command),
    };

    public RequestUpdateMyList(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
