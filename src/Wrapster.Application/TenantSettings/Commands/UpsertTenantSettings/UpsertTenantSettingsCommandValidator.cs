using FluentValidation;

namespace Wrapster.Application.TenantSettings.Commands.UpsertTenantSettings;

public sealed class UpsertTenantSettingsCommandValidator : AbstractValidator<UpsertTenantSettingsCommand>
{
    public UpsertTenantSettingsCommandValidator() =>
        RuleFor(x => x.DefaultLowStockThreshold)
            .GreaterThanOrEqualTo(0)
            .When(x => x.DefaultLowStockThreshold.HasValue);
}
