using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed record ShopeeShipOrderDropoffBody(
    [property: JsonPropertyName("branch_id")] long? BranchId);
