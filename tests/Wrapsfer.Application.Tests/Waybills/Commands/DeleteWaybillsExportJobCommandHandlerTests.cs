using Microsoft.Extensions.Logging.Abstractions;
using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Abstractions.Storage;
using Wrapsfer.Application.Waybills.Commands.DeleteWaybillsExportJob;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Waybills.Commands;

public class DeleteWaybillsExportJobCommandHandlerTests
{
    private const string OwnerUserId = "owner-user";

    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IWaybillExportJobRepository> _jobRepository = new();
    private readonly Mock<IReportStorage> _reportStorage = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly DeleteWaybillsExportJobCommandHandler _handler;

    public DeleteWaybillsExportJobCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.UserId).Returns(OwnerUserId);

        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new DeleteWaybillsExportJobCommandHandler(
            _tenantContext.Object,
            _jobRepository.Object,
            _reportStorage.Object,
            _unitOfWork.Object,
            NullLogger<DeleteWaybillsExportJobCommandHandler>.Instance);
    }

    private static WaybillExportJob NewJob(string userId, string keysJson = "[]")
    {
        WaybillExportJob job = WaybillExportJob.Create(
            requestedByUserId: userId,
            requesterTenantId: "wrapsfer",
            format: "Csv",
            from: new DateOnly(2026, 5, 1),
            to: new DateOnly(2026, 5, 20),
            partnerTenantIdsJson: "[\"partner-a\"]");
        job.RecordObjectKeys(keysJson);
        return job;
    }

    [Fact]
    public async Task Handle_JobNotFound_ReturnsNotFoundError()
    {
        _jobRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaybillExportJob?)null);

        Result result = await _handler.Handle(
            new DeleteWaybillsExportJobCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WaybillExportJobErrors.NotFound);
        _reportStorage.VerifyNoOtherCalls();
        _jobRepository.Verify(r => r.Remove(It.IsAny<WaybillExportJob>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OwnedByOtherUser_ReturnsNotOwnedError()
    {
        WaybillExportJob job = NewJob("someone-else");
        job.MarkCompleted();
        _jobRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        Result result = await _handler.Handle(
            new DeleteWaybillsExportJobCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WaybillExportJobErrors.NotOwned);
        _reportStorage.VerifyNoOtherCalls();
        _jobRepository.Verify(r => r.Remove(It.IsAny<WaybillExportJob>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NonTerminalStatus_ReturnsCannotDeleteError()
    {
        WaybillExportJob job = NewJob(OwnerUserId); // status = Queued
        _jobRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        Result result = await _handler.Handle(
            new DeleteWaybillsExportJobCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WaybillExportJobErrors.CannotDeleteNonTerminal);
        _reportStorage.VerifyNoOtherCalls();
        _jobRepository.Verify(r => r.Remove(It.IsAny<WaybillExportJob>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CompletedJob_DeletesEachObjectKeyAndRemovesRow()
    {
        WaybillExportJob job = NewJob(OwnerUserId,
            "[\"waybills/partner-a/2026-05-20/report.csv\",\"waybills/partner-b/2026-05-20/report.csv\"]");
        job.MarkCompleted();
        _jobRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        Result result = await _handler.Handle(
            new DeleteWaybillsExportJobCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _reportStorage.Verify(s => s.DeleteAsync(
            "waybills/partner-a/2026-05-20/report.csv", It.IsAny<CancellationToken>()), Times.Once);
        _reportStorage.Verify(s => s.DeleteAsync(
            "waybills/partner-b/2026-05-20/report.csv", It.IsAny<CancellationToken>()), Times.Once);
        _jobRepository.Verify(r => r.Remove(job), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FailedJobWithNoStoredKeys_StillRemovesRowAndCallsNoS3()
    {
        WaybillExportJob job = NewJob(OwnerUserId);
        job.MarkFailed("boom");
        _jobRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        Result result = await _handler.Handle(
            new DeleteWaybillsExportJobCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _reportStorage.Verify(s => s.DeleteAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _jobRepository.Verify(r => r.Remove(job), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_S3DeleteThrows_StillRemovesRow()
    {
        WaybillExportJob job = NewJob(OwnerUserId,
            "[\"waybills/partner-a/2026-05-20/report.csv\"]");
        job.MarkCompleted();
        _jobRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        _reportStorage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("S3 unavailable"));

        Result result = await _handler.Handle(
            new DeleteWaybillsExportJobCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _jobRepository.Verify(r => r.Remove(job), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
