using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Tests.Entities;

public class ShopeeShopConnectionTests
{
    private const string TenantId = "partner-acme";
    private const long ShopId = 123456;
    private const string AccessToken = "access-token";
    private const string RefreshToken = "refresh-token";
    private static readonly DateTime s_linkedAt = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime s_accessExpiry = s_linkedAt.AddHours(4);
    private static readonly DateTime s_refreshExpiry = s_linkedAt.AddDays(30);

    private static ShopeeShopConnection CreateConnection() =>
        ShopeeShopConnection.Create(
            TenantId, ShopId, AccessToken, RefreshToken,
            s_accessExpiry, s_refreshExpiry, s_linkedAt, "admin@acme").Value;

    [Fact]
    public void Create_WhenValid_SetsFields()
    {
        ShopeeShopConnection connection = CreateConnection();

        connection.TenantId.Should().Be(TenantId);
        connection.ShopId.Should().Be(ShopId);
        connection.AccessToken.Should().Be(AccessToken);
        connection.RefreshToken.Should().Be(RefreshToken);
        connection.AccessTokenExpiresAt.Should().Be(s_accessExpiry);
        connection.RefreshTokenExpiresAt.Should().Be(s_refreshExpiry);
        connection.LinkedAt.Should().Be(s_linkedAt);
        connection.LinkedBy.Should().Be("admin@acme");
        connection.ShopName.Should().BeNull();
        connection.Region.Should().BeNull();
    }

    [Fact]
    public void Create_WhenTenantIdMissing_Fails()
    {
        Result<ShopeeShopConnection> result = ShopeeShopConnection.Create(
            " ", ShopId, AccessToken, RefreshToken, s_accessExpiry, s_refreshExpiry, s_linkedAt, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.InvalidTenantId);
    }

    [Fact]
    public void Create_WhenShopIdNotPositive_Fails()
    {
        Result<ShopeeShopConnection> result = ShopeeShopConnection.Create(
            TenantId, 0, AccessToken, RefreshToken, s_accessExpiry, s_refreshExpiry, s_linkedAt, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.InvalidShopId);
    }

    [Fact]
    public void Create_WhenAccessTokenMissing_Fails()
    {
        Result<ShopeeShopConnection> result = ShopeeShopConnection.Create(
            TenantId, ShopId, "", RefreshToken, s_accessExpiry, s_refreshExpiry, s_linkedAt, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.InvalidAccessToken);
    }

    [Fact]
    public void Create_WhenRefreshTokenMissing_Fails()
    {
        Result<ShopeeShopConnection> result = ShopeeShopConnection.Create(
            TenantId, ShopId, AccessToken, " ", s_accessExpiry, s_refreshExpiry, s_linkedAt, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.InvalidRefreshToken);
    }

    [Fact]
    public void Create_WhenExpiryMissing_Fails()
    {
        Result<ShopeeShopConnection> result = ShopeeShopConnection.Create(
            TenantId, ShopId, AccessToken, RefreshToken, default, s_refreshExpiry, s_linkedAt, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.InvalidTokenExpiry);
    }

    [Fact]
    public void UpdateTokens_WhenValid_ReplacesTokenPair()
    {
        ShopeeShopConnection connection = CreateConnection();
        DateTime newAccessExpiry = s_accessExpiry.AddHours(4);
        DateTime newRefreshExpiry = s_refreshExpiry.AddDays(1);

        Result result = connection.UpdateTokens("new-access", "new-refresh", newAccessExpiry, newRefreshExpiry);

        result.IsSuccess.Should().BeTrue();
        connection.AccessToken.Should().Be("new-access");
        connection.RefreshToken.Should().Be("new-refresh");
        connection.AccessTokenExpiresAt.Should().Be(newAccessExpiry);
        connection.RefreshTokenExpiresAt.Should().Be(newRefreshExpiry);
    }

    [Fact]
    public void UpdateTokens_WhenAccessTokenMissing_FailsWithoutMutating()
    {
        ShopeeShopConnection connection = CreateConnection();

        Result result = connection.UpdateTokens("", "new-refresh", s_accessExpiry, s_refreshExpiry);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ShopeeShopConnectionErrors.InvalidAccessToken);
        connection.AccessToken.Should().Be(AccessToken);
        connection.RefreshToken.Should().Be(RefreshToken);
    }

    [Fact]
    public void UpdateShopProfile_TrimsAndTruncates()
    {
        ShopeeShopConnection connection = CreateConnection();

        connection.UpdateShopProfile($"  {new string('a', 300)}  ", " SG ");

        connection.ShopName.Should().HaveLength(256);
        connection.Region.Should().Be("SG");
    }

    [Fact]
    public void UpdateShopProfile_WhenBlank_SetsNull()
    {
        ShopeeShopConnection connection = CreateConnection();
        connection.UpdateShopProfile("Acme Store", "SG");

        connection.UpdateShopProfile(" ", null);

        connection.ShopName.Should().BeNull();
        connection.Region.Should().BeNull();
    }
}
