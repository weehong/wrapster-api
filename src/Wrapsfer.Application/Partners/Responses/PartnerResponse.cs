using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Partners.Responses;

public sealed record PartnerResponse(
    string TenantId,
    string DisplayName,
    string? ContactEmail,
    bool IsActive,
    PartnerTenantProvisioningStatus ProvisioningStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? DeactivatedAt)
{
    public static PartnerResponse FromEntity(PartnerTenant entity) => new(
        entity.TenantId,
        entity.DisplayName,
        entity.ContactEmail,
        entity.IsActive,
        entity.ProvisioningStatus,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.DeactivatedAt);
}
