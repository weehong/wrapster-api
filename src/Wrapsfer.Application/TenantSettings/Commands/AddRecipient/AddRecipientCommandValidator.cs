using FluentValidation;

namespace Wrapsfer.Application.TenantSettings.Commands.AddRecipient;

public sealed class AddRecipientCommandValidator : AbstractValidator<AddRecipientCommand>
{
    public AddRecipientCommandValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}
