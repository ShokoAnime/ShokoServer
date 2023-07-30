// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Quartz;
using QuartzJobFactory.Attributes;
using Shoko.Server.Commands;
using Shoko.Server.Repositories;

namespace Shoko.Server.Scheduling.Jobs;

[JobKeyMember("MediaInfo")]
[JobKeyGroup("Legacy")]
internal class MediaInfoJob : IJob
{
    private readonly ICommandRequestFactory _commandRequestFactory;

    public MediaInfoJob(ICommandRequestFactory commandRequestFactory)
    {
        _commandRequestFactory = commandRequestFactory;
    }
    
    protected MediaInfoJob() { }
    
    public Task Execute(IJobExecutionContext context)
    {
        try
        {
            // first build a list of files that we already know about, as we don't want to process them again
            var filesAll = RepoFactory.VideoLocal.GetAll();
            foreach (var vl in filesAll)
            {
                var cr = _commandRequestFactory.Create<CommandRequest_ReadMediaInfo>(c => c.VideoLocalID = vl.VideoLocalID);
                cr.Save();
            }
        }
        catch (Exception ex)
        {
            // TODO: Logging
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }

        return Task.CompletedTask;
    }
}
