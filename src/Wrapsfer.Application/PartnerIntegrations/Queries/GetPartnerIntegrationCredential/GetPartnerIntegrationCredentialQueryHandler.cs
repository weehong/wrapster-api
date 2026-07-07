using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.PartnerIntegrations.Common;
using Wrapsfer.Application.PartnerIntegrations.Options;
using Wrapsfer.Application.PartnerIntegrations.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.PartnerIntegrations.Queries.GetPartnerIntegrationCredential;

internal sealed class GetPartnerIntegrationCredentialQueryHandler(
    IPartnerIntegrationCredentialRepository credentialRepository,
    IIdentityProviderSettings identityProviderSettings,
    IOptions<PartnerIntegrationOptions> options)
    : IQueryHandler<GetPartnerIntegrationCredentialQuery, PartnerIntegrationCredentialResponse>
{
    public async Task<Result<PartnerIntegrationCredentialResponse>> Handle(
        GetPartnerIntegrationCredentialQuery request,
        CancellationToken cancellationToken)
    {
        PartnerIntegrationCredential? credential =
            await credentialRepository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (credential is null)
        {
            return Result<PartnerIntegrationCredentialResponse>.Failure(
                PartnerIntegrationCredentialErrors.NotFound);
        }

        return Result<PartnerIntegrationCredentialResponse>.Success(
            PartnerIntegrationCredentialResponseMapper.Map(credential, identityProviderSettings, options.Value));
    }
}
