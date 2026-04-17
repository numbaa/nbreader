using System;

namespace NbReader.Import;

public static class ImportErrorCatalog
{
    public static string GetMessage(ImportErrorCode code)
    {
        return code switch
        {
            ImportErrorCode.InputEmpty => "Input path is empty.",
            ImportErrorCode.PathNotFound => "Input path was not found.",
            ImportErrorCode.UnsupportedInputType => "Input type is not supported.",
            ImportErrorCode.ConfirmationRequired => "Plan requires user confirmation before importing.",
            ImportErrorCode.NoValidImage => "No valid image found in import plan.",
            ImportErrorCode.MixedDirectoryLayout => "Mixed directory layout requires user confirmation.",
            ImportErrorCode.DbConstraintFailed => "Database constraint failed while importing.",
            ImportErrorCode.DbWriteFailed => "Database write failed while importing.",
            _ => "Unexpected import error.",
        };
    }

    public static string Format(ImportErrorCode code, string? details = null)
    {
        var message = GetMessage(code);
        if (string.IsNullOrWhiteSpace(details))
        {
            return message;
        }

        return $"{message} Detail: {details}";
    }
}
