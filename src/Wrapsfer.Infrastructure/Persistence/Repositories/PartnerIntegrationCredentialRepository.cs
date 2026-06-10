using Microsoft.EntityFrameworkCore;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Infrastructure.Persistence.Repositories;

internal sealed class PartnerIntegrationCredentialRepository(ApplicationDbContext context)
    : IPartnerIntegrationCredentialRepository
{
    public async Task<PartnerIntegrationCredential?> GetByTenantIdAsync(
        string tenantId,
        CancellationToken cancellationToken = default) =>
        await context.PartnerIntegrationCredentials
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);

    public async Task<PartnerIntegrationCredential?> GetByClientIdAsync(
        string clientId,
        CancellationToken cancellationToken = default) =>
        await context.PartnerIntegrationCredentials
            .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

    public async Task<bool> ExistsForTenantAsync(string tenantId, CancellationToken cancellationToken = default) =>
        await context.PartnerIntegrationCredentials
            .AnyAsync(c => c.TenantId == tenantId, cancellationToken);

    public void Add(PartnerIntegrationCredential credential) =>
        context.PartnerIntegrationCredentials.Add(credential);

    public void Update(PartnerIntegrationCredential credential) =>
        context.PartnerIntegrationCredentials.Update(credential);
}
