using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.FileProcessing;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Products.Responses;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Enums;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;
using TenantSettingsEntity = Wrapster.Domain.Entities.TenantSettings;

namespace Wrapster.Application.Products.Commands.ImportProducts;

internal sealed class ImportProductsCommandHandler(
    IProductFileParser parser,
    IProductRepository productRepository,
    IProductComponentRepository productComponentRepository,
    ITenantSettingsRepository tenantSettingsRepository,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IOptions<ProductSettings> productSettings,
    ILogger<ImportProductsCommandHandler> logger) : ICommandHandler<ImportProductsCommand, ProductImportResult>
{
    public async Task<Result<ProductImportResult>> Handle(ImportProductsCommand request,
        CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        ProductImportBatch batch;
        try
        {
            batch = await parser.ParseAsync(request.File, request.Format, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse product import file for tenant {TenantId}", tenantId);
            return Result<ProductImportResult>.Failure(ProductImportErrors.CorruptedFile);
        }

        if (batch.ParseErrors.Count == 0 && batch.Rows.Count == 0)
        {
            return Result<ProductImportResult>.Failure(ProductImportErrors.EmptyFile);
        }

        List<RowError> errors = [.. batch.ParseErrors];

        // Per-row syntactic validation; keep going to collect all errors.
        List<ParsedRow> parsed = [];
        foreach (ProductImportRow row in batch.Rows)
        {
            ParsedRow parsedRow = ValidateRow(row, errors);
            parsed.Add(parsedRow);
        }

        // Duplicate barcodes within the file.
        Dictionary<string, int> firstRowByBarcode = new(StringComparer.OrdinalIgnoreCase);
        foreach (ParsedRow row in parsed)
        {
            if (row.Barcode is null)
            {
                continue;
            }

            if (firstRowByBarcode.TryGetValue(row.Barcode, out int firstRowNumber))
            {
                errors.Add(new RowError(row.RowNumber, ProductFileColumns.Barcode, row.Barcode,
                    ProductImportMessages.DuplicateBarcodeInFile(row.RowNumber, row.Barcode, firstRowNumber)));
            }
            else
            {
                firstRowByBarcode[row.Barcode] = row.RowNumber;
            }
        }

        // Collect all barcodes we need to resolve: row barcodes + referenced (component child / unpack target).
        HashSet<string> barcodesToLoad = new(StringComparer.OrdinalIgnoreCase);
        foreach (ParsedRow row in parsed)
        {
            if (row.Barcode is not null)
            {
                barcodesToLoad.Add(row.Barcode);
            }

            if (row.UnpackTargetBarcode is not null)
            {
                barcodesToLoad.Add(row.UnpackTargetBarcode);
            }

            if (row.Components is not null)
            {
                foreach ((string childBarcode, int _) in row.Components)
                {
                    barcodesToLoad.Add(childBarcode);
                }
            }
        }

        IReadOnlyList<Product> existing = barcodesToLoad.Count > 0
            ? await productRepository.GetByBarcodesAsync(barcodesToLoad, tenantId, cancellationToken)
            : [];

        Dictionary<string, Product> existingByBarcode =
            existing.ToDictionary(p => p.Barcode, StringComparer.OrdinalIgnoreCase);

        // Build an index of import rows by barcode so bundles/packages can reference
        // products being created in the same file, not just pre-existing ones.
        Dictionary<string, ParsedRow> rowsByBarcode = new(StringComparer.OrdinalIgnoreCase);
        foreach (ParsedRow r in parsed)
        {
            if (r.Barcode is not null && !rowsByBarcode.ContainsKey(r.Barcode))
            {
                rowsByBarcode[r.Barcode] = r;
            }
        }

        foreach (ParsedRow row in parsed)
        {
            if (row.Type == ProductType.Bundle && row.Components is not null)
            {
                foreach ((string childBarcode, int _) in row.Components)
                {
                    ProductType? resolvedType = ResolveType(childBarcode, existingByBarcode, rowsByBarcode);
                    if (resolvedType is null)
                    {
                        errors.Add(new RowError(row.RowNumber, ProductFileColumns.Components, row.Barcode,
                            ProductImportMessages.BundleComponentNotFound(row.RowNumber, row.Name, childBarcode)));
                        continue;
                    }

                    if (resolvedType != ProductType.Single)
                    {
                        errors.Add(new RowError(row.RowNumber, ProductFileColumns.Components, row.Barcode,
                            ProductImportMessages.BundleComponentMustBeSingle(row.RowNumber, row.Name, childBarcode)));
                    }
                }
            }

            if (row.Type == ProductType.Package && row.UnpackTargetBarcode is not null)
            {
                if (string.Equals(row.UnpackTargetBarcode, row.Barcode, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new RowError(row.RowNumber, ProductFileColumns.UnpackTargetBarcode, row.Barcode,
                        ProductImportMessages.PackageTargetSelfReference(row.RowNumber, row.Name)));
                    continue;
                }

                ProductType? resolvedType =
                    ResolveType(row.UnpackTargetBarcode, existingByBarcode, rowsByBarcode);
                if (resolvedType is null)
                {
                    errors.Add(new RowError(row.RowNumber, ProductFileColumns.UnpackTargetBarcode, row.Barcode,
                        ProductImportMessages.PackageTargetNotFound(row.RowNumber, row.Name, row.UnpackTargetBarcode)));
                }
                else if (resolvedType != ProductType.Single)
                {
                    errors.Add(new RowError(row.RowNumber, ProductFileColumns.UnpackTargetBarcode, row.Barcode,
                        ProductImportMessages.PackageTargetMustBeSingle(row.RowNumber, row.Name,
                            row.UnpackTargetBarcode)));
                }
            }
        }

        if (errors.Count > 0)
        {
            return new ProductImportResult(batch.Rows.Count, 0, 0, errors);
        }

        TenantSettingsEntity? settings =
            await tenantSettingsRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        int fallbackThreshold = settings?.DefaultLowStockThreshold ?? productSettings.Value.GlobalLowStockThreshold;

        int createdCount = 0;
        int updatedCount = 0;

        // Process Single first, then Package, then Bundle — guarantees in-file dependencies resolve.
        IEnumerable<ParsedRow> ordered = parsed
            .OrderBy(r => r.Type switch
            {
                ProductType.Single => 0,
                ProductType.Package => 1,
                ProductType.Bundle => 2,
                _ => 3
            })
            .ThenBy(r => r.RowNumber);

        foreach (ParsedRow row in ordered)
        {
            if (row.Barcode is null)
            {
                continue;
            }

            if (existingByBarcode.TryGetValue(row.Barcode, out Product? existingProduct))
            {
                existingProduct.Update(row.Name, row.SkuCode, clearSkuCode: false, row.Cost,
                    row.LowStockThreshold, clearLowStockThreshold: false, fallbackThreshold);
                updatedCount++;
                continue;
            }

            Guid? unpackTargetId = null;
            if (row.Type == ProductType.Package && row.UnpackTargetBarcode is not null &&
                existingByBarcode.TryGetValue(row.UnpackTargetBarcode, out Product? packageTarget))
            {
                unpackTargetId = packageTarget.Id;
            }

            Result<Product> createResult = Product.Create(
                tenantId,
                row.Barcode,
                row.Name!,
                row.Type!.Value,
                row.Cost ?? 0m,
                row.StockQuantity ?? 0,
                row.SkuCode,
                row.LowStockThreshold,
                unpackTargetId,
                row.UnpackQuantityPerPackage);

            if (createResult.IsFailure)
            {
                errors.Add(new RowError(row.RowNumber, null, row.Barcode, createResult.Error.Description));
                continue;
            }

            Product newProduct = createResult.Value;
            newProduct.CheckLowStock(fallbackThreshold);
            productRepository.Add(newProduct);
            existingByBarcode[row.Barcode] = newProduct;
            createdCount++;

            if (row.Type == ProductType.Bundle && row.Components is not null)
            {
                foreach ((string childBarcode, int quantity) in row.Components)
                {
                    Product child = existingByBarcode[childBarcode];
                    Result<ProductComponent> componentResult = ProductComponent.Create(
                        tenantId, newProduct.Id, child.Id, quantity);

                    if (componentResult.IsFailure)
                    {
                        errors.Add(new RowError(row.RowNumber, ProductFileColumns.Components, row.Barcode,
                            componentResult.Error.Description));
                        continue;
                    }

                    productComponentRepository.Add(componentResult.Value);
                }
            }
        }

        if (errors.Count > 0)
        {
            return new ProductImportResult(batch.Rows.Count, 0, 0, errors);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Imported products for tenant {TenantId}: created {Created}, updated {Updated}, total {Total}",
            tenantId, createdCount, updatedCount, batch.Rows.Count);

        return new ProductImportResult(batch.Rows.Count, createdCount, updatedCount, []);
    }

    private static ParsedRow ValidateRow(ProductImportRow raw, List<RowError> errors)
    {
        int rowNumber = raw.RowNumber;
        string? barcode = Trim(raw.Barcode);
        string? name = Trim(raw.Name);
        string? skuCode = Trim(raw.SkuCode);
        string? typeRaw = Trim(raw.Type);
        string? costRaw = Trim(raw.Cost);
        string? stockRaw = Trim(raw.StockQuantity);
        string? lowRaw = Trim(raw.LowStockThreshold);
        string? unpackTargetBarcode = Trim(raw.UnpackTargetBarcode);
        string? unpackQtyRaw = Trim(raw.UnpackQuantityPerPackage);
        string? componentsRaw = Trim(raw.Components);

        if (barcode is null)
        {
            errors.Add(new RowError(rowNumber, ProductFileColumns.Barcode, null,
                ProductImportMessages.RequiredField(rowNumber, ProductFileColumns.Barcode, "a barcode")));
        }

        if (name is null)
        {
            errors.Add(new RowError(rowNumber, ProductFileColumns.Name, barcode,
                ProductImportMessages.RequiredField(rowNumber, ProductFileColumns.Name, "a product name")));
        }

        ProductType? type = null;
        if (typeRaw is null)
        {
            errors.Add(new RowError(rowNumber, ProductFileColumns.Type, barcode,
                ProductImportMessages.RequiredField(rowNumber, ProductFileColumns.Type, "Single, Bundle, or Package")));
        }
        else if (Enum.TryParse(typeRaw, ignoreCase: true, out ProductType parsedType))
        {
            type = parsedType;
        }
        else
        {
            errors.Add(new RowError(rowNumber, ProductFileColumns.Type, barcode,
                ProductImportMessages.UnknownType(rowNumber, typeRaw)));
        }

        decimal? cost = null;
        if (costRaw is null)
        {
            errors.Add(new RowError(rowNumber, ProductFileColumns.Cost, barcode,
                ProductImportMessages.RequiredField(rowNumber, ProductFileColumns.Cost, "a cost, e.g. 9.99")));
        }
        else if (decimal.TryParse(costRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal costValue))
        {
            if (costValue < 0m)
            {
                errors.Add(new RowError(rowNumber, ProductFileColumns.Cost, barcode,
                    ProductImportMessages.NegativeNotAllowed(rowNumber, ProductFileColumns.Cost, "cost")));
            }
            else
            {
                cost = costValue;
            }
        }
        else
        {
            errors.Add(new RowError(rowNumber, ProductFileColumns.Cost, barcode,
                ProductImportMessages.NotANumber(rowNumber, ProductFileColumns.Cost, costRaw, "9.99")));
        }

        int? stock = TryParseInt(stockRaw, ProductFileColumns.StockQuantity, rowNumber, barcode, errors,
            required: true, requiredLabel: "a stock quantity, e.g. 100", allowZero: true);

        int? lowThreshold = null;
        if (lowRaw is not null)
        {
            lowThreshold = TryParseInt(lowRaw, ProductFileColumns.LowStockThreshold, rowNumber, barcode, errors,
                required: false, requiredLabel: null, allowZero: true);
        }

        int? unpackQty = null;
        if (unpackQtyRaw is not null)
        {
            unpackQty = TryParseInt(unpackQtyRaw, ProductFileColumns.UnpackQuantityPerPackage, rowNumber, barcode,
                errors, required: false, requiredLabel: null, allowZero: false);
        }

        List<(string ChildBarcode, int Quantity)>? components = null;
        if (type == ProductType.Bundle)
        {
            if (componentsRaw is null)
            {
                errors.Add(new RowError(rowNumber, ProductFileColumns.Components, barcode,
                    ProductImportMessages.BundleComponentsRequired(rowNumber, name)));
            }
            else
            {
                components = ParseComponents(componentsRaw, rowNumber, barcode, errors);
            }
        }

        if (type == ProductType.Package)
        {
            if (unpackTargetBarcode is null || unpackQty is null || unpackQty <= 0)
            {
                errors.Add(new RowError(rowNumber, ProductFileColumns.UnpackTargetBarcode, barcode,
                    ProductImportMessages.PackageTargetRequired(rowNumber, name)));
            }
        }

        return new ParsedRow(rowNumber, barcode, name, skuCode, type, cost, stock, lowThreshold,
            type == ProductType.Package ? unpackTargetBarcode : null,
            type == ProductType.Package ? unpackQty : null,
            components);
    }

    private static int? TryParseInt(string? raw, string column, int rowNumber, string? barcode,
        List<RowError> errors, bool required, string? requiredLabel, bool allowZero)
    {
        if (raw is null)
        {
            if (required && requiredLabel is not null)
            {
                errors.Add(new RowError(rowNumber, column, barcode,
                    ProductImportMessages.RequiredField(rowNumber, column, requiredLabel)));
            }

            return null;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            errors.Add(new RowError(rowNumber, column, barcode,
                ProductImportMessages.NotAWholeNumber(rowNumber, column, raw)));
            return null;
        }

        if (value < 0)
        {
            errors.Add(new RowError(rowNumber, column, barcode,
                ProductImportMessages.NegativeNotAllowed(rowNumber, column,
                    column == ProductFileColumns.StockQuantity ? "stock"
                    : column == ProductFileColumns.LowStockThreshold ? "the threshold"
                    : "this value")));
            return null;
        }

        if (!allowZero && value == 0)
        {
            errors.Add(new RowError(rowNumber, column, barcode,
                ProductImportMessages.MustBePositive(rowNumber, column,
                    column == ProductFileColumns.UnpackQuantityPerPackage ? "the unpack quantity"
                    : "this value")));
            return null;
        }

        return value;
    }

    private static List<(string ChildBarcode, int Quantity)>? ParseComponents(string value, int rowNumber,
        string? barcode, List<RowError> errors)
    {
        List<(string ChildBarcode, int Quantity)> results = [];
        string[] entries = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (entries.Length == 0)
        {
            errors.Add(new RowError(rowNumber, ProductFileColumns.Components, barcode,
                ProductImportMessages.BundleComponentsMalformed(rowNumber, value)));
            return null;
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string entry in entries)
        {
            string[] parts = entry.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int qty) ||
                qty <= 0)
            {
                errors.Add(new RowError(rowNumber, ProductFileColumns.Components, barcode,
                    ProductImportMessages.BundleComponentsMalformed(rowNumber, value)));
                return null;
            }

            if (!seen.Add(parts[0]))
            {
                errors.Add(new RowError(rowNumber, ProductFileColumns.Components, barcode,
                    ProductImportMessages.BundleComponentsMalformed(rowNumber, value)));
                return null;
            }

            results.Add((parts[0], qty));
        }

        return results;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProductType? ResolveType(string barcode,
        IReadOnlyDictionary<string, Product> existing,
        IReadOnlyDictionary<string, ParsedRow> rowsByBarcode)
    {
        if (existing.TryGetValue(barcode, out Product? product))
        {
            return product.Type;
        }

        if (rowsByBarcode.TryGetValue(barcode, out ParsedRow? row))
        {
            return row.Type;
        }

        return null;
    }

    private sealed record ParsedRow(
        int RowNumber,
        string? Barcode,
        string? Name,
        string? SkuCode,
        ProductType? Type,
        decimal? Cost,
        int? StockQuantity,
        int? LowStockThreshold,
        string? UnpackTargetBarcode,
        int? UnpackQuantityPerPackage,
        List<(string ChildBarcode, int Quantity)>? Components);
}
