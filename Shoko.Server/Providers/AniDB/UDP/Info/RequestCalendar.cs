using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Server.Providers.AniDB.UDP.Generic;

namespace Shoko.Server.Providers.AniDB.UDP.Info
{
    public class RequestCalendar : UDPRequest<ResponseCalendar>
    {

        protected override string BaseCommand => "CALENDAR";

        protected override UDPResponse<ResponseCalendar> ParseResponse(ILogger logger, UDPResponse<string> response)
        {
            var code = response.Code;
            var receivedData = response.Response;
            if (code == UDPReturnCode.CALENDAR_EMPTY)
                return new UDPResponse<ResponseCalendar> {Response = null, Code = code};

            var calendar = new ResponseCalendar
            {
                Next25Anime = new List<ResponseCalendar.CalendarEntry>(),
                Previous25Anime = new List<ResponseCalendar.CalendarEntry>()
            };

            foreach (var parts in receivedData.Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split('\n')))
            {
                if (parts.Length != 3) continue;
                if (!int.TryParse(parts[0], out int animeID)) continue;
                if (!int.TryParse(parts[1], out int epochElapsed)) continue;
                if (!int.TryParse(parts[2], out int flagInt)) continue;
                var flags = (ResponseCalendar.CalendarFlags) flagInt;
                var date = Commons.Utils.AniDB.GetAniDBDateAsDate(epochElapsed);
                var entry = new ResponseCalendar.CalendarEntry
                {
                    AnimeID = animeID,
                    ReleaseDate = date,
                    DateFlags = flags
                };
                bool known = !flags.HasFlag(ResponseCalendar.CalendarFlags.StartUnknown) &&
                            !flags.HasFlag(ResponseCalendar.CalendarFlags.StartDayUnknown) &&
                            !flags.HasFlag(ResponseCalendar.CalendarFlags.StartMonthDayUnknown);
                if (known && date.HasValue && date.Value < DateTime.UtcNow.Date) calendar.Previous25Anime.Add(entry);
                else calendar.Next25Anime.Add(entry);
            }

            return new UDPResponse<ResponseCalendar>
            {
                Response = calendar, Code = code
            };
        }
    }
}
