namespace Wrapsfer.Application.Abstractions.IdentityProvisioning;

/// <summary>
/// Reads user identity details from the identity provider, used to resolve audit actors
/// (e.g. backfilling historical audit rows that only captured a user id).
/// </summary>
public interface IIdentityUserDirectory
{
    /// <summary>
    /// Looks up a user by id within a specific realm.
    /// </summary>
    /// <returns>The resolved actor, or <c>null</c> when no user with that id exists in the realm.</returns>
    Task<ResolvedActor?> ResolveActorAsync(string realm, string userId, CancellationToken cancellationToken = default);
}
