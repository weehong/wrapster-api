using System.Collections;
using System.Reflection;
using System.Text.Json;
using Wrapsfer.Application.Abstractions;

namespace Wrapsfer.Application.Behaviors;

internal static class RequestLoggingSanitizer
{
    private const string RedactedValue = "***REDACTED***";
    private const string CircularReferenceValue = "<circular-reference>";

    public static string Sanitize(object? request)
    {
        object? sanitizedObject = SanitizeValue(
            request,
            new HashSet<object>(ReferenceEqualityComparer.Instance));

        return JsonSerializer.Serialize(sanitizedObject);
    }

    private static object? SanitizeValue(object? value, HashSet<object> visited)
    {
        if (value is null)
        {
            return null;
        }

        Type type = value.GetType();

        if (IsSimpleType(type))
        {
            return value;
        }

        if (value is IEnumerable enumerable)
        {
            List<object?> items = new();

            foreach (object? item in enumerable)
            {
                items.Add(SanitizeValue(item, visited));
            }

            return items;
        }

        if (!type.IsValueType && !visited.Add(value))
        {
            return CircularReferenceValue;
        }

        Dictionary<string, object?> sanitizedProperties = new();

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            if (property.IsDefined(typeof(SensitiveDataAttribute), true))
            {
                sanitizedProperties[property.Name] = RedactedValue;
                continue;
            }

            object? propertyValue;

            try
            {
                propertyValue = property.GetValue(value);
            }
            catch
            {
                sanitizedProperties[property.Name] = "<unavailable>";
                continue;
            }

            sanitizedProperties[property.Name] = SanitizeValue(propertyValue, visited);
        }

        return sanitizedProperties;
    }

    private static bool IsSimpleType(Type type) =>
        type.IsPrimitive ||
        type.IsEnum ||
        type == typeof(string) ||
        type == typeof(decimal) ||
        type == typeof(DateTime) ||
        type == typeof(DateTimeOffset) ||
        type == typeof(TimeSpan) ||
        type == typeof(Guid);
}
