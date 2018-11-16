using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Native.Hashing;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.Hash
{
    public class CmdVerifyFile : BaseCommand, ICommand
    {
        private readonly SVR_VideoLocal_Place _file;

        [JsonIgnore]
        public string FullName { get; }

        [JsonIgnore]
        public DateTime? CheckDate { get; private set; }

        [JsonIgnore]
        public int ImportFolderId => _file.ImportFolderID;
            
        private string _parallelTag;

        public CmdVerifyFile(string str) : base(str)
        {
            _file = Repo.Instance.VideoLocal_Place.GetByID(VideoLocalPlaceId);
            FullName = _file.FullServerPath;
        }

        public CmdVerifyFile(SVR_VideoLocal_Place file)
        {
            VideoLocalPlaceId = file.VideoLocal_Place_ID;
            FullName = file.FullServerPath;
            _file = file;
        }

        public int VideoLocalPlaceId { get; set; }

        [JsonIgnore]
        public ScanFileStatus ScanFileStatus { get; set; }

        [JsonIgnore]
        public string OriginalHash => _file.VideoLocal.Hash;

        [JsonIgnore]
        public string VerifiedHash { get; private set; } = string.Empty;

        [JsonIgnore]
        public long OriginalSize => _file.VideoLocal.FileSize;

        [JsonIgnore]
        public long VerifiedSize { get; private set; }


        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.VerifyFile, ExtraParams = new[] {FullName}};
        public string WorkType => WorkTypes.Hashing;

        public string ParallelTag
        {
            get
            {
                if (!string.IsNullOrEmpty(_parallelTag))
                    return _parallelTag;
                if (!string.IsNullOrEmpty(_file.ImportFolder.PhysicalTag))
                    return _file.ImportFolder.PhysicalTag;
                return string.Empty;
            }
            set { _parallelTag = value; }
        }

        public int ParallelMax { get; set; } = 1;


        [JsonIgnore]
        public int Priority { get; set; } = 5;

        public virtual string Id => $"ServerVerifyFile_{VideoLocalPlaceId}";

        public override async Task RunAsync(IProgress<ICommand> progress = null, CancellationToken token = default(CancellationToken))
        {
            try
            {
                ReportInit(progress);
                IFile file = _file.GetFile();
                if (file == null)
                {
                    ScanFileStatus = ScanFileStatus.ErrorFileNotFound;
                    CheckDate=DateTime.Now;
                    ReportFinish(progress);
                    return;
                }

                VerifiedSize = file.Size;
                if (VerifiedSize != OriginalSize)
                {
                    ScanFileStatus = ScanFileStatus.ErrorInvalidSize;
                    CheckDate = DateTime.Now;
                    ReportFinish(progress);
                    return;
                }

                Hasher h = new Hasher(file, HashTypes.ED2K);
                string error = await h.RunAsync(new ChildProgress(0, 100, this, progress), token);
                if (error != null)
                {
                    ScanFileStatus = ScanFileStatus.ErrorIOError;
                    CheckDate = DateTime.Now;
                    ReportError(progress, error);
                    return;
                }

                VerifiedHash = h.Result.GetHash(HashTypes.ED2K);
                if (string.Compare(VerifiedHash, OriginalHash, StringComparison.InvariantCultureIgnoreCase) != 0)
                    ScanFileStatus = ScanFileStatus.ErrorInvalidHash;
                else
                    ScanFileStatus = ScanFileStatus.ProcessedOK;
                CheckDate = DateTime.Now;
                ReportFinish(progress);
            }
            catch (Exception e)
            {
                CheckDate = DateTime.Now;
                ReportError(progress, $"Error processing ServerVerifyFile: {FullName} - {e}", e);
            }
        }
    }
}