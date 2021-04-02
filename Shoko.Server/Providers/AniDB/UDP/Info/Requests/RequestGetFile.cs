using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shoko.Commons.Extensions;
using Shoko.Models.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic.Requests;
using Shoko.Server.Providers.AniDB.UDP.Generic.Responses;
using Shoko.Server.Providers.AniDB.UDP.Info.Responses;

namespace Shoko.Server.Providers.AniDB.UDP.Info.Requests
{
    /// <summary>
    /// Add a file to MyList. If it doesn't exist, it will return the MyListID for future updates.
    /// If it exists, it will return the current status on AniDB. 
    /// </summary>
    public class RequestGetFile : UDPBaseRequest<ResponseGetFile>
    {
        // These are dependent on context
        protected override string BaseCommand
        {
            get
            {
                StringBuilder commandText = new StringBuilder("FILE size=");
                commandText.Append(FileData.FileSize);
                commandText.Append("&ed2k=");
                commandText.Append(FileData.ED2KHash);
                commandText.Append($"&fmask={_fByte1}{_fByte2}{_fByte3}{_fByte4}{_fByte5}");
                commandText.Append($"&amask={_aByte1}{_aByte2}{_aByte3}{_aByte4}");
                return commandText.ToString();
            }
        }
        
        public IHash FileData { get; set; }

        // https://wiki.anidb.net/UDP_API_Definition#FILE:_Retrieve_File_Data
        // these are all bitmasks, so byte literals make it easier to see what the values mean
        private readonly string _fByte1 = PadByte(0b01111111); // fmask - byte1 xref info. We want all of this
        private readonly string _fByte2 = PadByte(0b00000000); // fmask - byte2 hashes and file info
        private readonly string _fByte3 = PadByte(0b11000000); // fmask - byte3 mediainfo, we get quality and source
        private readonly string _fByte4 = PadByte(0b11010001); // fmask - byte4 language and misc info
        private readonly string _fByte5 = PadByte(0b11110000); // fmask - byte5 mylist info
        private readonly string _aByte1 = PadByte(0b00000000); // amask - byte1 these are all anime info
        private readonly string _aByte2 = PadByte(0b00000000); // amask - byte2 ^^
        private readonly string _aByte3 = PadByte(0b00000000); // amask - byte3 ^^
        private readonly string _aByte4 = PadByte(0b00000000); // amask - byte4 ^^

        private static string PadByte(byte b) => b.ToString("X").PadLeft(2, '0');

        protected override UDPBaseResponse<ResponseGetFile> ParseResponse(AniDBUDPReturnCode code, string receivedData)
        {
            switch (code)
            {
                case AniDBUDPReturnCode.FILE:
                {
                    // The spaces here are added for readability. They aren't in the response
                    // fileid|anime|episode|group|MyListID |other eps|deprecated|state|quality|source|audio lang|sub lang|file description|filename                                                                                                    |mylist state|mylist filestate|viewcount|view date
                    //2442444|14360|225455 |8482 |291278112|         |    0     |4097 |  high |  www | japanese | english|                |Magia Record: Mahou Shoujo Madoka Magica Gaiden - 03 - Sorry for Making You My Friend - [Doki](a076b874).mkv|   3        |         0      |     1   |1584060577
                    // we don't want to remove empty parts or change the layout here. Some will be empty, but we want consistent indexing
                    string[] parts = receivedData.Split('|').Select(a => a.Trim()).ToArray();
                    if (parts.Length != 18) throw new UnexpectedAniDBResponseException(code, receivedData);
                    // parse out numbers into temp vars
                    if (!int.TryParse(parts[0], out int fid)) throw new UnexpectedAniDBResponseException(code, receivedData);
                    if (!int.TryParse(parts[1], out int aid)) throw new UnexpectedAniDBResponseException(code, receivedData);
                    if (!int.TryParse(parts[2], out int eid)) throw new UnexpectedAniDBResponseException(code, receivedData);
                    bool hasGroup = int.TryParse(parts[3], out int gid);
                    int? groupID = hasGroup ? gid : null;
                    // save mylist and partial episode mapping 'til later

                    // cheap but fast
                    bool deprecated = parts[6].Equals("1");
                    GetFile_State state = Enum.Parse<GetFile_State>(parts[7]);
                    int version = 1;
                    if (state.HasFlag(GetFile_State.FILE_ISV2)) version = 2;
                    if (state.HasFlag(GetFile_State.FILE_ISV3)) version = 3;
                    if (state.HasFlag(GetFile_State.FILE_ISV4)) version = 4;
                    if (state.HasFlag(GetFile_State.FILE_ISV5)) version = 5;

                    bool? censored = state.HasFlag(GetFile_State.FILE_UNC) ? false : state.HasFlag(GetFile_State.FILE_CEN) ? true : null;
                    bool? crc = state.HasFlag(GetFile_State.FILE_CRCOK) ? true : state.HasFlag(GetFile_State.FILE_CRCERR) ? false : null;
                    bool chaptered = state.HasFlag(GetFile_State.FILE_CHAPTERED);
                    var quality = ParseQuality(parts[8]);
                    var source = ParseSource(parts[9]);
                    var description = parts[12];
                    var filename = parts[13];
                    
                    // other xrefs
                    List<ResponseGetFile.PartialEpisodeXRef> xrefs;
                    if (!string.IsNullOrEmpty(parts[5]))
                    {
                        string[] xrefStrings = parts[5].Split('\'');
                        xrefs = xrefStrings.Batch(2).Select(
                            a =>
                            {
                                if (!int.TryParse(a[0], out int epid)) return null;
                                if (!byte.TryParse(a[1], out byte per)) return null;
                                return new ResponseGetFile.PartialEpisodeXRef {EpisodeID = epid, Percentage = per};
                            }
                        ).Where(a => a != null).ToList();
                    }
                    else
                    {
                        xrefs = new List<ResponseGetFile.PartialEpisodeXRef>();
                    }
                    
                    // audio languages
                    var alangs = parts[10].Split(new[] {'\''}, StringSplitOptions.RemoveEmptyEntries).ToList();
                    
                    // sub languages
                    var slangs = parts[11].Split(new[] {'\''}, StringSplitOptions.RemoveEmptyEntries).ToList();
                    
                    // mylist
                    var myList = ParseMyList(parts);

                    return new UDPBaseResponse<ResponseGetFile>()
                    {
                        Code = code,
                        Response = new ResponseGetFile()
                        {
                            FileID = fid,
                            AnimeID = aid,
                            EpisodeID = eid,
                            GroupID = groupID,
                            Deprecated = deprecated,
                            Version = version,
                            Censored = censored,
                            CRCMatches = crc,
                            Chaptered = chaptered,
                            Description = description,
                            Filename = filename,
                            Quality = quality,
                            Source = source,
                            OtherEpisodeIDs = xrefs,
                            AudioLanguages = alangs,
                            SubtitleLanguages = slangs,
                            MyList = myList
                        }
                    };
                }
                case AniDBUDPReturnCode.NO_SUCH_FILE:
                    return new UDPBaseResponse<ResponseGetFile>() {Code = code, Response = null};
            }
            throw new UnexpectedAniDBResponseException(code, receivedData);
        }

        private static GetFile_Quality ParseQuality(string qualityString)
        {
            switch (qualityString.Replace(" ", "").ToLower())
            {
                case "veryhigh":
                    return GetFile_Quality.VeryHigh;
                case "high":
                    return GetFile_Quality.High;
                case "med":
                case "medium":
                    return GetFile_Quality.Medium;
                case "low":
                    return GetFile_Quality.Low;
                case "verylow":
                    return GetFile_Quality.VeryLow;
                case "corrupted":
                    return GetFile_Quality.Corrupted;
                case "eyecancer":
                    return GetFile_Quality.EyeCancer;
                default:
                    return GetFile_Quality.Unknown;
            }
        }
        
        private static GetFile_Source ParseSource(string sourceString)
        {
            switch (sourceString.Replace("-", "").ToLower())
            {
                case "tv": return GetFile_Source.TV;
                case "www": return GetFile_Source.Web;
                case "dvd": return GetFile_Source.DVD;
                case "bluray": return GetFile_Source.BluRay;
                case "vhs": return GetFile_Source.VHS;
                case "hkdvd": return GetFile_Source.HDDVD;
                case "hddvd": return GetFile_Source.HKDVD;
                case "hdtv": return GetFile_Source.HDTV;
                case "dtv": return GetFile_Source.DTV;
                case "camcorder": return GetFile_Source.Camcorder;
                case "vcd": return GetFile_Source.VCD;
                case "svcd": return GetFile_Source.SVCD;
                case "ld": return GetFile_Source.LaserDisc;
                default: return GetFile_Source.Unknown;
            }
        }

        private static ResponseGetFile.MyListInfo ParseMyList(string[] parts)
        {
            if (!int.TryParse(parts[4], out int myListID)) return null;
            var mylistState = Enum.Parse<MyList_State>(parts[14]);
            var mylistFileState = Enum.Parse<MyList_FileState>(parts[15]);
            DateTime? viewDate = null;
            if (int.TryParse(parts[16], out int viewCount))
            {
                if (long.TryParse(parts[17], out long viewTime))
                {
                    // they store in seconds to save space
                    viewDate = DateTime.UnixEpoch.AddMilliseconds(viewTime * 1000L);
                }
            }
            else viewCount = 0;

            return new ResponseGetFile.MyListInfo()
            {
                MyListID = myListID,
                State = mylistState,
                FileState = mylistFileState,
                ViewCount = viewCount,
                ViewDate = viewDate
            };
        }
    }
}
