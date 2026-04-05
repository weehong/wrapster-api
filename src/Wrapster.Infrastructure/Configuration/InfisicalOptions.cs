using System.ComponentModel.DataAnnotations;

namespace Wrapster.Infrastructure.Configuration;

public sealed class InfisicalOptions
{
    public const string SectionName = "Infisical";

    [Required] public string ClientId { get; init; } = string.Empty;

    [Required] public string ClientSecret { get; init; } = string.Empty;

    [Required] public string ProjectId { get; init; } = string.Empty;

    public string HostUri { get; init; } = "https://app.infisical.com";

    public string Environment { get; init; } = "dev";

    public string SecretPath { get; init; } = "/";
}
