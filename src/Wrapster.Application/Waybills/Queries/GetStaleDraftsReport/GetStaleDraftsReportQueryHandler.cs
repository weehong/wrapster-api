using Wrapster.Application.Abstractions;
using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.Waybills.Common;
using Wrapster.Application.Waybills.Responses;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Waybills.Queries.GetStaleDraftsReport;

internal sealed class GetStaleDraftsReportQueryHandler(
    IWaybillRepository waybillRepository,
    IProductRepository productRepository,
    ITenantContext tenantContext) : IQueryHandler<GetStaleDraftsReportQuery, IReadOnlyList<WaybillResponse>>
{
    public async Task<Result<IReadOnlyList<WaybillResponse>>> Handle(GetStaleDraftsReportQuery request,
        CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;
        DateTime since = DateTime.UtcNow.AddHours(-request.HoursBack);

        IReadOnlyList<Waybill> waybills =
            await waybillRepository.GetRecentlyAutoCancelledAsync(tenantId, since, cancellationToken);

        HashSet<Guid> productIds = waybills
            .SelectMany(w => w.Items)
            .Select(i => i.ProductId)
            .ToHashSet();

        IReadOnlyDictionary<Guid, string> namesById = productIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await productRepository.GetByIdsAsync(productIds, tenantId, cancellationToken))
            .ToDictionary(p => p.Id, p => p.Name);

        IReadOnlyList<WaybillResponse> responses = waybills
            .Select(w => WaybillResponseMapper.ToResponse(w, namesById))
            .ToList();

        return Result<IReadOnlyList<WaybillResponse>>.Success(responses);
    }
}
