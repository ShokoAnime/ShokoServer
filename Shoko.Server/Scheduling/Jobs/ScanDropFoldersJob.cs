// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Quartz;

namespace Shoko.Server.Scheduling.Jobs;

public class ScanDropFoldersJob : IJob
{
    public static readonly JobKey Key = new("ScanDropFolders", "System");
    
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            Importer.RunImport_DropFolders();
        }
        catch (Exception ex)
        {
            // TODO: Logging
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }
}
