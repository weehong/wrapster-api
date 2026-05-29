using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Queue;
using Wrapsfer.Application.Waybills.Commands.RequestWaybillsExport;
using Wrapsfer.Application.Waybills.Commands.RetryWaybillsExport;
using Wrapsfer.Application.Waybills.Messaging;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Enums;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Waybills.Commands;

public class RetryWaybillsExportCommandHandlerTests
{
    private const string OwnerUserId = "owner-user";

    private readonly Mock<IMessagePublisher> _messagePublisher = new();
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IWaybillExportJobRepository> _jobRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly RetryWaybillsExportCommandHandler _handler;

    private WaybillsExportRequestedMessage? _published;

    public RetryWaybillsExportCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.UserId).Returns(OwnerUserId);
        _tenantContext.Setup(x => x.TenantId).Returns("owner-tenant");

        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _messagePublisher
            .Setup(p => p.PublishAsync(
                It.IsAny<string>(), It.IsAny<WaybillsExportRequestedMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<string, WaybillsExportRequestedMessage, CancellationToken>((_, m, _) => _published = m);

        _handler = new RetryWaybillsExportCommandHandler(
            _messagePublisher.Object,
            _tenantContext.Object,
            _jobRepository.Object,
            _unitOfWork.Object);
    }

    private static WaybillExportJob FailedJob(string userId, string partnersJson = "[\"ophr168\"]")
    {
        WaybillExportJob job = WaybillExportJob.Create(
            requestedByUserId: userId,
            requesterTenantId: "wrapsfer",
            format: "Csv",
            from: new DateOnly(2026, 5, 1),
            to: new DateOnly(2026, 5, 20),
            partnerTenantIdsJson: partnersJson);
        job.MarkFailed("earlier failure");
        return job;
    }

    [Fact]
    public async Task Handle_JobNotFound_ReturnsNotFoundError()
    {
        _jobRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaybillExportJob?)null);

        Result result = await _handler.Handle(new RetryWaybillsExportCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WaybillExportJobErrors.NotFound);
        _messagePublisher.VerifyNoOtherCalls();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_JobOwnedByAnotherUser_ReturnsNotOwnedError()
    {
        WaybillExportJob job = FailedJob(userId: "someone-else");
        _jobRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        Result result = await _handler.Handle(new RetryWaybillsExportCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WaybillExportJobErrors.NotOwned);
        _messagePublisher.VerifyNoOtherCalls();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_JobNotInFailedStatus_ReturnsCannotRetryError()
    {
        WaybillExportJob job = WaybillExportJob.Create(
            OwnerUserId, "wrapsfer", "Csv",
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 20),
            "[\"ophr168\"]");
        _jobRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        Result result = await _handler.Handle(new RetryWaybillsExportCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WaybillExportJobErrors.CannotRetryNonFailed);
        _messagePublisher.VerifyNoOtherCalls();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_FailedJobOwnedByCaller_TransitionsRowAndPublishesMessageWithSameJobId()
    {
        WaybillExportJob job = FailedJob(OwnerUserId);
        _jobRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        Result result = await _handler.Handle(new RetryWaybillsExportCommand(job.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(WaybillExportJobStatus.Retrying);
        job.FailureReason.Should().BeNull();
        job.CompletedAt.Should().BeNull();

        _published.Should().NotBeNull();
        _published!.JobId.Should().Be(job.Id);
        _published.TenantIds.Should().BeEquivalentTo("ophr168");
        _published.Format.Should().Be(WaybillExportFormat.Csv);
        _published.From.Should().Be(new DateOnly(2026, 5, 1));
        _published.To.Should().Be(new DateOnly(2026, 5, 20));
        _published.Status.Should().BeNull();
        _published.Search.Should().BeNull();

        _jobRepository.Verify(r => r.Add(It.IsAny<WaybillExportJob>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
