using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Generic;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands.AniDB;

[Serializable]
[Command(CommandRequestType.AniDB_SyncVotes)]
public class CommandRequest_SyncMyVotes : CommandRequestImplementation
{
    private readonly IRequestFactory _requestFactory;
    private readonly ICommandRequestFactory _commandFactory;
    private readonly ISettingsProvider _settingsProvider;
    public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority7;

    public override QueueStateStruct PrettyDescription => new()
    {
        message = "Upload Local Votes To AniDB",
        queueState = QueueStateEnum.Actions_SyncVotes,
        extraParams = Array.Empty<string>()
    };

    protected override void Process()
    {
        Logger.LogInformation("Processing CommandRequest_SyncMyVotes");

        try
        {
            var settings = _settingsProvider.GetSettings();
            var request = _requestFactory.Create<RequestVotes>(
                r =>
                {
                    r.Username = settings.AniDb.Username;
                    r.Password = settings.AniDb.Password;
                }
            );
            var response = request.Execute();
            if (response.Response == null)
            {
                return;
            }

            foreach (var myVote in response.Response)
            {
                var dbVotes = RepoFactory.AniDB_Vote.GetByEntity(myVote.EntityID);
                AniDB_Vote thisVote = null;
                foreach (var dbVote in dbVotes)
                {
                    // we can only have anime permanent or anime temp but not both
                    if (myVote.VoteType is AniDBVoteType.Anime or AniDBVoteType.AnimeTemp)
                    {
                        if (dbVote.VoteType is (int)AniDBVoteType.Anime or (int)AniDBVoteType.AnimeTemp)
                        {
                            thisVote = dbVote;
                        }
                    }
                    else
                    {
                        thisVote = dbVote;
                    }
                }

                if (thisVote == null)
                {
                    thisVote = new AniDB_Vote { EntityID = myVote.EntityID };
                }

                thisVote.VoteType = (int)myVote.VoteType;
                thisVote.VoteValue = (int)(myVote.VoteValue * 100);

                RepoFactory.AniDB_Vote.Save(thisVote);

                if (myVote.VoteType is not (AniDBVoteType.Anime or AniDBVoteType.AnimeTemp))
                {
                    continue;
                }

                // download the anime info if the user doesn't already have it
                var cmdAnime = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(c =>
                {
                    c.AnimeID = thisVote.EntityID;
                    c.CreateSeriesEntry = settings.AniDb.AutomaticallyImportSeries;
                });
                cmdAnime.Save();
            }

            Logger.LogInformation("Processed Votes: {Count} Items", response.Response.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing CommandRequest_SyncMyVotes");
        }
    }

    public override void GenerateCommandID()
    {
        CommandID = "CommandRequest_SyncMyVotes";
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

    public CommandRequest_SyncMyVotes(ILoggerFactory loggerFactory, IRequestFactory requestFactory,
        ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider) : base(loggerFactory)
    {
        _requestFactory = requestFactory;
        _commandFactory = commandFactory;
        _settingsProvider = settingsProvider;
    }

    protected CommandRequest_SyncMyVotes()
    {
    }
}
