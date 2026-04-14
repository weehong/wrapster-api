using Wrapster.Application.Abstractions;
using Wrapster.Application.Waybills.Commands.CreateWaybill;
using Wrapster.Domain.Abstractions;
using Wrapster.Domain.Common;
using Wrapster.Domain.Entities;
using Wrapster.Domain.Errors;
using Wrapster.Domain.Repositories;

namespace Wrapster.Application.Tests.Waybills.Commands;

public class CreateWaybillCommandHandlerTests
{
    private const string TenantId = "test-tenant";
    private readonly CreateWaybillCommandHandler _handler;
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Mock<IWaybillRepository> _waybillRepository = new();

    public CreateWaybillCommandHandlerTests()
    {
        _tenantContext.Setup(x => x.TenantId).Returns(TenantId);
        _handler = new CreateWaybillCommandHandler(_waybillRepository.Object, _tenantContext.Object,
            _unitOfWork.Object);
    }

    [Fact]
    public async Task Handle_WhenNumberAlreadyExists_ReturnsDuplicate()
    {
        _waybillRepository.Setup(r =>
                r.ExistsByNumberAsync("WB-1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result<Guid> result = await _handler.Handle(
            new CreateWaybillCommand(new DateOnly(2026, 4, 13), "WB-1"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WaybillErrors.DuplicateWaybillNumber.Code);
    }

    [Fact]
    public async Task Handle_Success_AddsWaybillAndSaves()
    {
        _waybillRepository.Setup(r =>
                r.ExistsByNumberAsync(It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result<Guid> result = await _handler.Handle(
            new CreateWaybillCommand(new DateOnly(2026, 4, 13), "WB-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _waybillRepository.Verify(r => r.Add(It.IsAny<Waybill>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
