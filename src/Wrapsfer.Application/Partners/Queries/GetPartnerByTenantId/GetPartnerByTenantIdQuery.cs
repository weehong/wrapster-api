using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Partners.Responses;

namespace Wrapsfer.Application.Partners.Queries.GetPartnerByTenantId;

public sealed record GetPartnerByTenantIdQuery(string TenantId) : IQuery<PartnerResponse>;
