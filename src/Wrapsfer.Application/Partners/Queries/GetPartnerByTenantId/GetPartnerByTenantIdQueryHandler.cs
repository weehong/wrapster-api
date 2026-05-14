using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Partners.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Partners.Queries.GetPartnerByTenantId;

internal sealed class GetPartnerByTenantIdQueryHandler(IPartnerTenantRepository partnerTenantRepository)
    : IQueryHandler<GetPartnerByTenantIdQuery, PartnerResponse>
{
    public async Task<Result<PartnerResponse>> Handle(GetPartnerByTenantIdQuery request,
        CancellationToken cancellationToken)
    {
        PartnerTenant? partner = await partnerTenantRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (partner is null)
        {
            return Result<PartnerResponse>.Failure(PartnerTenantErrors.NotFound);
        }

        return Result<PartnerResponse>.Success(PartnerResponse.FromEntity(partner));
    }
}
