using System;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

#nullable enable
namespace Shoko.Server.Providers.AniDB.UDP.User;

public class RequestAddMylist : UDPRequest<MylistEntry>
{
    #region MyList Entry Identification

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

    public MylistState? State { get; set; }

    public MylistFileState? FileState { get; set; }

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
            if (FileID is > 0)
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
                throw new ArgumentException($"{nameof(FileID)}, {nameof(ED2K)}, or {nameof(AnimeID)} must be specified");
            }

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

    protected internal override UDPResponse<MylistEntry> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        var receivedData = response.Response;
        switch (code)
        {
            case UDPReturnCode.MYLIST_ENTRY_ADDED:
            {
                /* Response Format
                 * when identified by fid or ed2k+size: {int4 mylist id of new entry}
                 * when identified by aid: {int4 number of entries added}
                 */
                // a generic add covers a range of files, so AniDB returns how many
                // entries it created rather than a list ID we could use later
                var identifiedByFile = FileID is > 0 || !string.IsNullOrEmpty(ED2K);
                var lid = identifiedByFile && ulong.TryParse(receivedData, out var parsedLid) ? parsedLid : 0;
                return new UDPResponse<MylistEntry>
                {
                    Code = code,
                    Response = new MylistEntry
                    {
                        MylistID = lid,
                        FileID = (int)(FileID ?? 0),
                        AnimeID = AnimeID ?? 0,
                        ED2K = ED2K,
                        Size = Size,
                        FileState = FileState ?? MylistFileState.Normal,
                        State = State ?? MylistState.Unknown,
                        IsViewed = _isViewed ?? false,
                        ViewedAt = _viewedAt,
                        UpdatedAt = DateOnly.FromDateTime(DateTime.Today),
                        Other = Other ?? string.Empty,
                        Source = Source ?? string.Empty,
                        Storage = Storage ?? string.Empty,
                    }
                };
            }
            case UDPReturnCode.FILE_ALREADY_IN_MYLIST:
            {
                /* Response Format
                 * {int4 lid}|{int4 fid}|{int4 eid}|{int4 aid}|{int4 gid}|{int4 date}|{int2 state}|{int4 viewdate}|{str storage}|{str source}|{str other}|{int2 filestate}
                 */
                //file already exists: read the existing entry
                var arrStatus = receivedData.Split('|');
                ulong.TryParse(arrStatus[0], out var mylistID);
                int.TryParse(arrStatus[1], out var fileID);
                int.TryParse(arrStatus[2], out var episodeID);
                int.TryParse(arrStatus[3], out var animeID);

                var state = (MylistState)int.Parse(arrStatus[6]);
                var fileState = arrStatus.Length > 11 && int.TryParse(arrStatus[11], out var fileStateValue)
                    ? (MylistFileState)fileStateValue
                    : MylistFileState.Normal;

                var viewdate = ulong.Parse(arrStatus[7]);
                var updatedate = ulong.Parse(arrStatus[5]);
                var watched = viewdate > 0;
                var updatedAt = DateOnly.MinValue;
                DateTime? watchedDate = null;
                if (updatedate > 0)
                {
                    // AniDB reports this in UTC, and the HTTP export's `updated`
                    // is taken as-is, so shifting to local here would put the two
                    // sources a day apart either side of midnight
                    updatedAt = DateTime.UnixEpoch
                        .AddSeconds(updatedate)
                        .ToDateOnly();
                }

                if (watched)
                {
                    // kept in UTC to match what the request side sends
                    watchedDate = DateTime.UnixEpoch.AddSeconds(viewdate);
                }

                return new UDPResponse<MylistEntry>
                {
                    Code = code,
                    Response = new MylistEntry
                    {
                        MylistID = mylistID,
                        FileID = fileID,
                        EpisodeID = episodeID,
                        AnimeID = animeID,
                        ED2K = ED2K,
                        Size = Size,
                        State = state,
                        FileState = fileState,
                        IsViewed = watched,
                        ViewedAt = watchedDate,
                        UpdatedAt = updatedAt,
                        Storage = arrStatus.Length > 8 ? arrStatus[8] : string.Empty,
                        Source = arrStatus.Length > 9 ? arrStatus[9] : string.Empty,
                        Other = arrStatus.Length > 10 ? arrStatus[10] : string.Empty,
                    }
                };
            }
            case UDPReturnCode.NO_SUCH_FILE:
            case UDPReturnCode.NO_SUCH_EPISODE:
                return new UDPResponse<MylistEntry> { Code = code };
        }

        throw new UnexpectedUDPResponseException(code, receivedData, Command);
    }

    public RequestAddMylist(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
