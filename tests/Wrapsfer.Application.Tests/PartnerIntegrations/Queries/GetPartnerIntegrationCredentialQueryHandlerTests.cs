using Wrapsfer.Application.Abstractions.IdentityProvisioning;
using Wrapsfer.Application.PartnerIntegrations.Options;
using Wrapsfer.Application.PartnerIntegrations.Queries.GetPartnerIntegrationCredential;
using Wrapsfer.Application.PartnerIntegrations.Responses;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.PartnerIntegrations.Queries;

public class GetPartnerIntegrationCredentialQueryHandlerTests
{
    private const string TenantId = "partner-acme";
    private const string TokenUrl = "http://kc/realms/partner-acme/protocol/openid-connect/token";
    private const string ApiBaseUrl = "https://api.wrapsfer.com";

    private readonly Mock<IPartnerIntegrationCredentialRepository> _credentialRepository = new();
    private readonly GetPartnerIntegrationCredentialQueryHandler _handler;
    private readonly Mock<IIdentityProviderSettings> _identitySettings = new();

    public GetPartnerIntegrationCredentialQueryHandlerTests()
    {
        _identitySettings.Setup(x => x.GetTokenUrl(TenantId)).Returns(TokenUrl);
        _handler = new GetPartnerIntegrationCredentialQueryHandler(
            _credentialRepository.Object,
            _identitySettings.Object,
            Options.Create(new PartnerIntegrationOptions { ApiBaseUrl = ApiBaseUrl }));
    }

    [Fact]
    public async Task Handle_WhenCredentialExists_ReturnsMetadataWithoutSecret()
    {
        PartnerIntegrationCredential credential = PartnerIntegrationCredential.Create(
            TenantId,
            "wrapsfer-partner-acme-integration",
            "9f8d7c6b-5a49-4838-9271-605948372615",
            "Acme Integration").Value;
        _credentialRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credential);

        Result<PartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new GetPartnerIntegrationCredentialQuery(TenantId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TenantId.Should().Be(TenantId);
        result.Value.ClientId.Should().Be("wrapsfer-partner-acme-integration");
        result.Value.TokenUrl.Should().Be(TokenUrl);
        result.Value.ApiBaseUrl.Should().Be(ApiBaseUrl);
        result.Value.IsEnabled.Should().BeTrue();

        // The metadata contract must not expose any secret-bearing member.
        typeof(PartnerIntegrationCredentialResponse).GetProperties()
            .Should().NotContain(p => p.Name.Contains("Secret"));
    }

    [Fact]
    public async Task Handle_WhenCredentialMissing_ReturnsNotFound()
    {
        _credentialRepository.Setup(r => r.GetByTenantIdAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PartnerIntegrationCredential?)null);

        Result<PartnerIntegrationCredentialResponse> result = await _handler.Handle(
            new GetPartnerIntegrationCredentialQuery(TenantId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerIntegrationCredentialErrors.NotFound.Code);
    }
}
