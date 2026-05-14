using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Commands.CreateWaybill;

internal sealed class CreateWaybillCommandHandler(
    IWaybillRepository waybillRepository,
    IProductRepository productRepository,
    StockReservationService stockReservationService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateWaybillCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateWaybillCommand request, CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;

        bool exists =
            await waybillRepository.ExistsByNumberAsync(request.WaybillNumber, tenantId, cancellationToken);
        if (exists)
        {
            return Result<Guid>.Failure(WaybillErrors.DuplicateWaybillNumber);
        }

        IReadOnlyList<string> distinctBarcodes = request.Items
            .Select(i => i.Barcode)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        IReadOnlyList<Product> products =
            await productRepository.GetByBarcodesAsync(distinctBarcodes, tenantId, cancellationToken);

        Dictionary<string, Product> productsByBarcode =
            products.ToDictionary(p => p.Barcode, StringComparer.Ordinal);

        foreach (string barcode in distinctBarcodes)
        {
            if (!productsByBarcode.ContainsKey(barcode))
            {
                return Result<Guid>.Failure(ProductErrors.NotFound);
            }
        }

        Result<Waybill> waybillResult = Waybill.Create(tenantId, request.PackagingDate, request.WaybillNumber);
        if (waybillResult.IsFailure)
        {
            return Result<Guid>.Failure(waybillResult.Error);
        }

        Waybill waybill = waybillResult.Value;

        foreach (CreateWaybillItem item in request.Items)
        {
            Product product = productsByBarcode[item.Barcode];
            Result<WaybillItem> addResult = waybill.AddOrIncrementItem(product.Id, product.Barcode, item.Quantity);
            if (addResult.IsFailure)
            {
                return Result<Guid>.Failure(addResult.Error);
            }
        }

        Result reserveResult =
            await stockReservationService.ReserveItemsAsync(waybill.Items, tenantId, cancellationToken);
        if (reserveResult.IsFailure)
        {
            return Result<Guid>.Failure(reserveResult.Error);
        }

        waybillRepository.Add(waybill);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return waybill.Id;
    }
}
