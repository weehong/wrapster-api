using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Waybills.Common;
using Wrapsfer.Application.Waybills.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Waybills.Queries.GetWaybillsByDate;

internal sealed class GetWaybillsByDateQueryHandler(
    IWaybillRepository waybillRepository,
    IProductRepository productRepository,
    ITenantContext tenantContext) : IQueryHandler<GetWaybillsByDateQuery, IReadOnlyList<WaybillResponse>>
{
    public async Task<Result<IReadOnlyList<WaybillResponse>>> Handle(GetWaybillsByDateQuery request,
        CancellationToken cancellationToken)
    {
        string tenantId = tenantContext.TenantId;
        IReadOnlyList<Waybill> waybills =
            await waybillRepository.GetByDateAsync(request.PackagingDate, tenantId, cancellationToken);

        IReadOnlyDictionary<Guid, string> namesById = await LoadProductNames(waybills, tenantId, cancellationToken);

        IReadOnlyList<WaybillResponse> responses = waybills
            .Select(w => WaybillResponseMapper.ToResponse(w, namesById))
            .ToList();

        return Result<IReadOnlyList<WaybillResponse>>.Success(responses);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadProductNames(IReadOnlyList<Waybill> waybills,
        string tenantId, CancellationToken cancellationToken)
    {
        HashSet<Guid> productIds = waybills
            .SelectMany(w => w.Items)
            .Select(i => i.ProductId)
            .ToHashSet();

        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return (await productRepository.GetByIdsAsync(productIds, tenantId, cancellationToken))
            .ToDictionary(p => p.Id, p => p.Name);
    }
}
