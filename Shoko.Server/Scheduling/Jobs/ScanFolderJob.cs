// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Quartz;

namespace Shoko.Server.Scheduling.Jobs;

public class ScanFolderJob : IJob
{

    public static readonly JobKey Key = new("ScanFolder", "System");
    
    public async Task Execute(IJobExecutionContext context)
    {
        var folderId = context.MergedJobDataMap.GetInt("importFolderID");
        
        try
        {
            Importer.RunImport_ScanFolder(folderId);
        }
        catch (Exception ex)
        {
            //logger.Error(ex, ex.ToString());
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }
}
