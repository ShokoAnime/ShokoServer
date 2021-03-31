using System;
using System.Text;
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
                StringBuilder commandText = new StringBuilder("FILE size=" + FileData.FileSize);
                commandText.Append("&ed2k=" + FileData.ED2KHash);
                commandText.Append($"&fmask={_fByte1}{_fByte2}{_fByte3}{_fByte4}{_fByte5}");
                commandText.Append($"&amask={_aByte1}{_aByte2}{_aByte3}{_aByte4}");
                return commandText.ToString();
            }
        }
        
        public IHash FileData { get; set; }

        private readonly string _fByte1 = PadByte(127); // xref info. We want all of this
        // used to be 248. Trying 2 to allow for more space for other info.
        // We can calculate hashes ourselves, and if we got a response, then the files match
        private readonly string _fByte2 = PadByte(2); // hash and video color depth
        // used to be 255
        // trying for less info, as we can calculate mediainfo ourselves.
        // We may want to allow pulling this down for rare cases, like avdumping missing info
        // 192 is source and quality, which we can't calculate ourselves
        private readonly string _fByte3 = PadByte(192); // fmask - mediainfo
        // used to be 249. We are omitting redundant info like air date now
        private readonly string _fByte4 = PadByte(209); // fmask - language and misc info
        private readonly string _fByte5 = PadByte(254); // fmask - mylist info

        private readonly string _aByte1 = PadByte(0); // amask - byte1
        private readonly string _aByte2 = PadByte(0); // amask - byte2
        // used to be 252. We can get this from the http anime xml
        private readonly string _aByte3 = PadByte(0); // amask - byte3 old 236 Added Kanji name
        // used to be 192. We can get this from Group Info
        private readonly string _aByte4 = PadByte(0); // amask - byte4

        private static string PadByte(byte b) => b.ToString("X").PadLeft(2, '0');

        protected override UDPBaseResponse<ResponseGetFile> ParseResponse(AniDBUDPReturnCode code, string receivedData)
        {
            switch (code)
            {
                case AniDBUDPReturnCode.FILE:
                {
                    // TODO pull this down and parse it
                    break;
                }
                case AniDBUDPReturnCode.NO_SUCH_FILE:
                    return new UDPBaseResponse<ResponseGetFile>() {Code = code, Response = null};
            }
            throw new UnexpectedAniDBResponseException(code, receivedData);
        }
    }
}
