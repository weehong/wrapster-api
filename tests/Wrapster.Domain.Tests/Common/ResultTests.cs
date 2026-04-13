using Wrapster.Domain.Common;

namespace Wrapster.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_ReturnsIsSuccessTrue()
    {
        Result result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
    }

    [Fact]
    public void Failure_ReturnsIsSuccessFalse()
    {
        Result result = Result.Failure(Error.Failure);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Success_HasErrorNone()
    {
        Result result = Result.Success();

        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_HasSpecifiedError()
    {
        Error error = new("Test.Error", "Something went wrong", ErrorType.Validation);

        Result result = Result.Failure(error);

        result.Error.Should().Be(error);
    }

    [Fact]
    public void ResultT_Success_ReturnsValue()
    {
        Result<int> result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ResultT_Failure_ThrowsOnValueAccess()
    {
        Result<int> result = Result<int>.Failure(Error.Failure);

        Func<int> act = () => result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResultT_ImplicitConversion_FromNonNull_CreatesSuccess()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void ResultT_ImplicitConversion_FromNull_CreatesNullValueFailure()
    {
        Result<string> result = (string)null!;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NullValue);
    }

    [Fact]
    public void SuccessT_ViaResult_ReturnsSuccess()
    {
        Result<int> result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }
}
