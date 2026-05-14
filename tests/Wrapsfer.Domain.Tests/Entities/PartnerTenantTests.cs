using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;

namespace Wrapsfer.Domain.Tests.Entities;

public class PartnerTenantTests
{
    private const string OwnerRealm = "owner";

    [Fact]
    public void Create_WithValidInputs_ReturnsSuccessAndProvisioning()
    {
        Result<PartnerTenant> result = PartnerTenant.Create("partner-acme", "Partner Acme", OwnerRealm);

        result.IsSuccess.Should().BeTrue();
        result.Value.TenantId.Should().Be("partner-acme");
        result.Value.DisplayName.Should().Be("Partner Acme");
        result.Value.IsActive.Should().BeFalse();
        result.Value.ProvisioningStatus.Should().Be(PartnerTenantProvisioningStatus.Provisioning);
        result.Value.DeactivatedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Partner-ACME")]
    [InlineData("ab")]
    [InlineData("-partner")]
    [InlineData("partner-")]
    [InlineData("partner!")]
    public void Create_WithInvalidTenantId_ReturnsValidationFailure(string tenantId)
    {
        Result<PartnerTenant> result = PartnerTenant.Create(tenantId, "Display", OwnerRealm);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerTenantErrors.InvalidTenantId.Code);
    }

    [Fact]
    public void Create_WithTenantIdMatchingOwnerRealm_ReturnsFailure()
    {
        Result<PartnerTenant> result = PartnerTenant.Create("owner", "Display", "Owner");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerTenantErrors.OwnerRealmNotAllowed.Code);
    }

    [Fact]
    public void Create_WithEmptyDisplayName_ReturnsFailure()
    {
        Result<PartnerTenant> result = PartnerTenant.Create("partner-acme", "  ", OwnerRealm);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerTenantErrors.InvalidDisplayName.Code);
    }

    [Fact]
    public void Create_WithInvalidContactEmail_ReturnsFailure()
    {
        Result<PartnerTenant> result = PartnerTenant.Create("partner-acme", "Acme", OwnerRealm, "not-an-email");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerTenantErrors.InvalidContactEmail.Code);
    }

    [Fact]
    public void MarkActive_FromProvisioning_TransitionsToActive()
    {
        PartnerTenant partner = PartnerTenant.Create("partner-acme", "Acme", OwnerRealm).Value;

        Result result = partner.MarkActive();

        result.IsSuccess.Should().BeTrue();
        partner.IsActive.Should().BeTrue();
        partner.ProvisioningStatus.Should().Be(PartnerTenantProvisioningStatus.Active);
    }

    [Fact]
    public void MarkActive_WhenAlreadyActive_ReturnsAlreadyActive()
    {
        PartnerTenant partner = PartnerTenant.Create("partner-acme", "Acme", OwnerRealm).Value;
        partner.MarkActive();

        Result result = partner.MarkActive();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerTenantErrors.AlreadyActive.Code);
    }

    [Fact]
    public void MarkProvisioningFailed_SetsStatusFailedAndStoresTruncatedError()
    {
        PartnerTenant partner = PartnerTenant.Create("partner-acme", "Acme", OwnerRealm).Value;
        string longError = new('x', 2000);

        partner.MarkProvisioningFailed(longError);

        partner.ProvisioningStatus.Should().Be(PartnerTenantProvisioningStatus.Failed);
        partner.IsActive.Should().BeFalse();
        partner.LastProvisioningError.Should().NotBeNull();
        partner.LastProvisioningError!.Length.Should().Be(1024);
    }

    [Fact]
    public void Deactivate_FromActive_SetsDeactivated()
    {
        PartnerTenant partner = PartnerTenant.Create("partner-acme", "Acme", OwnerRealm).Value;
        partner.MarkActive();
        DateTime at = new(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc);

        Result result = partner.Deactivate(at);

        result.IsSuccess.Should().BeTrue();
        partner.IsActive.Should().BeFalse();
        partner.ProvisioningStatus.Should().Be(PartnerTenantProvisioningStatus.Deactivated);
        partner.DeactivatedAt.Should().Be(at);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ReturnsAlreadyInactive()
    {
        PartnerTenant partner = PartnerTenant.Create("partner-acme", "Acme", OwnerRealm).Value;

        Result result = partner.Deactivate(DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerTenantErrors.AlreadyInactive.Code);
    }

    [Fact]
    public void Reactivate_FromDeactivated_TransitionsToActive()
    {
        PartnerTenant partner = PartnerTenant.Create("partner-acme", "Acme", OwnerRealm).Value;
        partner.MarkActive();
        partner.Deactivate(DateTime.UtcNow);

        Result result = partner.Reactivate();

        result.IsSuccess.Should().BeTrue();
        partner.IsActive.Should().BeTrue();
        partner.ProvisioningStatus.Should().Be(PartnerTenantProvisioningStatus.Active);
        partner.DeactivatedAt.Should().BeNull();
    }

    [Fact]
    public void Reactivate_WhenFailed_ReturnsCannotModifyInactive()
    {
        PartnerTenant partner = PartnerTenant.Create("partner-acme", "Acme", OwnerRealm).Value;
        partner.MarkProvisioningFailed("boom");

        Result result = partner.Reactivate();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerTenantErrors.CannotModifyInactive.Code);
    }

    [Fact]
    public void BeginRetry_FromFailed_TransitionsToProvisioning()
    {
        PartnerTenant partner = PartnerTenant.Create("partner-acme", "Acme", OwnerRealm).Value;
        partner.MarkProvisioningFailed("boom");

        Result result = partner.BeginRetry();

        result.IsSuccess.Should().BeTrue();
        partner.ProvisioningStatus.Should().Be(PartnerTenantProvisioningStatus.Provisioning);
        partner.LastProvisioningError.Should().BeNull();
    }

    [Fact]
    public void BeginRetry_WhenNotFailed_ReturnsNotRetryable()
    {
        PartnerTenant partner = PartnerTenant.Create("partner-acme", "Acme", OwnerRealm).Value;
        partner.MarkActive();

        Result result = partner.BeginRetry();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PartnerTenantErrors.NotRetryable.Code);
    }
}
