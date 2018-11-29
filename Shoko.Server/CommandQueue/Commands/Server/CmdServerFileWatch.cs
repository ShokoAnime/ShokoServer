using System;
using System.IO;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.CommandQueue.Commands.Hash;
using Shoko.Server.Import;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

namespace Shoko.Server.CommandQueue.Commands.Server
{
    public class CmdServerFileWatch : BaseCommand, ICommand
    {
        public WatcherChangeTypes ChangeType { get; set; }
        public string Directory { get; set; }
        public string Name { get; set; }
        



        public string ParallelTag { get; set; } = "FILE_WATCH";
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 1;

        public string Id => $"FileWatch_{ChangeType}_{Directory ?? ""}_{Name??""}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct { QueueState = QueueStateEnum.FileWatch, ExtraParams = new string[] { ChangeType.ToString() , (Directory ?? "")}};
        public string WorkType => WorkTypes.Server;

        public CmdServerFileWatch(FileSystemEventArgs evnt)
        {
            ChangeType = evnt.ChangeType;
            Directory = evnt.FullPath;
            Name = evnt.Name;
        }



        public CmdServerFileWatch(string str) : base(str)
        {

        }

        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info($"Checking File Watch : {ChangeType} - {Directory ?? ""} - {Name ?? ""}");
            try
            {
                ReportInit(progress);
                if ((ChangeType&(WatcherChangeTypes.Created|WatcherChangeTypes.Renamed))>0)
                {
                    if (Directory.StartsWith("|CLOUD|"))
                    {
                        int shareid = int.Parse(Name);
                        Importer.RunImport_ImportFolderNewFiles(Repo.Instance.ImportFolder.GetByID(shareid));
                    }
                    else
                    {
                        // When the path that was created represents a directory we need to manually get the contained files to add.
                        // The reason for this is that when a directory is moved into a source directory (from the same drive) we will only recieve
                        // an event for the directory and not the contained files. However, if the folder is copied from a different drive then
                        // a create event will fire for the directory and each file contained within it (As they are all treated as separate operations)

                        // This is faster and doesn't throw on weird paths. I've had some UTF-16/UTF-32 paths cause serious issues
                        if (System.IO.Directory.Exists(Directory)) // filter out invalid events
                        {
                            logger.Info("New folder detected: {0}: {1}", Directory, ChangeType);

                            string[] files = System.IO.Directory.GetFiles(Directory, "*.*", SearchOption.AllDirectories);

                            foreach (string file in files)
                            {
                                if (Utils.IsVideo(file))
                                {
                                    logger.Info("Found file {0} under folder {1}", file, Directory);

                                    CommandQueue.Queue.Instance.Add(new CmdHashFile(file, false));
                                }
                            }
                        }
                        else if (File.Exists(Directory))
                        {
                            logger.Info("New file detected: {0}: {1}", Directory, ChangeType);

                            if (Utils.IsVideo(Directory))
                            {
                                logger.Info("Found file {0}", Directory);

                                CommandQueue.Queue.Instance.Add(new CmdHashFile(Directory, false));
                            }
                        }
                        // else it was deleted before we got here
                    }
                }
                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing CmdServerFileWatch {ChangeType} - {Directory ?? ""} - {Name ?? ""} - {ex}", ex);
            }
        }
    }
}

