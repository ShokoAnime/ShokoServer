using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands.AniDB;

[Serializable]
[Command(CommandRequestType.AniDB_GetCalendar)]
public class CommandRequest_GetCalendar : CommandRequestImplementation
{
    private readonly IRequestFactory _requestFactory;
    private readonly ICommandRequestFactory _commandFactory;

    public bool ForceRefresh { get; set; }

    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority5;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Getting calendar info from UDP API",
        queueState = QueueStateEnum.GetCalendar,
        extraParams = new string[0]
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_GetCalendar");

        try
        {
            // we will always assume that an anime was downloaded via http first

            var sched =
                RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBCalendar);
            if (sched == null)
            {
                sched = new ScheduledUpdate
                {
                    UpdateType = (int)ScheduledUpdateType.AniDBCalendar, UpdateDetails = string.Empty
                };
            }
            else
            {
                var freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDb.Calendar_UpdateFrequency);

                // if we have run this in the last 12 hours and are not forcing it, then exit
                var tsLastRun = DateTime.Now - sched.LastUpdate;
                if (tsLastRun.TotalHours < freqHours)
                {
                    if (!ForceRefresh)
                    {
                        return;
                    }
                }
            }

            sched.LastUpdate = DateTime.Now;

            var request = _requestFactory.Create<RequestCalendar>();
            var response = request.Execute();
            RepoFactory.ScheduledUpdate.Save(sched);

            if (response.Response?.Next25Anime != null)
            {
                foreach (var cal in response.Response.Next25Anime)
                {
                    GetAnime(cal);
                }
            }

            if (response.Response?.Previous25Anime == null)
            {
                return;
            }

            foreach (var cal in response.Response.Previous25Anime)
            {
                GetAnime(cal);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing CommandRequest_GetCalendar: {Ex}", ex);
        }
    }

    private void GetAnime(ResponseCalendar.CalendarEntry cal)
    {
        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(cal.AnimeID);
        var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(cal.AnimeID);
        if (anime != null && update != null)
        {
            // don't update if the local data is less 2 days old
            var ts = DateTime.Now - update.UpdatedAt;
            if (ts.TotalDays >= 2)
            {
                var cmdAnime = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(
                    c =>
                    {
                        c.AnimeID = cal.AnimeID;
                        c.ForceRefresh = true;
                    }
                );
                cmdAnime.Save();
            }
            else
            {
                // update the release date even if we don't update the anime record
                if (anime.AirDate == cal.ReleaseDate)
                {
                    return;
                }

                anime.AirDate = cal.ReleaseDate;
                RepoFactory.AniDB_Anime.Save(anime);
                var ser = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
                if (ser != null)
                {
                    RepoFactory.AnimeSeries.Save(ser, true, false);
                }
            }
        }
        else
        {
            var cmdAnime = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(
                c =>
                {
                    c.AnimeID = cal.AnimeID;
                    c.ForceRefresh = true;
                }
            );
            cmdAnime.Save();
        }
    }

    public override void GenerateCommandID()
    {
        CommandID = "CommandRequest_GetCalendar";
    }

    public override bool LoadFromDBCommand(CommandRequest cq)
    {
        CommandID = cq.CommandID;
        CommandRequestID = cq.CommandRequestID;
        Priority = cq.Priority;
        CommandDetails = cq.CommandDetails;
        DateTimeUpdated = cq.DateTimeUpdated;

        // read xml to get parameters
        if (CommandDetails.Trim().Length <= 0) return false;

        var docCreator = new XmlDocument();
        docCreator.LoadXml(CommandDetails);

        // populate the fields
        ForceRefresh = bool.Parse(
            TryGetProperty(docCreator, "CommandRequest_GetCalendar", "ForceRefresh"));

        return true;
    }

    public override CommandRequest ToDatabaseObject()
    {
        GenerateCommandID();

        var cq = new CommandRequest
        {
            CommandID = CommandID,
            CommandType = CommandType,
            Priority = Priority,
            CommandDetails = ToXML(),
            DateTimeUpdated = DateTime.Now
        };
        return cq;
    }

    public CommandRequest_GetCalendar(ILoggerFactory loggerFactory, IRequestFactory requestFactory,
        ICommandRequestFactory commandFactory) : base(loggerFactory)
    {
        _requestFactory = requestFactory;
        _commandFactory = commandFactory;
    }

    protected CommandRequest_GetCalendar()
    {
    }
}
