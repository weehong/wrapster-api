using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Common;
using Wrapsfer.Application.Waybills.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Queries.GetStaleDraftsReport;

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
