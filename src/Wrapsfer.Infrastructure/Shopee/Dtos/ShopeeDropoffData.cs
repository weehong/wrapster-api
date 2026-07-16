using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeDropoffData
{
    [JsonPropertyName("branch_list")]
    public List<ShopeeDropoffBranchEntry>? BranchList { get; set; }
}
