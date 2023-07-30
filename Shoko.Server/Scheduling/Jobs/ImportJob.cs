// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Quartz;
using QuartzJobFactory.Attributes;

namespace Shoko.Server.Scheduling.Jobs;

[JobKeyMember("Import")]
[JobKeyGroup("Legacy")]
[DisallowConcurrentExecution]
internal class ImportJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        // TODO: Make everything asynchronous
        try
        {
            Importer.RunImport_NewFiles();
            Importer.RunImport_IntegrityCheck();

            // drop folder
            Importer.RunImport_DropFolders();

            // TvDB association checks
            Importer.RunImport_ScanTvDB();

            // Trakt association checks
            Importer.RunImport_ScanTrakt();

            // MovieDB association checks
            Importer.RunImport_ScanMovieDB();

            // Check for missing images
            Importer.RunImport_GetImages();

            // Check for previously ignored files
            Importer.CheckForPreviouslyIgnored();
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
