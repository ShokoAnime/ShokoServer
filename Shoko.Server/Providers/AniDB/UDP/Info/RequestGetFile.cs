using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shoko.Server.Extensions;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Info;

/// <summary>
/// Get File Info. Getting the file info will only return any data if the hashes match
/// If there is MyList info, it will also return that
/// </summary>
public class RequestGetFile : UDPRequest<ResponseGetFile>
{
    // These are dependent on context
    protected override string BaseCommand
    {
        get
        {
            var commandText = new StringBuilder("FILE size=");
            commandText.Append(Size);
            commandText.Append("&ed2k=");
            commandText.Append(Hash);
            commandText.Append($"&fmask={_fByte1}{_fByte2}{_fByte3}{_fByte4}{_fByte5}");
            commandText.Append($"&amask={_aByte1}{_aByte2}{_aByte3}{_aByte4}");
            return commandText.ToString();
        }
    }

    public string Hash { get; set; }
    public long Size { get; set; }

    // https://wiki.anidb.net/UDP_API_Definition#FILE:_Retrieve_File_Data
    // these are all bitmasks, so byte literals make it easier to see what the values mean
    private readonly string _fByte1 = PadByte(0b01110111); // fmask - byte1 xref info. We want all of this
    private readonly string _fByte2 = PadByte(0b00000000); // fmask - byte2 hashes and file info
    private readonly string _fByte3 = PadByte(0b11000000); // fmask - byte3 mediainfo, we get quality and source
    private readonly string _fByte4 = PadByte(0b11011001); // fmask - byte4 language and misc info
    private readonly string _fByte5 = PadByte(0b00000000); // fmask - byte5 mylist info
    private readonly string _aByte1 = PadByte(0b00000000); // amask - byte1 these are all anime info
    private readonly string _aByte2 = PadByte(0b00000000); // amask - byte2 ^^
    private readonly string _aByte3 = PadByte(0b00000000); // amask - byte3 ^^
    private readonly string _aByte4 = PadByte(0b11000000); // amask - byte4 group name and short name

    private static string PadByte(byte b)
    {
        return b.ToString("X").PadLeft(2, '0');
    }

    private static readonly Regex s_episodeFormat1 = new("^(\\d+'\\d+)+$", RegexOptions.Compiled);
    private static readonly Regex s_episodeFormat2 = new("^(\\d+,\\d+'?)+$", RegexOptions.Compiled);

    protected override UDPResponse<ResponseGetFile> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        var receivedData = response.Response;
        switch (code)
        {
            case UDPReturnCode.FILE:
            {
                // NOTE: Redo this overview if the fmask or amask above changes again.
                // |fileid|anime|episode|group|other eps|deprecated|state|quality|source|audio lang|sub lang|file desc|air date|filename|group name|group name short|
                //     00 |  01 |    02 |  03 |      04 |       05 |  06 |    07 |   08 |       09 |     10 |      11 |     12 |     13 |       14 |             15 |
                // we don't want to remove empty parts or change the layout here. Some will be empty, but we want consistent indexing
                var parts = receivedData.Split('|').Select(a => a.Trim()).ToArray();
                if (parts.Length != 16)
                {
                    throw new UnexpectedUDPResponseException("There were the wrong number of data columns", code, receivedData, Command);
                }

                // parse out numbers into temp vars
                if (!int.TryParse(parts[00], out var fid))
                {
                    throw new UnexpectedUDPResponseException("File ID was not an int", code, receivedData, Command);
                }

                if (!int.TryParse(parts[01], out var aid))
                {
                    throw new UnexpectedUDPResponseException("Anime ID was not an int", code, receivedData, Command);
                }

                // It can be possible that a file is added with an unknown group, though I've never seen it before
                var hasGroup = int.TryParse(parts[03], out var gid) && gid != 0;
                int? groupID = hasGroup ? gid : null;
                // save mylist and partial episode mapping 'til later

                // cheap but fast
                var deprecated = parts[05].Equals("1");
                if (!Enum.TryParse<GetFile_State>(parts[07], out var state)) state = GetFile_State.None;
                var version = 1;
                if (state.HasFlag(GetFile_State.IsV2))
                    version = 2;
                if (state.HasFlag(GetFile_State.IsV3))
                    version = 3;
                if (state.HasFlag(GetFile_State.IsV4))
                    version = 4;
                if (state.HasFlag(GetFile_State.IsV5))
                    version = 5;

                bool? censored = state.HasFlag(GetFile_State.Uncensored) ? false :
                    state.HasFlag(GetFile_State.Censored) ? true : null;
                bool? crc = state.HasFlag(GetFile_State.CRCMatch) ? true :
                    state.HasFlag(GetFile_State.CRCErr) ? false : null;
                var chaptered = state.HasFlag(GetFile_State.Chaptered);
                var quality = ParseQuality(parts[08]);
                var source = ParseSource(parts[09]);
                var description = parts[11];
                var filename = parts[13];
                var airDate = int.TryParse(parts[12], out var rawReleaseDate) && rawReleaseDate > 0
                    ? DateOnly.FromDateTime(DateTime.UnixEpoch.AddSeconds(rawReleaseDate))
                    : (DateOnly?)null;
                var groupName = parts[14];
                var groupShortName = parts[15];

                // episode xrefs
                var xrefs = new List<ResponseGetFile.EpisodeXRef>();
                // if it's a number, it's not more than one
                if (int.TryParse(parts[2], out var eid))
                {
                    xrefs.Add(new ResponseGetFile.EpisodeXRef { EpisodeID = eid, Percentage = 100 });
                }
                // try to parse multiple
                else
                {
                    var eps = parts[02].Split('\'');
                    foreach (var ep in eps)
                    {
                        var percent = (byte)Math.Round(100D / eps.Length);
                        if (!int.TryParse(ep.Trim(), out var epid))
                        {
                            var epParts = ep.Trim().Split(',');
                            if (epParts.Length < 2)
                            {
                                continue;
                            }

                            if (!int.TryParse(epParts[0].Trim(), out epid))
                            {
                                continue;
                            }

                            percent = byte.Parse(epParts[1]);
                        }

                        xrefs.Add(new ResponseGetFile.EpisodeXRef { EpisodeID = epid, Percentage = percent });
                    }
                }

                var rawOtherEps = parts[04];
                var otherXrefs = new List<ResponseGetFile.EpisodeXRef>();
                if (!string.IsNullOrEmpty(rawOtherEps))
                {
                    // check the format.
                    // 1: number'percent'number'percent
                    // 2: number,percent'number,percent

                    if (s_episodeFormat1.IsMatch(rawOtherEps))
                    {
                        var xrefStrings = rawOtherEps.Split('\'');
                        var tempXrefs = xrefStrings.Batch(2).Select(
                            a =>
                            {
                                if (!int.TryParse(a[0], out var epid))
                                {
                                    return null;
                                }

                                if (!byte.TryParse(a[1], out var per))
                                {
                                    return null;
                                }

                                return new ResponseGetFile.EpisodeXRef { EpisodeID = epid, Percentage = per };
                            }
                        ).Where(a => a != null).ToArray();
                        if (tempXrefs.Length > 0)
                        {
                            otherXrefs.AddRange(tempXrefs);
                        }
                    }
                    else if (s_episodeFormat2.IsMatch(rawOtherEps))
                    {
                        var xrefStrings = rawOtherEps.Split('\'');
                        var tempXrefs = xrefStrings.Select(
                            a =>
                            {
                                var aParts = a.Split(',');
                                if (!int.TryParse(aParts[0], out var epid))
                                {
                                    return null;
                                }

                                if (!byte.TryParse(aParts[1], out var per))
                                {
                                    return null;
                                }

                                return new ResponseGetFile.EpisodeXRef { EpisodeID = epid, Percentage = per };
                            }
                        ).Where(a => a != null).ToArray();
                        if (tempXrefs.Length > 0)
                        {
                            otherXrefs.AddRange(tempXrefs);
                        }
                    }
                    else
                        Logger.LogError("Found an Other Episodes format that was not handled: {Format}", rawOtherEps);
                }

                // audio languages
                var alangs = parts[09].Split('\'', StringSplitOptions.RemoveEmptyEntries).Where(lang => lang != "none").ToList();

                // sub languages
                var slangs = parts[10].Split('\'', StringSplitOptions.RemoveEmptyEntries).Where(lang => lang != "none").ToList();

                return new UDPResponse<ResponseGetFile>
                {
                    Code = code,
                    Response = new ResponseGetFile
                    {
                        FileID = fid,
                        AnimeID = aid,
                        GroupID = groupID,
                        GroupName = groupName,
                        GroupShortName = groupShortName,
                        Deprecated = deprecated,
                        Version = version,
                        Censored = censored,
                        CRCMatches = crc,
                        Chaptered = chaptered,
                        Description = description,
                        Filename = filename,
                        Quality = quality,
                        Source = source,
                        EpisodeIDs = xrefs,
                        OtherEpisodes = otherXrefs,
                        AudioLanguages = alangs,
                        SubtitleLanguages = slangs,
                        ReleasedAt = airDate,
                    }
                };
            }
            case UDPReturnCode.NO_SUCH_FILE:
                return new UDPResponse<ResponseGetFile> { Code = code, Response = null };
        }

        throw new UnexpectedUDPResponseException(code, receivedData, Command);
    }

    private static GetFile_Quality ParseQuality(string qualityString)
    {
        return qualityString.Replace(" ", "").ToLower() switch
        {
            "veryhigh" => GetFile_Quality.VeryHigh,
            "high" => GetFile_Quality.High,
            "med" => GetFile_Quality.Medium,
            "medium" => GetFile_Quality.Medium,
            "low" => GetFile_Quality.Low,
            "verylow" => GetFile_Quality.VeryLow,
            "corrupted" => GetFile_Quality.Corrupted,
            "eyecancer" => GetFile_Quality.EyeCancer,
            _ => GetFile_Quality.Unknown
        };
    }

    private static GetFile_Source ParseSource(string sourceString)
    {
        return sourceString.Replace("-", "").ToLower() switch
        {
            "tv" => GetFile_Source.TV,
            "www" => GetFile_Source.Web,
            "dvd" => GetFile_Source.DVD,
            "bluray" => GetFile_Source.BluRay,
            "vhs" => GetFile_Source.VHS,
            "hkdvd" => GetFile_Source.HKDVD,
            "hddvd" => GetFile_Source.HDDVD,
            "hdtv" => GetFile_Source.HDTV,
            "dtv" => GetFile_Source.DTV,
            "camcorder" => GetFile_Source.Camcorder,
            "vcd" => GetFile_Source.VCD,
            "svcd" => GetFile_Source.SVCD,
            "ld" => GetFile_Source.LaserDisc,
            _ => GetFile_Source.Unknown
        };
    }

    public RequestGetFile(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
