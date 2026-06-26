using FluentValidation.Results;
using Wrapsfer.Application.Products.Queries.ListProductAuditLogs;

namespace Wrapsfer.Application.Tests.Products.Queries;

public class ListProductAuditLogsQueryValidatorTests
{
    private readonly ListProductAuditLogsQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_HasNoErrors()
    {
        ValidationResult result = _validator.Validate(new ListProductAuditLogsQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validate_WhenPagingInvalid_HasValidationError(int page, int pageSize)
    {
        ValidationResult result = _validator.Validate(new ListProductAuditLogsQuery(Guid.NewGuid(), page, pageSize));

        result.IsValid.Should().BeFalse();
    }
}
