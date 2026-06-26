using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Infrastructure.Authentication;
using Wrapsfer.Infrastructure.Persistence;

namespace Wrapsfer.Infrastructure.BackgroundServices;

/// <summary>
/// One-time, mostly-idempotent backfill of audit actor identity. Historical <c>AuditLog</c> rows
/// captured a <c>UserId</c> but no resolvable name (the <c>Username</c> column predates many rows, and
/// <c>ActorName</c>/<c>ActorRealm</c> are newer still), so the UI showed "Unknown user (…)". This job
/// resolves each distinct user from Keycloak — trying the recorded actor realm, then the data tenant,
/// then the owner realm — and fills <c>ActorName</c> (plus <c>Username</c>/<c>ActorRealm</c> when null).
/// Rows whose user cannot be resolved are left untouched (the UI keeps its fallback) and are retried on
/// the next boot; the candidate set shrinks as rows are filled, so this converges.
/// </summary>
public sealed class AuditLogActorBackfillJob(
    IServiceScopeFactory scopeFactory,
    IOptions<KeycloakOptions> options,
    ILogger<AuditLogActorBackfillJob> logger) : BackgroundService
{
    private readonly KeycloakOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (!await context.AuditLogs.AnyAsync(a => a.UserId != null && a.ActorName == null, stoppingToken))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_options.AdminClientId)
                || string.IsNullOrWhiteSpace(_options.AdminClientSecret))
            {
                logger.LogWarning(
                    "Audit actor backfill skipped: Keycloak admin credentials are not configured.");
                return;
            }

            IIdentityUserDirectory directory = scope.ServiceProvider.GetRequiredService<IIdentityUserDirectory>();
            await BackfillAsync(context, directory, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Audit actor backfill failed");
        }
    }

    private async Task BackfillAsync(
        ApplicationDbContext context,
        IIdentityUserDirectory directory,
        CancellationToken cancellationToken)
    {
        // Distinct (user, candidate-realm) tuples across all unresolved rows. A user exists in exactly
        // one realm, so we resolve once per user and apply to every row sharing that user id.
        List<UserRealmCandidates> candidatesByUser = (await context.AuditLogs
                .Where(a => a.UserId != null && a.ActorName == null)
                .Select(a => new { a.UserId, a.ActorRealm, a.TenantId })
                .Distinct()
                .ToListAsync(cancellationToken))
            .GroupBy(t => t.UserId!)
            .Select(g => new UserRealmCandidates(
                g.Key,
                g.SelectMany(t => new[] { t.ActorRealm, t.TenantId })
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r!)
                    .Append(_options.OwnerRealm)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .ToList();

        int resolved = 0;
        int unresolved = 0;
        int updatedRows = 0;

        foreach (UserRealmCandidates candidate in candidatesByUser)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (string realm, ResolvedActor actor)? hit = await ResolveAsync(directory, candidate, cancellationToken);
            if (hit is null)
            {
                unresolved++;
                continue;
            }

            resolved++;
            string userId = candidate.UserId;
            string resolvedRealm = hit.Value.realm;
            string username = hit.Value.actor.Username;
            string actorName = hit.Value.actor.FullName ?? username;

            // ExecuteUpdate writes raw SQL — it bypasses the AuditLogInterceptor (no recursive audit)
            // and works despite AuditLog's init-only properties.
            updatedRows += await context.AuditLogs
                .Where(a => a.UserId == userId && a.ActorName == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(a => a.ActorName, actorName)
                        .SetProperty(a => a.Username, a => a.Username ?? username)
                        .SetProperty(a => a.ActorRealm, a => a.ActorRealm ?? resolvedRealm),
                    cancellationToken);
        }

        logger.LogInformation(
            "Audit actor backfill complete: resolved {Resolved} users, {Unresolved} unresolved, {Rows} rows updated",
            resolved, unresolved, updatedRows);
    }

    private async Task<(string realm, ResolvedActor actor)?> ResolveAsync(
        IIdentityUserDirectory directory,
        UserRealmCandidates candidate,
        CancellationToken cancellationToken)
    {
        foreach (string realm in candidate.CandidateRealms)
        {
            try
            {
                ResolvedActor? actor = await directory.ResolveActorAsync(realm, candidate.UserId, cancellationToken);
                if (actor is not null)
                {
                    return (realm, actor);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Audit actor backfill: failed to resolve user {UserId} in realm {Realm}",
                    candidate.UserId, realm);
            }
        }

        return null;
    }

    private sealed record UserRealmCandidates(string UserId, IReadOnlyList<string> CandidateRealms);
}
