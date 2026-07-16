using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeDropoffBranchEntry
{
    [JsonPropertyName("branch_id")]
    public long BranchId { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }
}
