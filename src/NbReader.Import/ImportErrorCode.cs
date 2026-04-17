namespace NbReader.Import;

public enum ImportErrorCode
{
    InputEmpty,
    PathNotFound,
    UnsupportedInputType,
    ConfirmationRequired,
    NoValidImage,
    MixedDirectoryLayout,
    DbConstraintFailed,
    DbWriteFailed,
    Unknown,
}
