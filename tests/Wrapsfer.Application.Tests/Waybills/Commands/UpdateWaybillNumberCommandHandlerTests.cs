using Wrapsfer.Application.Abstractions;
using Wrapsfer.Application.Waybills.Commands.UpdateWaybillNumber;
using Wrapsfer.Domain.Abstractions;
using Wrapsfer.Domain.Common;
using Wrapsfer.Domain.Entities;
using Wrapsfer.Domain.Errors;
using Wrapsfer.Domain.Repositories;

namespace Wrapsfer.Application.Tests.Waybills.Commands;

public class UpdateWaybillNumberCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private static readonly DateOnly PackagingDate = new(2026, 5, 21);
    private readonly UpdateWaybillNumberCommandHandler _handler;
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IWaybillRepository> _waybillRepository = new();

    public UpdateWaybillNumberCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new UpdateWaybillNumberCommandHandler(
            _waybillRepository.Object, _tenantContext.Object, _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WhenWaybillNotFound_ReturnsNotFound()
    {
        _waybillRepository.Setup(r =>
                r.GetByIdAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Waybill?)null);

        Result result = await _handler.Handle(
            new UpdateWaybillNumberCommand(Guid.NewGuid(), "WB-2"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WaybillErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenNumberExistsInAnyTenant_ReturnsDuplicate()
    {
        Waybill waybill = Waybill.Create(TenantId, PackagingDate, "WB-1").Value;
        _waybillRepository.Setup(r =>
                r.GetByIdAsync(waybill.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);
        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync("WB-2", waybill.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result result = await _handler.Handle(
            new UpdateWaybillNumberCommand(waybill.Id, "WB-2"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WaybillErrors.DuplicateWaybillNumber.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenWaybillNotDraft_ReturnsCannotEditNonDraft()
    {
        Waybill waybill = Waybill.Create(TenantId, PackagingDate, "WB-1").Value;
        waybill.AddOrIncrementItem(Guid.NewGuid(), "BC-1", 1);
        waybill.MarkPacked();

        _waybillRepository.Setup(r =>
                r.GetByIdAsync(waybill.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);
        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync(It.IsAny<string>(), waybill.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(
            new UpdateWaybillNumberCommand(waybill.Id, "WB-2"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WaybillErrors.CannotEditNonDraft.Code);
    }

    [Fact]
    public async Task Handle_Success_TrimsNumberAndSaves()
    {
        Waybill waybill = Waybill.Create(TenantId, PackagingDate, "WB-1").Value;
        _waybillRepository.Setup(r =>
                r.GetByIdAsync(waybill.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(waybill);
        _waybillRepository.Setup(r =>
                r.ExistsByNumberInAnyTenantAsync(It.IsAny<string>(), waybill.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result result = await _handler.Handle(
            new UpdateWaybillNumberCommand(waybill.Id, " WB-2 "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        waybill.WaybillNumber.Should().Be("WB-2");
        _waybillRepository.Verify(r =>
            r.ExistsByNumberInAnyTenantAsync("WB-2", waybill.Id, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
