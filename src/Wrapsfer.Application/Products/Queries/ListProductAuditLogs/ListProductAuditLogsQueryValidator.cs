using FluentValidation;

namespace Wrapsfer.Application.Products.Queries.ListProductAuditLogs;

public sealed class ListProductAuditLogsQueryValidator : AbstractValidator<ListProductAuditLogsQuery>
{
    public ListProductAuditLogsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
