using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.Partners.Responses;

namespace Wrapsfer.Application.Partners.Queries.ListPartners;

public sealed record ListPartnersQuery(bool? IsActive = null) : IQuery<IReadOnlyList<PartnerResponse>>;
