using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Partners.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Partners.Queries.ListPartners;

internal sealed class ListPartnersQueryHandler(IPartnerTenantRepository partnerTenantRepository)
    : IQueryHandler<ListPartnersQuery, IReadOnlyList<PartnerResponse>>
{
    public async Task<Result<IReadOnlyList<PartnerResponse>>> Handle(ListPartnersQuery request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PartnerTenant> partners = await partnerTenantRepository.ListAsync(cancellationToken);
        IReadOnlyList<PartnerResponse> responses =
            partners.Select(PartnerResponse.FromEntity).ToList();
        return Result<IReadOnlyList<PartnerResponse>>.Success(responses);
    }
}
