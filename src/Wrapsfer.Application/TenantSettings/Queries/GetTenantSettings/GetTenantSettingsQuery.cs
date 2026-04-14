using Wrapsfer.Application.Abstractions.Messaging;
using Wrapsfer.Application.TenantSettings.Responses;

namespace Wrapsfer.Application.TenantSettings.Queries.GetTenantSettings;

public sealed record GetTenantSettingsQuery : IQuery<TenantSettingsResponse>;
