using Wrapsfer.Application.Abstractions.Messaging;

namespace Wrapsfer.Application.TenantSettings.Commands.UpsertTenantSettings;

public sealed record UpsertTenantSettingsCommand(
    int? DefaultLowStockThreshold,
    bool ClearDefaultLowStockThreshold) : ICommand;
