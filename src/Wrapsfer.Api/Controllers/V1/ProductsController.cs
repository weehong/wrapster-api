using MediatR;
using Microsoft.AspNetCore.Mvc;
using Wrapsfer.Api.Contracts;
using Wrapsfer.Api.Filters;
using Wrapsfer.Application.Abstractions.FileProcessing;
using Wrapsfer.Application.Common;
using Wrapsfer.Application.Products.Commands.CreateProduct;
using Wrapsfer.Application.Products.Commands.DeleteProduct;
using Wrapsfer.Application.Products.Commands.ImportProducts;
using Wrapsfer.Application.Products.Commands.RequestProductsExport;
using Wrapsfer.Application.Products.Commands.SetProductActive;
using Wrapsfer.Application.Products.Commands.UnpackPackage;
using Wrapsfer.Application.Products.Commands.UpdateProduct;
using Wrapsfer.Application.Products.Commands.UpdateProductStock;
using Wrapsfer.Application.Products.Queries.GetProductByBarcode;
using Wrapsfer.Application.Products.Queries.GetProductById;
using Wrapsfer.Application.Products.Queries.GetProductBySku;
using Wrapsfer.Application.Products.Queries.ListProducts;
using Wrapsfer.Application.Products.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Api.Controllers.V1;

[ServiceFilter(typeof(TenantResolutionFilter))]
public sealed class ProductsController(ISender sender, IProductFileWriter fileWriter) : ApiControllerBase
{
    private const long MaxImportBytes = 20L * 1024 * 1024;

    private static readonly HashSet<string> s_csvContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/csv",
        "application/csv",
        "text/plain"
    };

    private static readonly HashSet<string> s_xlsxContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-excel"
    };

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        Result<Guid> result = await sender.Send(command, cancellationToken);
        return ToCreatedResult(result);
    }

    [HttpGet]
    [AllowOwnerTenantScope]
    public async Task<IActionResult> List(
        [FromQuery] string? tenantId,
        [FromQuery] string? search,
        [FromQuery] ProductType? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        bool includeAllPartnerTenants =
            HttpContext.Items[TenantResolutionFilter.OwnerCrossTenantScopeKey] is true;

        Result<PagedResult<ProductResponse>> result =
            await sender.Send(
                new ListProductsQuery(search, type, page, pageSize, includeAllPartnerTenants, includeInactive),
                cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        Result<ProductResponse> result = await sender.Send(new GetProductByIdQuery(id), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("by-barcode/{barcode:maxlength(256)}")]
    public async Task<IActionResult> GetByBarcode(string barcode, CancellationToken cancellationToken)
    {
        Result<ProductResponse> result = await sender.Send(new GetProductByBarcodeQuery(barcode), cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("by-sku/{skuCode:maxlength(256)}")]
    public async Task<IActionResult> GetBySku(string skuCode, CancellationToken cancellationToken)
    {
        Result<ProductResponse> result = await sender.Send(new GetProductBySkuQuery(skuCode), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        UpdateProductCommand command = new(id, request.Name, request.SkuCode, request.ClearSkuCode, request.Cost,
            request.LowStockThreshold, request.ClearLowStockThreshold,
            request.UnpackTargetProductId, request.UnpackQuantityPerPackage,
            request.Components);
        Result result = await sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new DeleteProductCommand(id), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("{id:guid}/stock")]
    public async Task<IActionResult> UpdateStock(Guid id, [FromBody] UpdateProductStockRequest request,
        CancellationToken cancellationToken)
    {
        UpdateProductStockCommand command = new(id, request.NewQuantity);
        Result result = await sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetProductActiveRequest request,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new SetProductActiveCommand(id, request.IsActive), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("import")]
    [RequestSizeLimit(MaxImportBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImportBytes)]
    public async Task<IActionResult> Import(
        [FromQuery] ProductFileFormat format,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return ToActionResult(Result<ProductImportResult>.Failure(ProductImportErrors.FileRequired));
        }

        if (file.Length > MaxImportBytes)
        {
            return ToActionResult(Result<ProductImportResult>.Failure(ProductImportErrors.FileTooLarge));
        }

        HashSet<string> allowed = format == ProductFileFormat.Csv ? s_csvContentTypes : s_xlsxContentTypes;
        if (!allowed.Contains(file.ContentType))
        {
            return ToActionResult(Result<ProductImportResult>.Failure(ProductImportErrors.UnsupportedFormat));
        }

        await using Stream stream = file.OpenReadStream();
        Result<ProductImportResult> result =
            await sender.Send(new ImportProductsCommand(stream, format), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export(
        [FromQuery] ProductFileFormat format,
        CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new RequestProductsExportCommand(format), cancellationToken);
        return result.IsSuccess
            ? Accepted(new { message = "Export requested. You'll receive it by email shortly.", format })
            : ToActionResult(result);
    }

    [HttpGet("import-template")]
    public async Task<IActionResult> DownloadImportTemplate(
        [FromQuery] ProductFileFormat format,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductExportRow> sampleRows =
        [
            new("4900000000001", "Example Single", "SKU-SINGLE-1", nameof(ProductType.Single),
                9.99m, 100, 10, null, null, null),
            new("4900000000002", "Example Bundle", "SKU-BUNDLE-1", nameof(ProductType.Bundle),
                24.99m, 0, null, null, null, "4900000000001:2;4900000000003:1"),
            new("4900000000004", "Example Package", "SKU-PACK-1", nameof(ProductType.Package),
                49.99m, 20, 5, "4900000000001", 12, null)
        ];

        byte[] bytes = await fileWriter.WriteAsync(sampleRows, format, cancellationToken);

        (string contentType, string fileName) = format == ProductFileFormat.Csv
            ? ("text/csv", "wrapsfer-products-template.csv")
            : ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "wrapsfer-products-template.xlsx");

        return File(bytes, contentType, fileName);
    }

    [HttpPost("{id:guid}/unpack")]
    public async Task<IActionResult> Unpack(Guid id, [FromBody] UnpackPackageRequest request,
        CancellationToken cancellationToken)
    {
        UnpackPackageCommand command = new(id, request.Quantity);
        Result result = await sender.Send(command, cancellationToken);
        return ToActionResult(result);
    }
}
