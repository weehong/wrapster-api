using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Wrapster.Application.Behaviors;
using Wrapster.Domain.Common;

namespace Wrapster.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WhenNoValidators_CallsNext()
    {
        List<IValidator<TestCommand>> validators = [];
        ValidationBehavior<TestCommand, Result> behavior = new(validators,
            NullLogger<ValidationBehavior<TestCommand, Result>>.Instance);

        Result result = await behavior.Handle(new TestCommand("value"),
            _ => Task.FromResult(Result.Success()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidationPasses_CallsNext()
    {
        Mock<IValidator<TestCommand>> validator = new();
        validator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        ValidationBehavior<TestCommand, Result> behavior = new([validator.Object],
            NullLogger<ValidationBehavior<TestCommand, Result>>.Instance);

        Result result = await behavior.Handle(new TestCommand("value"),
            _ => Task.FromResult(Result.Success()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ReturnsFailure()
    {
        Mock<IValidator<TestCommand>> validator = new();
        validator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<TestCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new List<ValidationFailure>
            {
                new("Name", "Name is required")
            }));

        ValidationBehavior<TestCommand, Result> behavior = new([validator.Object],
            NullLogger<ValidationBehavior<TestCommand, Result>>.Instance);

        Result result = await behavior.Handle(new TestCommand(""),
            _ => Task.FromResult(Result.Success()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    public sealed record TestCommand(string Name) : IRequest<Result>;
}
