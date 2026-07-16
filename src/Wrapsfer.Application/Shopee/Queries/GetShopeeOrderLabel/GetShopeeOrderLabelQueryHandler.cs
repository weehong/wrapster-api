using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Abstractions.Shopee;
using Wrapsfer.Application.Abstractions.Storage;
using Wrapsfer.Application.Shopee.Responses;
using Wrapsfer.Application.Shopee.Services;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeOrderLabel;

internal sealed class GetShopeeOrderLabelQueryHandler(
    IShopeeOrderRepository orderRepository,
    IShopeeShopConnectionRepository connectionRepository,
    IShopeeGateway shopeeGateway,
    ShopeeConnectionTokenRefresher tokenRefresher,
    IReportStorage reportStorage,
    IUnitOfWork unitOfWork) : IQueryHandler<GetShopeeOrderLabelQuery, ShopeeOrderLabelResult>
{
    private const string PdfContentType = "application/pdf";

    public async Task<Result<ShopeeOrderLabelResult>> Handle(
        GetShopeeOrderLabelQuery request, CancellationToken cancellationToken)
    {
        ShopeeOrder? order = await orderRepository.GetByIdAsync(
            request.OrderId, request.TenantId, cancellationToken);
        if (order is null)
        {
            return Result<ShopeeOrderLabelResult>.Failure(ShopeeOrderErrors.NotFound);
        }

        if (order.Status != ShopeeOrderStatus.Shipped)
        {
            return Result<ShopeeOrderLabelResult>.Failure(ShopeeOrderErrors.NotShipped);
        }

        string fileName = $"shopee-awb-{order.OrderSn}.pdf";

        if (!string.IsNullOrEmpty(order.LabelStorageKey))
        {
            byte[]? stored = await reportStorage.DownloadAsync(order.LabelStorageKey, cancellationToken);
            if (stored is not null)
            {
                return Result<ShopeeOrderLabelResult>.Success(
                    new ShopeeOrderLabelResult(stored, PdfContentType, fileName));
            }
        }

        ShopeeShopConnection? connection = await connectionRepository.GetByTenantIdAsync(
            request.TenantId, cancellationToken);
        if (connection is null)
        {
            return Result<ShopeeOrderLabelResult>.Failure(ShopeeOrderErrors.ConnectionNotFound);
        }

        await tokenRefresher.RefreshIfNeededAsync(connection, cancellationToken);
        Result<byte[]> documentResult = await shopeeGateway.DownloadShippingDocumentAsync(
            connection.ShopId, connection.AccessToken, order.OrderSn, cancellationToken);
        if (documentResult.IsFailure)
        {
            return Result<ShopeeOrderLabelResult>.Failure(documentResult.Error);
        }

        StoredReport storedReport = await reportStorage.UploadAsync(
            $"shopee-labels/{order.TenantId}/{order.OrderSn}.pdf",
            documentResult.Value, PdfContentType, cancellationToken);

        order.MarkLabelStored(storedReport.ObjectKey);
        order.MarkLabelPrinted(DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ShopeeOrderLabelResult>.Success(
            new ShopeeOrderLabelResult(documentResult.Value, PdfContentType, fileName));
    }
}
