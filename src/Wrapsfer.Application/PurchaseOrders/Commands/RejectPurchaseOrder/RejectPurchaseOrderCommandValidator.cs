using FluentValidation;

namespace Wrapsfer.Application.PurchaseOrders.Commands.RejectPurchaseOrder;

public sealed class RejectPurchaseOrderCommandValidator : AbstractValidator<RejectPurchaseOrderCommand>
{
    public RejectPurchaseOrderCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
