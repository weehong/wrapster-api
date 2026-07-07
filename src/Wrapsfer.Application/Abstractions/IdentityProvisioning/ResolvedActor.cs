namespace Wrapsfer.Application.Abstractions.IdentityProvisioning;

/// <summary>
/// Identity details for an audit actor resolved from the identity provider.
/// </summary>
/// <param name="Username">The login username (e.g. Keycloak <c>preferred_username</c>).</param>
/// <param name="FullName">The display name (first + last) when set, otherwise <c>null</c>.</param>
public sealed record ResolvedActor(string Username, string? FullName);
