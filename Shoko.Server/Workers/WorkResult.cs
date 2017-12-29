using NLog;

namespace Shoko.Server.Workers
{
    public class WorkResult
    {
        public WorkResultStatus Status { get; }
        public string Error { get; }

        public WorkResult()
        {
            Status = WorkResultStatus.Ok;
            Error = null;
        }

        public WorkResult(WorkResultStatus status, string error)
        {
            Status = status;
            Error = error;
        }
        public WorkResult(WorkResultStatus status, Logger logger, string error)
        {
            Status = status;
            Error = error;
            logger.Error(Error);
        }
    }
    public class WorkResult<T> : WorkResult
    {
        public T Result { get; }
        public WorkResult(T result)
        {
            Result = result;
        }

        public WorkResult(T result, WorkResultStatus status, string error) : base(status, error)
        {
            Result = result;
        }
        public WorkResult(WorkResultStatus status, string error) : base(status, error)
        {
        }
        public WorkResult(T result, WorkResultStatus status, Logger logger, string error) : base(status, logger, error)
        {
            Result = result;
        }
        public WorkResult(WorkResultStatus status, Logger logger, string error) : base(status, logger,error)
        {
        }
    }
}