using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Queue;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Application.Waybills.Messaging;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Waybills.Commands;

public class RequestWaybillsExportCommandHandlerTests
{
    private const string OwnerUserId = "owner-user";

    private readonly Mock<IMessagePublisher> _messagePublisher = new();
    private readonly Mock<IPartnerTenantRepository> _partnerTenantRepository = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly RequestWaybillsExportCommandHandler _handler;

    private WaybillsExportRequestedMessage? _published;

    public RequestWaybillsExportCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.UserId).Returns(OwnerUserId);

        _messagePublisher
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(), It.IsAny<WaybillsExportRequestedMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<string, WaybillsExportRequestedMessage, CancellationToken>((_, m, _) => _published = m);

        _handler = new RequestWaybillsExportCommandHandler(
            _messagePublisher.Object,
            _partnerTenantRepository.Object,
            _tenantContext.Object);
    }

    private static PartnerTenant Partner(string tenantId) =>
        PartnerTenant.Create(tenantId, tenantId, "owner").Value;

    [Fact]
    public async Task Handle_OwnerWithExplicitPartnerTenants_PublishesMessageForThosePartnersOnly()
    {
        _partnerTenantRepository.Setup(r => r.ListAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PartnerTenant> { Partner("partner-a"), Partner("partner-b"), Partner("partner-c") });

        RequestWaybillsExportCommand command = new(
            WaybillExportFormat.Csv, null, null, null, null,
            IncludeAllPartnerTenants: true,
            PartnerTenantIds: new[] { "partner-a", "partner-b" });

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _published.Should().NotBeNull();
        _published!.TenantIds.Should().BeEquivalentTo("partner-a", "partner-b");
    }

    [Fact]
    public async Task Handle_OwnerWithNoTarget_ResolvesAllActivePartners()
    {
        _partnerTenantRepository.Setup(r => r.ListAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PartnerTenant> { Partner("partner-a"), Partner("partner-b") });

        RequestWaybillsExportCommand command = new(
            WaybillExportFormat.Xlsx, null, null, null, null,
            IncludeAllPartnerTenants: true);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _published!.TenantIds.Should().BeEquivalentTo("partner-a", "partner-b");
        _partnerTenantRepository.Verify(r => r.ListAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NonOwnerWithPartnerTenantIds_IsRejectedAndDoesNotPublish()
    {
        _tenantContext.Setup(x => x.TenantId).Returns("partner-a");

        RequestWaybillsExportCommand command = new(
            WaybillExportFormat.Csv, null, null, null, null,
            IncludeAllPartnerTenants: false,
            PartnerTenantIds: new[] { "partner-b" });

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WaybillExportErrors.PartnerScopeNotAllowed);
        _messagePublisher.Verify(p => p.PublishAsync(
            It.IsAny<string>(), It.IsAny<WaybillsExportRequestedMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownOrInactivePartnerIds_FailBeforePublish()
    {
        _partnerTenantRepository.Setup(r => r.ListAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PartnerTenant> { Partner("partner-a") });

        RequestWaybillsExportCommand command = new(
            WaybillExportFormat.Csv, null, null, null, null,
            IncludeAllPartnerTenants: true,
            PartnerTenantIds: new[] { "partner-a", "ghost-partner" });

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WaybillExportErrors.UnknownOrInactivePartner);
        _messagePublisher.Verify(p => p.PublishAsync(
            It.IsAny<string>(), It.IsAny<WaybillsExportRequestedMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PartnerSelfExport_PublishesForOwnTenantOnly()
    {
        _tenantContext.Setup(x => x.TenantId).Returns("partner-a");

        RequestWaybillsExportCommand command = new(
            WaybillExportFormat.Pdf, null, null, null, null,
            IncludeAllPartnerTenants: false);

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _published!.TenantIds.Should().ContainSingle().Which.Should().Be("partner-a");
        _partnerTenantRepository.Verify(r => r.ListAsync(It.IsAny<bool?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
