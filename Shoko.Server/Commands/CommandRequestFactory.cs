using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Server.Commands.Exceptions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Server;

namespace Shoko.Server.Commands;

public class CommandRequestFactory : ICommandRequestFactory
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<CommandRequestFactory> _logger;

    public CommandRequestFactory(IServiceProvider provider, ILogger<CommandRequestFactory> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public T Create<T>(Action<T> ctor = null) where T : CommandRequest
    {
        var obj = ActivatorUtilities.CreateInstance<T>(_provider);
        ctor?.Invoke(obj);
        obj.PostInit();
        obj.GenerateCommandID();
        return obj;
    }

    public void CreateAndSave<T>(Action<T> ctor = null, bool force = false) where T : CommandRequest
    {
        var request = Create(ctor);
        Save(request, force);
    }

    public virtual void Save(CommandRequest request, bool force = false)
    {
        var commandID = request.CommandID + (force ? "_Forced" : "");
        var crTemp = RepoFactory.CommandRequest.GetByCommandID(commandID);
        if (crTemp != null)
        {
            switch (request.ConflictBehavior)
            {
                case CommandConflict.Replace:
                    RepoFactory.CommandRequest.Delete(crTemp);
                    break;
                case CommandConflict.Ignore: return;
                case CommandConflict.Error:
                default: throw new CommandExistsException { CommandID = commandID };
            }
        }

        request.DateTimeUpdated = DateTime.Now;
        request.GenerateCommandID();
        _logger.LogTrace("Saving new CommandRequest: {CommandType} {CommandID}", (CommandRequestType)request.CommandType, request.CommandID);
        try
        {
            RepoFactory.CommandRequest.Save(request);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to Save CommandRequest, retrying");
            try
            {
                RepoFactory.CommandRequest.Save(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Still Failed to Save CommandRequest");
            }
        }

        switch (CommandRequestRepository.GetQueueIndex(request))
        {
            case 0:
                ShokoService.CmdProcessorGeneral.NotifyOfNewCommand();
                break;
            case 1:
                ShokoService.CmdProcessorHasher.NotifyOfNewCommand();
                break;
            case 2:
                ShokoService.CmdProcessorImages.NotifyOfNewCommand();
                break;
        }
    }
}
