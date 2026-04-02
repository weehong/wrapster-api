using Microsoft.Extensions.Options;

namespace Wrapster.Infrastructure.Authentication;

public sealed class KeycloakOptionsValidator : IValidateOptions<KeycloakOptions>
{
    public ValidateOptionsResult Validate(string? name, KeycloakOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            failures.Add($"{KeycloakOptions.SectionName}:BaseUrl is required.");
        }
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out Uri? absoluteUri)
                 || string.IsNullOrWhiteSpace(absoluteUri.Host))
        {
            failures.Add($"{KeycloakOptions.SectionName}:BaseUrl must be a valid absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(options.OwnerRealm))
        {
            failures.Add($"{KeycloakOptions.SectionName}:OwnerRealm is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add($"{KeycloakOptions.SectionName}:Audience is required.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
