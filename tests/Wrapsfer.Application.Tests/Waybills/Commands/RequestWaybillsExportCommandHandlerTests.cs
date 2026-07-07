using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Queue;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Application.Waybills.Messaging;
using Wrapsfer.Domain.Abstractions;
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
    private readonly Mock<IWaybillExportJobRepository> _jobRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly RequestWaybillsExportCommandHandler _handler;

    private WaybillsExportRequestedMessage? _published;

    public RequestWaybillsExportCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.UserId).Returns(OwnerUserId);
        _tenantContext.Setup(x => x.TenantId).Returns("owner-tenant");
        _tenantContext.Setup(x => x.DisplayName).Returns("Vernon Wee Hong KOH");

        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _messagePublisher
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(), It.IsAny<WaybillsExportRequestedMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<string, WaybillsExportRequestedMessage, CancellationToken>((_, m, _) => _published = m);

        _handler = new RequestWaybillsExportCommandHandler(
            _messagePublisher.Object,
            _partnerTenantRepository.Object,
            _tenantContext.Object,
            _jobRepository.Object,
            _unitOfWork.Object);
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
        _published.RequestedByName.Should().Be("Vernon Wee Hong KOH");
        _published.JobId.Should().NotBe(Guid.Empty);
        _jobRepository.Verify(r => r.Add(It.IsAny<WaybillExportJob>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
            PartnerTenantIds: new[] { "partner-a" });

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _messagePublisher.Verify(
            p => p.PublishAsync(
                It.IsAny<string>(), It.IsAny<WaybillsExportRequestedMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _jobRepository.Verify(r => r.Add(It.IsAny<WaybillExportJob>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownPartner_IsRejected()
    {
        _partnerTenantRepository.Setup(r => r.ListAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PartnerTenant> { Partner("partner-a") });

        RequestWaybillsExportCommand command = new(
            WaybillExportFormat.Pdf, null, null, null, null,
            IncludeAllPartnerTenants: true,
            PartnerTenantIds: new[] { "partner-unknown" });

        Result result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _messagePublisher.Verify(
            p => p.PublishAsync(
                It.IsAny<string>(), It.IsAny<WaybillsExportRequestedMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
