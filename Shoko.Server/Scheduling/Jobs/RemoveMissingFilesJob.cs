// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Quartz;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling.Jobs;

internal class RemoveMissingFilesJob : IJob
{
    public static readonly JobKey Key = new("RemoveMissingFiles", "Legacy");
    public bool RemoveMyList { get; set; }
    
    public async Task Execute(IJobExecutionContext context)
    {
        Analytics.PostEvent("Importer", "RemoveMissing");
        try
        {
            Importer.RemoveRecordsWithoutPhysicalFiles(RemoveMyList);
        }
        catch (Exception ex)
        {
            // TODO: Logging
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }
}
