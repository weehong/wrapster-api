using System.Text.RegularExpressions;
using FluentValidation;

namespace Wrapsfer.Application.Partners.Commands.CreatePartner;

public sealed partial class CreatePartnerCommandValidator : AbstractValidator<CreatePartnerCommand>
{
    public CreatePartnerCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(63)
            .Must(BeDnsSafeTenantId)
            .WithMessage(
                "TenantId must be 3-63 lowercase alphanumeric characters or dashes, and cannot start or end with a dash.");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.AdminEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(x => x.AdminUsername)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.TemporaryPassword)
            .NotEmpty()
            .MinimumLength(12)
            .WithMessage("Temporary password must be at least 12 characters long.");

        RuleFor(x => x.ContactEmail)
            .EmailAddress()
            .MaximumLength(320)
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
    }

    private static bool BeDnsSafeTenantId(string? value) =>
        !string.IsNullOrEmpty(value) && TenantIdRegex().IsMatch(value);

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex TenantIdRegex();
}
