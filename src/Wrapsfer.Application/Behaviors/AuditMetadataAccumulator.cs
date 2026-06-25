using Wrapsfer.Application.Abstractions.Auditing;

namespace Wrapsfer.Application.Behaviors;

/// <summary>
/// Singleton collector that bridges handlers (which call <see cref="Set"/>) and
/// <see cref="AuditBehavior{TRequest,TResponse}"/> (which opens a scope and reads what was set).
/// The active collection is held in an <see cref="AsyncLocal{T}"/> so concurrent requests and nested
/// MediatR sends each write into their own isolated bag.
/// </summary>
internal sealed class AuditMetadataAccumulator : IAuditMetadata
{
    private static readonly AsyncLocal<Dictionary<string, object?>?> s_current = new();

    public void Set(string key, object? value)
    {
        Dictionary<string, object?>? current = s_current.Value;
        if (current is not null)
        {
            current[key] = value;
        }
    }

    public AuditMetadataScope BeginScope()
    {
        Dictionary<string, object?>? previous = s_current.Value;
        Dictionary<string, object?> values = new();
        s_current.Value = values;

        return new AuditMetadataScope(values, () => s_current.Value = previous);
    }
}
