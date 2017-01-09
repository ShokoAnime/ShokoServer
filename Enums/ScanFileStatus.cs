namespace Shoko.Models
{
    public enum ScanFileStatus
    {
        Waiting=0,
        ProcessedOK=1,
        ErrorFileNotFound=2,
        ErrorInvalidSize=3,
        ErrorInvalidHash=4,
        ErrorMissingHash=5,
        ErrorIOError=6
    }
}