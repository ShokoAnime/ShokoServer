using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Import;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.Server
{
    public class CmdServerDeleteFolder : BaseCommand, ICommand
    {
        public int ImportFolderId { get; set; }
        [JsonIgnore]
        public string ScanFolderPath { get; set; }

        public string ParallelTag { get; set; } = "IMPORT";
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 1;

        public string Id => $"ScanFolder_{ImportFolderId}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.DeleteFolder, ExtraParams = new[] {ScanFolderPath ?? ImportFolderId.ToString()}};
        public string WorkType => WorkTypes.Server;

        public CmdServerDeleteFolder(int importFolderId)
        {
            ImportFolderId = importFolderId;
            SVR_ImportFolder fldr = Repo.Instance.ImportFolder.GetByID(ImportFolderId);
            ScanFolderPath = fldr.ImportFolderLocation;
        }



        public CmdServerDeleteFolder(string str) : base(str)
        {
            SVR_ImportFolder fldr = Repo.Instance.ImportFolder.GetByID(ImportFolderId);
            ScanFolderPath = fldr.ImportFolderLocation;
        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info($"Deleting Import Folder : {ImportFolderId} ({ScanFolderPath}).");
            try
            {
                ReportInit(progress);
                Importer.DeleteImportFolder(ImportFolderId);
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CmdServerScanFolder {ImportFolderId} ({ScanFolderPath}) - {ex}", ex);
            }
        }
    }
}

