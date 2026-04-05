namespace Wrapster.Infrastructure.Authentication;

public sealed class KeycloakOptions
{
    public const string SectionName = "Keycloak";

    public string BaseUrl { get; set; } = null!;
    public string OwnerRealm { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public bool RequireHttpsMetadata { get; set; } = true;
}
