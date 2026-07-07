using FluentValidation.Results;
using Wrapsfer.Application.Waybills.Commands.BulkUpdateWaybillStatus;
using Wrapsfer.Domain.Enums;

namespace Wrapsfer.Application.Tests.Waybills.Commands;

public class BulkUpdateWaybillStatusCommandValidatorTests
{
    private readonly BulkUpdateWaybillStatusCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidPackedCommand_Passes()
    {
        ValidationResult result = _validator.Validate(
            new BulkUpdateWaybillStatusCommand([Guid.NewGuid()], WaybillStatus.Packed, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyIds_Fails()
    {
        ValidationResult result = _validator.Validate(
            new BulkUpdateWaybillStatusCommand([], WaybillStatus.Packed, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BulkUpdateWaybillStatusCommand.Ids));
    }

    [Fact]
    public void Validate_WithMoreThan100Ids_Fails()
    {
        List<Guid> ids = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList();

        ValidationResult result = _validator.Validate(
            new BulkUpdateWaybillStatusCommand(ids, WaybillStatus.Packed, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyGuidInIds_Fails()
    {
        ValidationResult result = _validator.Validate(
            new BulkUpdateWaybillStatusCommand([Guid.Empty], WaybillStatus.Packed, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithDraftStatus_Fails()
    {
        ValidationResult result = _validator.Validate(
            new BulkUpdateWaybillStatusCommand([Guid.NewGuid()], WaybillStatus.Draft, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BulkUpdateWaybillStatusCommand.Status));
    }

    [Fact]
    public void Validate_CancelledWithoutReason_Fails()
    {
        ValidationResult result = _validator.Validate(
            new BulkUpdateWaybillStatusCommand([Guid.NewGuid()], WaybillStatus.Cancelled, null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(BulkUpdateWaybillStatusCommand.Reason));
    }

    [Fact]
    public void Validate_CancelledWithReason_Passes()
    {
        ValidationResult result = _validator.Validate(
            new BulkUpdateWaybillStatusCommand([Guid.NewGuid()], WaybillStatus.Cancelled, "out of stock"));

        result.IsValid.Should().BeTrue();
    }
}
