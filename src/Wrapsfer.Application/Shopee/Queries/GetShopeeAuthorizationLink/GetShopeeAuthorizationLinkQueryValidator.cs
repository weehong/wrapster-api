using FluentValidation;

namespace Wrapsfer.Application.Shopee.Queries.GetShopeeAuthorizationLink;

public sealed class GetShopeeAuthorizationLinkQueryValidator
    : AbstractValidator<GetShopeeAuthorizationLinkQuery>
{
    public GetShopeeAuthorizationLinkQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.RedirectUrl)
            .NotEmpty()
            .Must(BeAbsoluteHttpUrl)
            .WithMessage("Redirect URL must be an absolute http(s) URL");
    }

    private static bool BeAbsoluteHttpUrl(string redirectUrl) =>
        Uri.TryCreate(redirectUrl, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
