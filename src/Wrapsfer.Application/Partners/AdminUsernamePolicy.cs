using System.Text.RegularExpressions;

namespace Wrapsfer.Application.Partners;

/// <summary>
/// Validation rules for a partner admin username, kept in sync with the constraints Keycloak's
/// default user profile enforces on the realm we provision. A username accepted here is therefore
/// also accepted by Keycloak, so it never fails late inside provisioning.
/// </summary>
internal static partial class AdminUsernamePolicy
{
    // Keycloak's default `length` validator for the username attribute.
    internal const int MinLength = 3;
    internal const int MaxLength = 255;

    internal const string InvalidCharactersMessage =
        "Admin username cannot contain spaces or special characters.";

    internal static bool HasAllowedCharacters(string? value) =>
        !string.IsNullOrEmpty(value) && AllowedRegex().IsMatch(value);

    // Mirrors Keycloak's default `username-prohibited-characters` validator (a match means allowed):
    //   ^[^<>&"'\s\v\h$%!#?§,;:*~/\\|^=\[\]{}()`\p{Cntrl}]+$
    // .NET `\s` already covers `\v`/`\h`; `\p{Cntrl}` maps to the ASCII control range \x00-\x1f plus \x7f.
    [GeneratedRegex(
        @"^[^<>&""'$%!#?§,;:*~/\\|^=\[\]{}()`\s\x00-\x1f\x7f]+$",
        RegexOptions.CultureInvariant)]
    private static partial Regex AllowedRegex();
}
