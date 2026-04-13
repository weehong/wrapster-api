using Wrapster.Application.Abstractions.FileProcessing;

namespace Wrapster.Application.Products.Commands.ImportProducts;

public static class ProductImportMessages
{
    public const string FileRequired =
        "Please attach a file to import. We accept .csv and .xlsx files up to 20 MB.";

    public const string UnsupportedFormat =
        "We only accept .csv and .xlsx files. Please export again in one of those formats.";

    public const string FileTooLarge =
        "This file is too large. Please keep uploads under 20 MB, or split into multiple files.";

    public const string EmptyFile =
        "This file didn't contain any product rows. Please add at least one row of data after the header.";

    public const string CorruptedFile =
        "We couldn't read this file. Please make sure it's a valid CSV or XLSX and try again.";

    public static string MissingColumn(string columnName) =>
        $"We couldn't find a '{columnName}' column. Please check the header row and make sure all required columns are present.";

    public static string RequiredField(int rowNumber, string columnName, string friendlyLabel) =>
        $"Row {rowNumber}, column '{columnName}': this field is required. Please enter {friendlyLabel}.";

    public static string NotANumber(int rowNumber, string columnName, string value, string example) =>
        $"Row {rowNumber}, column '{columnName}': '{value}' isn't a number. Please use digits only, e.g. {example}.";

    public static string NotAWholeNumber(int rowNumber, string columnName, string value) =>
        $"Row {rowNumber}, column '{columnName}': '{value}' isn't a whole number. Please enter 0 or a positive whole number.";

    public static string NegativeNotAllowed(int rowNumber, string columnName, string what) =>
        $"Row {rowNumber}, column '{columnName}': {what} can't be negative. Please enter 0 or a positive whole number.";

    public static string MustBePositive(int rowNumber, string columnName, string what) =>
        $"Row {rowNumber}, column '{columnName}': {what} must be greater than zero.";

    public static string UnknownType(int rowNumber, string value) =>
        $"Row {rowNumber}, column '{ProductFileColumns.Type}': '{value}' isn't recognised. Use Single, Bundle, or Package.";

    public static string DuplicateBarcodeInFile(int rowNumber, string barcode, int firstRowNumber) =>
        $"Row {rowNumber} uses barcode {barcode}, which also appears on row {firstRowNumber}. Each barcode can appear only once per file.";

    public static string BundleComponentsRequired(int rowNumber, string? bundleName) =>
        $"Row {rowNumber} ({Describe(bundleName)}): components are required for bundles. Please list at least one child using the format childBarcode:quantity.";

    public static string BundleComponentsMalformed(int rowNumber, string value) =>
        $"Row {rowNumber}, column '{ProductFileColumns.Components}': we couldn't read '{value}'. Use the format childBarcode:quantity;childBarcode:quantity (e.g. 4900000000001:2;4900000000002:3).";

    public static string BundleComponentNotFound(int rowNumber, string? bundleName, string childBarcode) =>
        $"Row {rowNumber} ({Describe(bundleName)}): the component {childBarcode} doesn't exist in your catalog yet. Create it first, or remove it from the components list.";

    public static string BundleComponentMustBeSingle(int rowNumber, string? bundleName, string childBarcode) =>
        $"Row {rowNumber} ({Describe(bundleName)}): {childBarcode} is a bundle or package. Bundles can only contain single products.";

    public static string PackageTargetRequired(int rowNumber, string? packageName) =>
        $"Row {rowNumber} ({Describe(packageName)}): a package must specify an unpackTargetBarcode and unpackQuantityPerPackage.";

    public static string PackageTargetNotFound(int rowNumber, string? packageName, string targetBarcode) =>
        $"Row {rowNumber} ({Describe(packageName)}): the unpack target {targetBarcode} doesn't exist in your catalog. Create it first, or correct the unpackTargetBarcode.";

    public static string PackageTargetMustBeSingle(int rowNumber, string? packageName, string targetBarcode) =>
        $"Row {rowNumber} ({Describe(packageName)}): the unpack target {targetBarcode} must be a single product, not a bundle or another package.";

    public static string PackageTargetSelfReference(int rowNumber, string? packageName) =>
        $"Row {rowNumber} ({Describe(packageName)}): a package cannot unpack into itself. Please choose a different target.";

    public static string SkuConflict(int rowNumber, string skuCode) =>
        $"Row {rowNumber}: SKU '{skuCode}' is already used by another product. Please choose a unique SKU or clear this one.";

    private static string Describe(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "row" : $"'{name}'";
}
