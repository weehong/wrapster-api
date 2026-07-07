using Wrapsfer.Domain.Common;

namespace Wrapsfer.Domain.Errors;

public static class ProductImportErrors
{
    public static readonly Error FileRequired = new(
        "ProductImport.FileRequired",
        "Please attach a file to import. We accept .csv and .xlsx files up to 20 MB.",
        ErrorType.Validation);

    public static readonly Error UnsupportedFormat = new(
        "ProductImport.UnsupportedFormat",
        "We only accept .csv and .xlsx files. Please export again in one of those formats.",
        ErrorType.Validation);

    public static readonly Error FileTooLarge = new(
        "ProductImport.FileTooLarge",
        "This file is too large. Please keep uploads under 20 MB, or split into multiple files.",
        ErrorType.Validation);

    public static readonly Error EmptyFile = new(
        "ProductImport.EmptyFile",
        "This file didn't contain any product rows. Please add at least one row of data after the header.",
        ErrorType.Validation);

    public static readonly Error CorruptedFile = new(
        "ProductImport.CorruptedFile",
        "We couldn't read this file. Please make sure it's a valid CSV or XLSX and try again.",
        ErrorType.Validation);
}
