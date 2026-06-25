using System.Diagnostics;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Auditing;
using Wrapsfer.Domain.Common;

namespace Wrapsfer.Application.Behaviors;

/// <summary>
/// Records a durable audit row for every MediatR command/query execution. Constraining
/// <typeparamref name="TResponse"/> to <see cref="Result"/> scopes this behavior to the application's
/// command/query contract (all return <see cref="Result"/> or <see cref="Result{T}"/>) and excludes
/// framework/notification requests. Registered as the outermost behavior so validation failures are
/// captured as <see cref="AuditAction.Failed"/>.
/// </summary>
internal sealed class AuditBehavior<TRequest, TResponse>(
    AuditMetadataAccumulator accumulator,
    IAuditSink sink,
    ITenantContext tenantContext,
    ILogger<AuditBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;
        string correlationId = Guid.NewGuid().ToString("N");

        using AuditMetadataScope scope = accumulator.BeginScope();
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            TResponse response = await next(cancellationToken);
            stopwatch.Stop();

            AuditAction action = response.IsFailure ? AuditAction.Failed : AuditAction.Executed;
            string? errorCode = response.IsFailure ? response.Error.Code : null;

            await SafeWriteAsync(
                requestName,
                correlationId,
                action,
                request,
                scope.Values,
                stopwatch.ElapsedMilliseconds,
                errorCode,
                exceptionType: null);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            await SafeWriteAsync(
                requestName,
                correlationId,
                AuditAction.Failed,
                request,
                scope.Values,
                stopwatch.ElapsedMilliseconds,
                errorCode: null,
                exceptionType: ex.GetType().FullName);

            throw;
        }
    }

    private async Task SafeWriteAsync(
        string requestName,
        string correlationId,
        AuditAction action,
        TRequest request,
        IReadOnlyDictionary<string, object?> metadata,
        long elapsedMilliseconds,
        string? errorCode,
        string? exceptionType)
    {
        try
        {
            string changes = BuildChanges(request, metadata, elapsedMilliseconds, errorCode, exceptionType);

            AuditLogRecord record = new(
                requestName,
                correlationId,
                action,
                changes,
                ResolveTenantId(),
                ResolveUserId(),
                ResolveUsername(),
                DateTime.UtcNow);

            // Use None: a cancelled request must still leave an audit trail.
            await sink.WriteAsync(record, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Auditing must never break the request it is observing.
            logger.LogWarning(ex, "Failed to write audit log for {RequestName}", requestName);
        }
    }

    private static string BuildChanges(
        TRequest request,
        IReadOnlyDictionary<string, object?> metadata,
        long elapsedMilliseconds,
        string? errorCode,
        string? exceptionType)
    {
        Dictionary<string, object?> changes = new()
        {
            ["request"] = SanitizeRequest(request),
            ["elapsedMilliseconds"] = elapsedMilliseconds
        };

        if (errorCode is not null)
        {
            changes["errorCode"] = errorCode;
        }

        if (exceptionType is not null)
        {
            changes["exceptionType"] = exceptionType;
        }

        foreach (KeyValuePair<string, object?> entry in metadata)
        {
            changes[entry.Key] = entry.Value;
        }

        return JsonSerializer.Serialize(changes);
    }

    private static JsonElement SanitizeRequest(TRequest request)
    {
        string sanitizedJson = RequestLoggingSanitizer.Sanitize(request);
        using JsonDocument document = JsonDocument.Parse(sanitizedJson);
        return document.RootElement.Clone();
    }

    private string? ResolveTenantId() => SafeResolve(() => tenantContext.TenantId);

    private string? ResolveUserId() => SafeResolve(() => tenantContext.UserId);

    private string? ResolveUsername() => SafeResolve(() => tenantContext.Username);

    private static string? SafeResolve(Func<string?> accessor)
    {
        try
        {
            return accessor();
        }
        catch
        {
            return null;
        }
    }
}
