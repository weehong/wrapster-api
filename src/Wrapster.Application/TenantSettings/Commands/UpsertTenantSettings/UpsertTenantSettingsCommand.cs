using Wrapster.Application.Abstractions.Messaging;

namespace Wrapster.Application.TenantSettings.Commands.UpsertTenantSettings;

public sealed record UpsertTenantSettingsCommand(
    int? DefaultLowStockThreshold,
    bool ClearDefaultLowStockThreshold) : ICommand;
