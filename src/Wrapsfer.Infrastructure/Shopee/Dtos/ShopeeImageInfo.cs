using System.Text.Json.Serialization;

namespace Wrapsfer.Infrastructure.Shopee.Dtos;

internal sealed class ShopeeImageInfo
{
    [JsonPropertyName("image_url_list")]
    public List<string>? ImageUrlList { get; set; }
}
