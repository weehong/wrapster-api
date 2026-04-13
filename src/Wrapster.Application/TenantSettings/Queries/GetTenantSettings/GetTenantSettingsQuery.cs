using Wrapster.Application.Abstractions.Messaging;
using Wrapster.Application.TenantSettings.Responses;

namespace Wrapster.Application.TenantSettings.Queries.GetTenantSettings;

public sealed record GetTenantSettingsQuery : IQuery<TenantSettingsResponse>;
