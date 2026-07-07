using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.UpdateWaybill;

internal sealed class UpdateWaybillCommandHandler(
    IWaybillRepository waybillRepository,
    IProductRepository productRepository,
    StockReservationService stockReservationService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateWaybillCommand>
{
    public async Task<Result> Handle(UpdateWaybillCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        Waybill? waybill =
            await waybillRepository.GetByIdWithItemsAsync(request.Id, tenantId, cancellationToken);
        if (waybill is null)
        {
            return Result.Failure(WaybillErrors.NotFound);
        }

        bool duplicate = await waybillRepository.ExistsByNumberInAnyTenantAsync(
            request.WaybillNumber.Trim(), request.Id, cancellationToken);
        if (duplicate)
        {
            return Result.Failure(WaybillErrors.DuplicateWaybillNumber);
        }

        Result<IReadOnlyList<(Guid ProductId, string Barcode, int Quantity)>> resolveResult =
            await ResolveItemsAsync(request.Items, tenantId, cancellationToken);
        if (resolveResult.IsFailure)
        {
            return Result.Failure(resolveResult.Error);
        }

        Result dateResult = waybill.UpdatePackagingDate(request.PackagingDate);
        if (dateResult.IsFailure)
        {
            return dateResult;
        }

        Result numberResult = waybill.UpdateWaybillNumber(request.WaybillNumber);
        if (numberResult.IsFailure)
        {
            return numberResult;
        }

        Result<IReadOnlyList<QuantityChange>> replaceResult = waybill.ReplaceItems(resolveResult.Value);
        if (replaceResult.IsFailure)
        {
            return Result.Failure(replaceResult.Error);
        }

        foreach (QuantityChange change in replaceResult.Value)
        {
            if (change.Delta > 0)
            {
                Result reserveResult = await stockReservationService.ReserveAsync(
                    change.ProductId, change.Delta, tenantId, cancellationToken);
                if (reserveResult.IsFailure)
                {
                    return reserveResult;
                }
            }
            else if (change.Delta < 0)
            {
                Result releaseResult = await stockReservationService.ReleaseAsync(
                    change.ProductId, -change.Delta, tenantId, cancellationToken);
                if (releaseResult.IsFailure)
                {
                    return releaseResult;
                }
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<IReadOnlyList<(Guid ProductId, string Barcode, int Quantity)>>> ResolveItemsAsync(
        IReadOnlyList<UpdateWaybillItem> items, string tenantId, CancellationToken cancellationToken)
    {
        List<Guid> productIds = items
            .Where(i => i.ProductId.HasValue)
            .Select(i => i.ProductId!.Value)
            .Distinct()
            .ToList();

        List<string> barcodes = items
            .Where(i => !i.ProductId.HasValue && !string.IsNullOrWhiteSpace(i.Barcode))
            .Select(i => i.Barcode!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        IReadOnlyList<Product> productsById = productIds.Count > 0
            ? await productRepository.GetByIdsAsync(productIds, tenantId, cancellationToken)
            : [];
        IReadOnlyList<Product> productsByBarcode = barcodes.Count > 0
            ? await productRepository.GetByBarcodesAsync(barcodes, tenantId, cancellationToken)
            : [];

        Dictionary<Guid, Product> productLookupById = productsById.ToDictionary(p => p.Id);
        Dictionary<string, Product> productLookupByBarcode =
            productsByBarcode.ToDictionary(p => p.Barcode, StringComparer.Ordinal);

        List<(Guid ProductId, string Barcode, int Quantity)> resolved = new(items.Count);

        foreach (UpdateWaybillItem item in items)
        {
            Product? product;

            if (item.ProductId.HasValue)
            {
                if (!productLookupById.TryGetValue(item.ProductId.Value, out product))
                {
                    return Result<IReadOnlyList<(Guid ProductId, string Barcode, int Quantity)>>
                        .Failure(ProductErrors.NotFound);
                }

                if (!string.IsNullOrWhiteSpace(item.Barcode) &&
                    !string.Equals(item.Barcode, product.Barcode, StringComparison.Ordinal))
                {
                    return Result<IReadOnlyList<(Guid ProductId, string Barcode, int Quantity)>>
                        .Failure(WaybillErrors.InvalidBarcode);
                }
            }
            else
            {
                if (!productLookupByBarcode.TryGetValue(item.Barcode!, out product))
                {
                    return Result<IReadOnlyList<(Guid ProductId, string Barcode, int Quantity)>>
                        .Failure(ProductErrors.NotFound);
                }
            }

            if (!product.IsActive)
            {
                return Result<IReadOnlyList<(Guid ProductId, string Barcode, int Quantity)>>
                    .Failure(ProductErrors.Inactive);
            }

            resolved.Add((product.Id, product.Barcode, item.Quantity));
        }

        return Result<IReadOnlyList<(Guid ProductId, string Barcode, int Quantity)>>.Success(resolved);
    }
}
