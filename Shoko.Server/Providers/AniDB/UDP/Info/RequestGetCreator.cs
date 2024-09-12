using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Providers.AniDB.UDP.Generic;

#nullable enable
namespace Shoko.Server.Providers.AniDB.UDP.Info;

/// <summary>
/// Get File Info. Getting the file info will only return any data if the hashes match
/// If there is MyList info, it will also return that
/// </summary>
public class RequestGetCreator : UDPRequest<ResponseGetCreator?>
{
    // These are dependent on context
    protected override string BaseCommand => $"CREATOR creatorid={CreatorID}";

    public int CreatorID { get; set; }

    protected override UDPResponse<ResponseGetCreator?> ParseResponse(UDPResponse<string> response)
    {
        var code = response.Code;
        var receivedData = response.Response;
        switch (code)
        {
            case UDPReturnCode.CREATOR:
            {
                // {int creatorid}|{str creator name kanji}|{str creator name transcription}|{int type}|{str pic_name}|{str url_english}|{str url_japanese}|{str wiki_url_english}|{str wiki_url_japanese}|{int last update date}
                var parts = receivedData.Split('|').Select(a => a.Trim()).ToArray();
                if (parts.Length < 10)
                {
                    throw new UnexpectedUDPResponseException("There were the wrong number of data columns", code,
                        receivedData, Command);
                }

                if (!int.TryParse(parts[0], out var creatorID))
                {
                    throw new UnexpectedUDPResponseException("Creator ID was not an int", code, receivedData, Command);
                }

                if (!int.TryParse(parts[3], out var creatorType))
                {
                    throw new UnexpectedUDPResponseException("Creator type was not an int", code, receivedData, Command);
                }

                if (!int.TryParse(parts[9], out var lastUpdated))
                {
                    throw new UnexpectedUDPResponseException("Last updated date was not an int", code, receivedData, Command);
                }

                var name = string.IsNullOrEmpty(parts[1]) ? null : parts[1]?.Replace("`", "'");
                var transcribedName = parts[2].Replace("`", "'");
                var picName = string.IsNullOrEmpty(parts[4]) ? null : parts[4];
                var urlEnglish = string.IsNullOrEmpty(parts[5]) ? null : parts[5];
                var urlJapanese = string.IsNullOrEmpty(parts[6]) ? null : parts[6];
                var wikiUrlEnglish = string.IsNullOrEmpty(parts[7]) ? null : parts[7];
                var wikiUrlJapanese = string.IsNullOrEmpty(parts[8]) ? null : parts[8];
                var lastUpdatedAt = DateTime.UnixEpoch.AddSeconds(lastUpdated).ToLocalTime();
                return new UDPResponse<ResponseGetCreator?>
                {
                    Code = code,
                    Response = new ResponseGetCreator
                    {
                        ID = creatorID,
                        Name = transcribedName,
                        OriginalName = name,
                        Type = (CreatorType)creatorType,
                        ImagePath = picName,
                        EnglishHomepageUrl = urlEnglish,
                        JapaneseHomepageUrl = urlJapanese,
                        EnglishWikiUrl = wikiUrlEnglish,
                        JapaneseWikiUrl = wikiUrlJapanese,
                        LastUpdateAt = lastUpdatedAt,
                    },
                };
            }
            case UDPReturnCode.NO_SUCH_CREATOR:
                return new UDPResponse<ResponseGetCreator?> { Code = code, Response = null };
        }

        throw new UnexpectedUDPResponseException(code, receivedData, Command);
    }

    public RequestGetCreator(ILoggerFactory loggerFactory, IUDPConnectionHandler handler) : base(loggerFactory, handler)
    {
    }
}
