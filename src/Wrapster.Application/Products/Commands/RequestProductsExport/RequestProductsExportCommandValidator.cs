using FluentValidation;
using Wrapster.Application.Products.Commands.ImportProducts;

namespace Wrapster.Application.Products.Commands.RequestProductsExport;

public sealed class RequestProductsExportCommandValidator : AbstractValidator<RequestProductsExportCommand>
{
    public RequestProductsExportCommandValidator()
    {
        RuleFor(x => x.Format)
            .IsInEnum()
            .WithMessage(ProductImportMessages.UnsupportedFormat);
    }
}
