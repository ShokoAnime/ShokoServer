using System;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Microsoft.Extensions.Logging;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

#nullable enable
namespace Shoko.Server.Providers.AniDB.UDP.User;

/// <summary>
/// Retrieve an entry from the User's AniDB MyList.
/// </summary>
public class RequestGetMylist : UDPRequest<MylistEntry>
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

    #region By Episode ID (eid)

    /// <summary>
    /// The ID of the AniDB Episode (eid). Undocumented but supported by the
    /// live API.
    /// </summary>
    public int? EpisodeID { get; set; }

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
            var command = "MYLIST ";
            if (MylistID is > 0)
            {
                command += $"lid={MylistID.Value}";
            }
            else if (FileID is > 0)
            {
                command += $"fid={FileID.Value}";
            }
            else if (EpisodeID is > 0)
            {
                command += $"eid={EpisodeID.Value}";
            }
            else if (!string.IsNullOrEmpty(ED2K))
            {
                if (Size is not > 0)
                    throw new ArgumentException($"{nameof(Size)} is required if {nameof(ED2K)} is specified");
                command += $"size={Size.Value}&ed2k={ED2K}";
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
                throw new ArgumentException($"{nameof(MylistID)}, {nameof(FileID)}, {nameof(EpisodeID)}, {nameof(ED2K)}, or {nameof(AnimeID)} must be specified");
            }

            return command;
        }
    }

    protected internal override UDPResponse<MylistEntry> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        var receivedData = response.Response;
        switch (code)
        {
            case UDPReturnCode.MYLIST:
            {
                /* Response Format
                 * {int4 lid}|{int4 fid}|{int4 eid}|{int4 aid}|{int4 gid}|{int4 date}|{int2 state}|{int4 viewdate}|{str storage}|{str source}|{str other}|{int2 filestate}
                 */
                var parts = receivedData.Split('|');
                ulong.TryParse(parts[0], out var mylistID);
                int.TryParse(parts[1], out var fileID);
                int.TryParse(parts[2], out var episodeID);
                int.TryParse(parts[3], out var animeID);
                var state = (MylistState)int.Parse(parts[6]);
                var fileState = parts.Length > 11 ? (MylistFileState)int.Parse(parts[11]) : MylistFileState.Normal;

                var updatedAtSeconds = ulong.Parse(parts[5]);
                var viewdateSeconds = ulong.Parse(parts[7]);
                var isViewed = viewdateSeconds > 0;
                var updatedAt = DateOnly.MinValue;
                DateTime? viewedAt = null;
                if (updatedAtSeconds > 0)
                {
                    // AniDB reports this in UTC, and the HTTP export's `updated`
                    // is taken as-is, so shifting to local here would put the two
                    // sources a day apart either side of midnight
                    updatedAt = DateTime.UnixEpoch
                        .AddSeconds(updatedAtSeconds)
                        .ToDateOnly();
                }

                if (isViewed)
                {
                    // kept in UTC to match what the request side sends
                    viewedAt = DateTime.UnixEpoch.AddSeconds(viewdateSeconds);
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
                        State = state,
                        FileState = fileState,
                        IsViewed = isViewed,
                        ViewedAt = viewedAt,
                        UpdatedAt = updatedAt,
                        Storage = parts.Length > 8 ? parts[8] : string.Empty,
                        Source = parts.Length > 9 ? parts[9] : string.Empty,
                        Other = parts.Length > 10 ? parts[10] : string.Empty,
                    }
                };
            }
            case UDPReturnCode.NO_SUCH_ENTRY:
            case UDPReturnCode.NO_SUCH_FILE:
            case UDPReturnCode.NO_SUCH_EPISODE:
                return new UDPResponse<MylistEntry> { Code = code };
        }

        throw new UnexpectedUDPResponseException(code, receivedData, Command);
    }

    public RequestGetMylist(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
