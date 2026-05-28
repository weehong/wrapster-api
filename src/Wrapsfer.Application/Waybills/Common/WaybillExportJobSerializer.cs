using System.Text.Json;

namespace Wrapsfer.Application.Waybills.Common;

internal static class WaybillExportJobSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string SerializeIds(IReadOnlyList<string> values) =>
        JsonSerializer.Serialize(values, Options);

    public static IReadOnlyList<string> DeserializeIds(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(json, Options) ?? [];
    }

    public static IReadOnlyList<string>? DeserializeEmails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        List<string>? emails = JsonSerializer.Deserialize<List<string>>(json, Options);
        return emails is { Count: > 0 } ? emails : null;
    }
}
